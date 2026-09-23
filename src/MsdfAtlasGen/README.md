# MsdfAtlasGen

In-process MSDF/MTSDF font-atlas generation for .NET, powered by
[`msdf-atlas-gen`](https://github.com/Chlumsky/msdf-atlas-gen) and
[`msdfgen`](https://github.com/Chlumsky/msdfgen).

The upstream C++ libraries are compiled from source into a small native library (`msdf_atlas_c`) with a
stable C API, and wrapped by an AOT- and trim-compatible managed API. You get the atlas pixels, glyph
layout, font metrics and kerning as .NET objects, with no process to spawn and no temporary files.

For the standalone `msdf-atlas-gen` command-line executable, use the sibling package
[`MsdfAtlasGen.Native`](https://www.nuget.org/packages/MsdfAtlasGen.Native).

## Install

```xml
<PackageReference Include="MsdfAtlasGen" Version="X.Y.Z" />
```

## Use

```csharp
using MsdfAtlasGen;

var atlas = MsdfAtlas.Generate(File.ReadAllBytes("MyFont.ttf"), new AtlasOptions
{
    ImageType = ImageType.Mtsdf,
    Glyphs = GlyphSelection.Ascii,
    EmSize = 48,
    DistanceRange = DistanceRange.Pixels(6),
    YOrigin = YOrigin.Top,
});

ReadOnlyMemory<byte> rgba = atlas.Pixels;          // atlas.Width × atlas.Height × atlas.Channels
foreach (var glyph in atlas.Glyphs)
{
    // glyph.Codepoint, glyph.Advance, glyph.PlaneBounds (ems), glyph.AtlasBounds (pixels)
}

File.WriteAllBytes("atlas.png", atlas.EncodePng());
atlas.ExportJson("atlas.json");                     // same layout as msdf-atlas-gen -json
```

`MsdfAtlas.Generate` is safe to call concurrently. It throws `MsdfAtlasException` with a
`Status` when generation fails, for example `PackDidNotFit` when fixed atlas dimensions are too small.

## Differences from the msdf-atlas-gen CLI

- The default image type is `Mtsdf` (CLI: `msdf`).
- The default glyph size is a fixed 32 pixels per em (CLI: a minimum of 32 that the packer may grow).
  Set `EmSize = 0` and `MinEmSize` to get the CLI behaviour.
- Glyphs are selected with `GlyphSelection` (ASCII, codepoints, a string, glyph indices, or all glyphs)
  rather than charset files.
- Kerning is loaded whenever `Kerning` is true; the CLI only loads it when a JSON output is requested.
- Only tight packing of a single font is supported. Grid packing, font variants, variable-font axes and
  image formats other than PNG are not exposed.

## Packaged runtime identifiers

| RID | Native asset |
|---|---|
| `win-x64` | `runtimes/win-x64/native/msdf_atlas_c.dll` (static CRT) |
| `linux-x64` | `runtimes/linux-x64/native/libmsdf_atlas_c.so` (glibc 2.35+) |
| `osx-x64` | `runtimes/osx-x64/native/libmsdf_atlas_c.dylib` (macOS 11+) |
| `osx-arm64` | `runtimes/osx-arm64/native/libmsdf_atlas_c.dylib` (macOS 11+) |

The .NET host resolves the library from `runtimes/<rid>/native/` automatically, for both portable
and RID-specific builds and publishes. FreeType, libpng and zlib are linked statically, and the library
exports only its C API.

## License

MIT. Redistributed native code retains its upstream licenses; see `THIRD-PARTY-NOTICES.md`.
