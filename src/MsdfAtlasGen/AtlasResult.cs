using System.Buffers;
using System.Text;
using System.Text.Json;

namespace MsdfAtlasGen;

/// <summary>A generated atlas: pixels, layout and metrics. Immutable and independent of native memory.</summary>
public sealed class AtlasResult
{
    private readonly byte[] _pixels;

    private AtlasResult(
        byte[] pixels,
        int width,
        int height,
        int channels,
        ImageType imageType,
        YOrigin yOrigin,
        AtlasMetrics atlas,
        FontMetrics font,
        GlyphInfo[] glyphs,
        KerningPair[] kerning,
        bool glyphsByCodepoint,
        bool kerningEnabled
    )
    {
        _pixels = pixels;
        Width = width;
        Height = height;
        Channels = channels;
        ImageType = imageType;
        YOrigin = yOrigin;
        Atlas = atlas;
        Font = font;
        Glyphs = glyphs;
        Kerning = kerning;
        GlyphsByCodepoint = glyphsByCodepoint;
        KerningEnabled = kerningEnabled;
    }

    /// <summary>Atlas width in pixels.</summary>
    public int Width { get; }

    /// <summary>Atlas height in pixels.</summary>
    public int Height { get; }

    /// <summary>Bytes per pixel: 1, 3 (MSDF) or 4 (MTSDF).</summary>
    public int Channels { get; }

    /// <summary>Atlas content.</summary>
    public ImageType ImageType { get; }

    /// <summary>Row order of <see cref="Pixels"/> and orientation of all bounds and metrics.</summary>
    public YOrigin YOrigin { get; }

    /// <summary>Interleaved 8-bit pixels, <see cref="Width"/> × <see cref="Channels"/> bytes per row.</summary>
    public ReadOnlyMemory<byte> Pixels => _pixels;

    /// <summary>Final scale, distance range and glyph counts.</summary>
    public AtlasMetrics Atlas { get; }

    /// <summary>Font metrics in ems.</summary>
    public FontMetrics Font { get; }

    /// <summary>Glyphs in atlas order.</summary>
    public IReadOnlyList<GlyphInfo> Glyphs { get; }

    /// <summary>Kerning pairs by glyph index; empty when kerning was not requested.</summary>
    public IReadOnlyList<KerningPair> Kerning { get; }

    /// <summary>Whether glyphs were selected by codepoint rather than by glyph index.</summary>
    public bool GlyphsByCodepoint { get; }

    /// <summary>Whether kerning was requested.</summary>
    public bool KerningEnabled { get; }

    /// <summary>Encodes the atlas as a PNG, always top-down regardless of <see cref="YOrigin"/>.</summary>
    /// <exception cref="MsdfAtlasException">Encoding failed.</exception>
    public unsafe byte[] EncodePng()
    {
        byte* data = null;
        nuint size = 0;
        fixed (byte* pixels = _pixels)
        {
            try
            {
                var status = NativeMethods.EncodePng(pixels, Width, Height, Channels, (int)YOrigin, &data, &size);
                if (status != (int)MsdfAtlasStatus.Ok)
                    throw MsdfAtlasException.FromStatus(status, 0);
                return new ReadOnlySpan<byte>(data, checked((int)size)).ToArray();
            }
            finally
            {
                NativeMethods.BufferFree(data);
            }
        }
    }

    /// <summary>Writes the msdf-atlas-gen JSON layout to <paramref name="path"/>.</summary>
    public void ExportJson(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.Create(path);
        WriteJson(stream);
    }

    /// <summary>Writes the msdf-atlas-gen JSON layout to <paramref name="stream"/>.</summary>
    public void WriteJson(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var writer = new Utf8JsonWriter(stream);
        AtlasJsonWriter.Write(writer, this);
    }

    /// <summary>Returns the msdf-atlas-gen JSON layout.</summary>
    public string ToJson()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
            AtlasJsonWriter.Write(writer, this);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal static unsafe AtlasResult FromNative(nint result, AtlasOptions options)
    {
        int width, height, channels;
        var image = NativeMethods.ResultImage(result, &width, &height, &channels);
        var pixels = new ReadOnlySpan<byte>(image, checked(width * height * channels)).ToArray();

        var atlas = NativeMethods.ResultAtlasMetrics(result);
        var font = NativeMethods.ResultFontMetrics(result);

        uint glyphCount;
        var nativeGlyphs = NativeMethods.ResultGlyphs(result, &glyphCount);
        var glyphs = new GlyphInfo[glyphCount];
        for (var i = 0; i < glyphs.Length; i++)
        {
            ref readonly var g = ref nativeGlyphs[i];
            glyphs[i] = new GlyphInfo(
                g.GlyphIndex,
                g.Codepoint,
                g.IsWhitespace != 0,
                g.Advance,
                g.HasPlaneBounds != 0 ? new GlyphBounds(g.PlaneLeft, g.PlaneBottom, g.PlaneRight, g.PlaneTop) : null,
                g.HasAtlasBounds != 0 ? new GlyphBounds(g.AtlasLeft, g.AtlasBottom, g.AtlasRight, g.AtlasTop) : null
            );
        }

        uint kerningCount;
        var nativeKerning = NativeMethods.ResultKerning(result, &kerningCount);
        var kerning = new KerningPair[kerningCount];
        for (var i = 0; i < kerning.Length; i++)
        {
            ref readonly var k = ref nativeKerning[i];
            kerning[i] = new KerningPair(k.GlyphIndex1, k.GlyphIndex2, k.Codepoint1, k.Codepoint2, k.Advance);
        }

        return new AtlasResult(
            pixels,
            width,
            height,
            channels,
            (ImageType)atlas->ImageType,
            (YOrigin)atlas->YOrigin,
            new AtlasMetrics(atlas->Scale, atlas->PxRangeLower, atlas->PxRangeUpper, atlas->GlyphsRequested, atlas->GlyphsLoaded),
            new FontMetrics(font->EmSize, font->AscenderY, font->DescenderY, font->LineHeight, font->UnderlineY, font->UnderlineThickness),
            glyphs,
            kerning,
            options.Glyphs.Kind is GlyphSelectionKind.Ascii or GlyphSelectionKind.Codepoints,
            options.Kerning
        );
    }
}

/// <summary>Final atlas scale and distance range.</summary>
/// <param name="Scale">Pixels per em.</param>
/// <param name="DistanceRangeLower">Lower bound of the distance range in pixels.</param>
/// <param name="DistanceRangeUpper">Upper bound of the distance range in pixels.</param>
/// <param name="GlyphsRequested">Distinct glyphs requested by the selection.</param>
/// <param name="GlyphsLoaded">Glyphs found in the font and placed in the atlas.</param>
public readonly record struct AtlasMetrics(
    double Scale,
    double DistanceRangeLower,
    double DistanceRangeUpper,
    int GlyphsRequested,
    int GlyphsLoaded
)
{
    /// <summary>Width of the distance range in pixels.</summary>
    public double DistanceRange => DistanceRangeUpper - DistanceRangeLower;

    /// <summary>Midpoint of the distance range in pixels.</summary>
    public double DistanceRangeMiddle => 0.5 * (DistanceRangeLower + DistanceRangeUpper);
}

/// <summary>Font metrics in ems; vertical values are signed per <see cref="AtlasResult.YOrigin"/>.</summary>
public readonly record struct FontMetrics(
    double EmSize,
    double Ascender,
    double Descender,
    double LineHeight,
    double UnderlineY,
    double UnderlineThickness
);

/// <summary>A glyph placed in the atlas.</summary>
/// <param name="GlyphIndex">Glyph index in the font.</param>
/// <param name="Codepoint">Unicode codepoint, or 0 when selected by glyph index.</param>
/// <param name="IsWhitespace">Whether the glyph has no outline.</param>
/// <param name="Advance">Horizontal advance in ems.</param>
/// <param name="PlaneBounds">Quad bounds in ems relative to the glyph origin; <see langword="null"/> without a box.</param>
/// <param name="AtlasBounds">Quad bounds in atlas pixels; <see langword="null"/> without a box.</param>
public readonly record struct GlyphInfo(
    int GlyphIndex,
    uint Codepoint,
    bool IsWhitespace,
    double Advance,
    GlyphBounds? PlaneBounds,
    GlyphBounds? AtlasBounds
);

/// <summary>Axis-aligned bounds, signed per <see cref="AtlasResult.YOrigin"/>.</summary>
public readonly record struct GlyphBounds(double Left, double Bottom, double Right, double Top);

/// <summary>Kerning adjustment between two glyphs, in ems.</summary>
/// <param name="GlyphIndex1">Glyph index of the left glyph.</param>
/// <param name="GlyphIndex2">Glyph index of the right glyph.</param>
/// <param name="Codepoint1">Codepoint of the left glyph, or 0 when it has none.</param>
/// <param name="Codepoint2">Codepoint of the right glyph, or 0 when it has none.</param>
/// <param name="Advance">Advance adjustment in ems.</param>
public readonly record struct KerningPair(
    int GlyphIndex1,
    int GlyphIndex2,
    uint Codepoint1,
    uint Codepoint2,
    double Advance
);

/// <summary>Versions reported by the loaded native library.</summary>
/// <param name="AbiVersion">C ABI version.</param>
/// <param name="MsdfAtlasGen">Bundled msdf-atlas-gen version.</param>
/// <param name="Msdfgen">Bundled msdfgen version.</param>
public readonly record struct NativeVersionInfo(uint AbiVersion, string MsdfAtlasGen, string Msdfgen);
