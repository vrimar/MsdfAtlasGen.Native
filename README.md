# MsdfAtlasGen.Native

A thin NuGet package that bundles the [`msdf-atlas-gen`](https://github.com/Chlumsky/msdf-atlas-gen)
executable (MSDF/MTSDF font-atlas generator) as native runtime assets for `win-x64`, `linux-x64`,
`osx-x64`, and `osx-arm64`, plus a tiny managed helper that resolves the RID-appropriate binary at
runtime. Upstream ships Windows-only release binaries; this package builds every RID from source in CI.

## Install

```xml
<PackageReference Include="MsdfAtlasGen.Native" Version="X.Y.Z" />
```

The RID-appropriate `msdf-atlas-gen[.exe]` is copied into your build/publish output automatically
by a `buildTransitive` targets file (the binary is packed under `tools/<rid>/`).

## Use

```csharp
using MsdfAtlasGen.Native;

var tool = MsdfAtlasGenTool.ExecutablePath; // absolute path to the bundled binary for the current RID;
                                            // ensures the exec bit on Unix. Throws PlatformNotSupportedException
                                            // on an unbundled RID, FileNotFoundException if the asset is missing.

var psi = new ProcessStartInfo(tool) { UseShellExecute = false };
psi.ArgumentList.Add("-font");     psi.ArgumentList.Add("MyFont.ttf");
psi.ArgumentList.Add("-type");     psi.ArgumentList.Add("mtsdf");
psi.ArgumentList.Add("-imageout"); psi.ArgumentList.Add("atlas.png");
psi.ArgumentList.Add("-json");     psi.ArgumentList.Add("atlas.json");
Process.Start(psi)!.WaitForExit();
```

The package exposes only the binary; atlas parameters (`-type`, `-size`, `-pxrange`, …) are the
caller's policy.

## Packaged runtime identifiers

| RID | Notes |
|---|---|
| `win-x64` | statically linked, single `.exe` |
| `linux-x64` | statically linked (except glibc/libstdc++) |
| `osx-x64` | statically linked |
| `osx-arm64` | statically linked |

## Building from source

Binaries are produced by CI (`.github/workflows/build-native.yml`) from the pinned
`external/msdf-atlas-gen` submodule (tag `v1.4`, recursive → `msdfgen`). Locally:

```bash
git submodule update --init --recursive
# Unix (needs cmake + a vcpkg checkout in $VCPKG_ROOT):
build/build-native-unix.sh linux-x64      # or osx-x64 / osx-arm64
# Windows:
pwsh build/build-native-win.ps1
```

Skia-based geometry preprocessing is disabled (`-DMSDF_ATLAS_USE_SKIA=OFF`), which keeps the vcpkg
dependency set to `freetype` + `libpng` and produces a self-contained executable. This is sufficient
for standard `-type mtsdf -format png` atlas generation.

Output is staged into `artifacts/native/<rid>/` and packed under `tools/<rid>/` in the NuGet package.

## Releasing

Tag `vX.Y.Z` (or run the `package` workflow manually) → CI builds all RIDs, packs, and pushes to
NuGet.org using the `NUGET_API_KEY` repository secret.

## License

MIT (this wrapper). Redistributed native binaries retain their upstream licenses — see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
