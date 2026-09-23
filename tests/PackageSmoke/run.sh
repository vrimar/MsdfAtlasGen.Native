#!/usr/bin/env bash
set -euo pipefail

if [ $# -ne 3 ]; then
    echo "usage: $0 <nupkg-dir> <version> <rid>" >&2
    exit 2
fi

nupkgs="$(cd "$1" && pwd)"
version="$2"
rid="$3"

cd "$(dirname "$0")/../.."

work=artifacts/package-smoke
project=tests/PackageSmoke/PackageSmoke.csproj
font=tests/MsdfAtlasGen.Native.Tests/assets/test-font.ttf
exe=PackageSmoke
[[ "$rid" == win-* ]] && exe=PackageSmoke.exe

rm -rf "$work" tests/PackageSmoke/bin tests/PackageSmoke/obj
mkdir -p "$work/feed"
for id in MsdfAtlasGen MsdfAtlasGen.Native; do
    cp "$nupkgs/$id.$version.nupkg" "$work/feed/"
done

# pwd -W gives Git Bash on Windows a path dotnet understands.
work_abs="$(cd "$work" && { pwd -W 2>/dev/null || pwd; })"
# Private cache: a shared one may hold a stale nupkg with this same version.
export NUGET_PACKAGES="$work_abs/nuget-packages"

props=(-c Release "-p:MsdfPackageVersion=$version")

for tfm in net8.0 net10.0; do
    echo "== portable run ($tfm)"
    dotnet run --project "$project" -f "$tfm" "${props[@]}" -- "$font"
done

echo "== RID-specific publish ($rid)"
dotnet publish "$project" -f net10.0 -r "$rid" "${props[@]}" -o "$work/publish-rid"
"$work/publish-rid/$exe" "$font"

echo "== Native AOT publish ($rid)"
dotnet publish "$project" -f net10.0 -r "$rid" "${props[@]}" -p:PublishAot=true -o "$work/publish-aot"
"$work/publish-aot/$exe" "$font"

echo "== package smoke passed ($rid, $version)"
