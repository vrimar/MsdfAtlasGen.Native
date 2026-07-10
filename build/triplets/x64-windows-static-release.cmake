set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CRT_LINKAGE static)
set(VCPKG_LIBRARY_LINKAGE static)

# Release-only: skip Debug variants of freetype/libpng in CI. Static CRT +
# static libs so the produced msdf-atlas-gen.exe is a single self-contained file.
set(VCPKG_BUILD_TYPE release)
