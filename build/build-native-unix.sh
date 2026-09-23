#!/usr/bin/env bash
# Builds the msdf-atlas-gen executable and the msdf_atlas_c shared library on
# Linux or macOS using CMake + vcpkg.
#
# Usage: build-native-unix.sh <rid>
#   rid: linux-x64 | osx-x64 | osx-arm64
#
# Dependencies (install before running):
#   Linux (Debian/Ubuntu): sudo apt install build-essential cmake ninja-build \
#                                           curl zip unzip tar pkg-config
#   macOS (Homebrew):      brew install cmake ninja
#
# vcpkg provides freetype + libpng (static). Skia geometry-preprocessing is
# disabled, so both binaries are self-contained. If VCPKG_ROOT is unset a local
# vcpkg checkout is bootstrapped under build/vcpkg-local.
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <rid>   (linux-x64 | osx-x64 | osx-arm64)" >&2
    exit 1
fi

RID="$1"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
MSDF="$REPO/external/msdf-atlas-gen"
SRC="$REPO/native"
TRIPLETS="$REPO/build/triplets"

EXTRA_CMAKE_ARGS=()
case "$RID" in
    linux-x64)
        TARGET_TRIPLET="x64-linux-release"
        LIBNAME="libmsdf_atlas_c.so"
        ;;
    osx-x64)
        TARGET_TRIPLET="x64-osx-release"
        LIBNAME="libmsdf_atlas_c.dylib"
        EXTRA_CMAKE_ARGS=(-DCMAKE_OSX_ARCHITECTURES=x86_64 -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0)
        ;;
    osx-arm64)
        TARGET_TRIPLET="arm64-osx-release"
        LIBNAME="libmsdf_atlas_c.dylib"
        EXTRA_CMAKE_ARGS=(-DCMAKE_OSX_ARCHITECTURES=arm64 -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0)
        ;;
    *)
        echo "Unsupported RID: $RID" >&2
        exit 1
        ;;
esac

if [ ! -f "$MSDF/CMakeLists.txt" ]; then
    echo "msdf-atlas-gen submodule missing at $MSDF. Run: git submodule update --init --recursive" >&2
    exit 1
fi

# Apple clang rejects the C-style cast in main.cpp as ambiguous because
# BitmapAtlasStorage defines conversion operators to both BitmapConstSection and
# BitmapConstRef (gcc/MSVC pick the direct one). Force the direct operator so the
# source compiles identically on every toolchain. Guarded → idempotent.
MAIN_CPP="$MSDF/msdf-atlas-gen/main.cpp"
if grep -q '(msdfgen::BitmapConstSection<T, N>) generator.atlasStorage()' "$MAIN_CPP"; then
    echo "[build-native-unix] Patching main.cpp: disambiguate BitmapConstSection cast."
    sed -i.bak 's/(msdfgen::BitmapConstSection<T, N>) generator\.atlasStorage()/generator.atlasStorage().operator msdfgen::BitmapConstSection<T, N>()/' "$MAIN_CPP"
    rm -f "$MAIN_CPP.bak"
fi

# Resolve vcpkg.
VCPKG_ROOT="${VCPKG_ROOT:-}"
if [ -z "$VCPKG_ROOT" ]; then
    VCPKG_ROOT="$REPO/build/vcpkg-local"
    if [ ! -x "$VCPKG_ROOT/vcpkg" ]; then
        echo "[build-native-unix] Bootstrapping local vcpkg at $VCPKG_ROOT"
        [ -d "$VCPKG_ROOT" ] || git clone --depth 1 https://github.com/microsoft/vcpkg.git "$VCPKG_ROOT"
        "$VCPKG_ROOT/bootstrap-vcpkg.sh" -disableMetrics
    fi
fi
TOOLCHAIN="$VCPKG_ROOT/scripts/buildsystems/vcpkg.cmake"
if [ ! -f "$TOOLCHAIN" ]; then
    echo "vcpkg toolchain not found at $TOOLCHAIN. Set VCPKG_ROOT to your vcpkg checkout." >&2
    exit 1
fi

NPROC="$(getconf _NPROCESSORS_ONLN 2>/dev/null || sysctl -n hw.physicalcpu 2>/dev/null || echo 4)"
BUILD_DIR="$REPO/build/build-$RID"
rm -rf "$BUILD_DIR"

GENERATOR="Unix Makefiles"
command -v ninja >/dev/null 2>&1 && GENERATOR="Ninja"

echo "[build-native-unix] cmake configure ($RID, triplet=$TARGET_TRIPLET, generator=$GENERATOR)"
cmake -S "$SRC" -B "$BUILD_DIR" -G "$GENERATOR" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE="$TOOLCHAIN" \
    -DVCPKG_OVERLAY_TRIPLETS="$TRIPLETS" \
    -DVCPKG_TARGET_TRIPLET="$TARGET_TRIPLET" \
    -DMSDF_ATLAS_USE_VCPKG=ON \
    -DMSDF_ATLAS_USE_SKIA=OFF \
    -DMSDF_ATLAS_NO_ARTERY_FONT=ON \
    -DMSDF_ATLAS_MSDFGEN_EXTERNAL=OFF \
    -DMSDF_ATLAS_BUILD_STANDALONE=ON \
    -DMSDF_ATLAS_INSTALL=OFF \
    -DBUILD_SHARED_LIBS=OFF \
    "${EXTRA_CMAKE_ARGS[@]}"

echo "[build-native-unix] cmake --build ($NPROC jobs)"
cmake --build "$BUILD_DIR" --config Release --parallel "$NPROC"

EXE="$(find "$BUILD_DIR" -type f -name 'msdf-atlas-gen' -perm -u+x | head -n1)"
if [ -z "$EXE" ]; then
    echo "msdf-atlas-gen executable not produced; inspect $BUILD_DIR." >&2
    exit 1
fi

LIB="$BUILD_DIR/bin/$LIBNAME"
if [ ! -f "$LIB" ]; then
    echo "$LIBNAME not produced; inspect $BUILD_DIR." >&2
    exit 1
fi

NATIVE_OUT="$REPO/artifacts/native/$RID"
mkdir -p "$NATIVE_OUT"
cp "$EXE" "$NATIVE_OUT/msdf-atlas-gen"
chmod +x "$NATIVE_OUT/msdf-atlas-gen"
cp "$LIB" "$NATIVE_OUT/$LIBNAME"

echo "[build-native-unix] Staged: $NATIVE_OUT/msdf-atlas-gen, $NATIVE_OUT/$LIBNAME"
ls -la "$NATIVE_OUT"

# Smoke: the binary must at least start and report its version (skip on cross-arch osx).
if [ "$RID" = "linux-x64" ] || [ "$RID" = "osx-arm64" ] || [ "$RID" = "osx-x64" ]; then
    if [ "$(uname -m)" != "arm64" ] || [ "$RID" != "osx-x64" ]; then
        "$NATIVE_OUT/msdf-atlas-gen" -version || true
    fi
fi

case "$RID" in
    linux-x64)
        bad="$(nm -D --defined-only "$NATIVE_OUT/$LIBNAME" | awk '{print $3}' | grep -v '^msdf_atlas_' || true)"
        [ -z "$bad" ] || { echo "Unexpected exports in $LIBNAME:" >&2; echo "$bad" >&2; exit 1; }
        bad="$(readelf -d "$NATIVE_OUT/$LIBNAME" | awk '/\(NEEDED\)/ {gsub(/[][]/, "", $5); print $5}' | grep -vE '^((libstdc\+\+|libm|libgcc_s|libc|libpthread|libdl|librt)\.so|ld-linux)' || true)"
        [ -z "$bad" ] || { echo "Unexpected dependencies of $LIBNAME:" >&2; echo "$bad" >&2; exit 1; }
        ;;
    osx-*)
        bad="$(nm -gU "$NATIVE_OUT/$LIBNAME" | awk '{print $3}' | grep -v '^_msdf_atlas_' || true)"
        [ -z "$bad" ] || { echo "Unexpected exports in $LIBNAME:" >&2; echo "$bad" >&2; exit 1; }
        bad="$(otool -L "$NATIVE_OUT/$LIBNAME" | tail -n +2 | awk '{print $1}' | grep -vE '^(/usr/lib/|/System/Library/|@rpath/libmsdf_atlas_c\.dylib$)' || true)"
        [ -z "$bad" ] || { echo "Unexpected dependencies of $LIBNAME:" >&2; echo "$bad" >&2; exit 1; }
        codesign --force --sign - "$NATIVE_OUT/$LIBNAME"
        codesign --verify --verbose "$NATIVE_OUT/$LIBNAME"
        ;;
esac
