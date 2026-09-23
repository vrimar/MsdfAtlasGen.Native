namespace MsdfAtlasGen;

/// <summary>
/// Options for <see cref="MsdfAtlas.Generate"/>. Defaults follow msdf-atlas-gen, except the image type
/// (<see cref="ImageType.Mtsdf"/>) and the glyph size (fixed at 32 pixels per em).
/// </summary>
public sealed record AtlasOptions
{
    /// <summary>Atlas content. Defaults to <see cref="ImageType.Mtsdf"/>.</summary>
    public ImageType ImageType { get; init; } = ImageType.Mtsdf;

    /// <summary>Glyphs to include. Defaults to printable ASCII.</summary>
    public GlyphSelection Glyphs { get; init; } = GlyphSelection.Ascii;

    /// <summary>Fixed glyph scale in pixels per em. Set to 0 to let the packer choose the scale.</summary>
    public double EmSize { get; init; } = 32;

    /// <summary>Minimum pixels per em when <see cref="EmSize"/> is 0. The packer then maximises the scale.</summary>
    public double MinEmSize { get; init; }

    /// <summary>
    /// Distance field range. Equal bounds select 2 pixels; mask image types always use 1 pixel.
    /// Defaults to 2 pixels.
    /// </summary>
    public DistanceRange DistanceRange { get; init; } = DistanceRange.Pixels(2);

    /// <summary>Constraint on the atlas dimensions when they are not fixed.</summary>
    public DimensionsConstraint DimensionsConstraint { get; init; } = DimensionsConstraint.MultipleOfFourSquare;

    /// <summary>Fixed atlas width. Takes effect when both it and <see cref="FixedHeight"/> are positive.</summary>
    public int FixedWidth { get; init; }

    /// <summary>Fixed atlas height. Takes effect when both it and <see cref="FixedWidth"/> are positive.</summary>
    public int FixedHeight { get; init; }

    /// <summary>Pixels between glyph boxes. <see langword="null"/> selects 0 for MSDF/MTSDF and -1 otherwise.</summary>
    public int? Spacing { get; init; }

    /// <summary>Miter limit for PSDF, MSDF and MTSDF; ignored for other image types.</summary>
    public double MiterLimit { get; init; } = 1;

    /// <summary>Aligns the horizontal glyph origin to the pixel grid.</summary>
    public bool PixelAlignOriginX { get; init; }

    /// <summary>Aligns the vertical glyph origin to the pixel grid.</summary>
    public bool PixelAlignOriginY { get; init; } = true;

    /// <summary>Padding inside the glyph quad.</summary>
    public Padding InnerPadding { get; init; } = Padding.None;

    /// <summary>Padding outside the glyph quad.</summary>
    public Padding OuterPadding { get; init; } = Padding.None;

    /// <summary>Edge coloring strategy for MSDF and MTSDF.</summary>
    public EdgeColoring EdgeColoring { get; init; } = EdgeColoring.InkTrap;

    /// <summary>Corner angle threshold in radians for edge coloring.</summary>
    public double AngleThreshold { get; init; } = 3;

    /// <summary>Seed for edge coloring.</summary>
    public ulong ColoringSeed { get; init; }

    /// <summary>Error correction preset for MSDF and MTSDF.</summary>
    public ErrorCorrectionMode ErrorCorrection { get; init; } = ErrorCorrectionMode.AutoMixed;

    /// <summary>Supports overlapping contours.</summary>
    public bool OverlapSupport { get; init; } = true;

    /// <summary>Runs a scanline pass to fix signs. Disables the error correction distance check.</summary>
    public bool ScanlinePass { get; init; } = true;

    /// <summary>Loads kerning pairs for the selected glyphs.</summary>
    public bool Kerning { get; init; } = true;

    /// <summary>Worker threads. 0 uses the processor count.</summary>
    public int ThreadCount { get; init; }

    /// <summary>Row order of the pixels and orientation of metrics. Defaults to <see cref="YOrigin.Bottom"/>.</summary>
    public YOrigin YOrigin { get; init; } = YOrigin.Bottom;

    internal unsafe NativeOptions ToNative(uint* ids)
    {
        return new NativeOptions
        {
            StructSize = (uint)sizeof(NativeOptions),
            AbiVersion = NativeMethods.AbiVersionExpected,
            GlyphSelection = (int)Glyphs.Kind,
            IdCount = (uint)Glyphs.Ids.Length,
            Ids = ids,
            ImageType = (int)ImageType,
            YOrigin = (int)YOrigin,
            EmSize = EmSize,
            MinEmSize = MinEmSize,
            RangeUnits = (int)DistanceRange.Units,
            RangeLower = DistanceRange.Lower,
            RangeUpper = DistanceRange.Upper,
            DimensionsConstraint = (int)DimensionsConstraint,
            Width = FixedWidth,
            Height = FixedHeight,
            Spacing = Spacing ?? NativeMethods.SpacingDefault,
            MiterLimit = MiterLimit,
            PxAlignOriginX = PixelAlignOriginX ? 1 : 0,
            PxAlignOriginY = PixelAlignOriginY ? 1 : 0,
            InnerPaddingUnits = (int)InnerPadding.Units,
            OuterPaddingUnits = (int)OuterPadding.Units,
            InnerPaddingLeft = InnerPadding.Left,
            InnerPaddingBottom = InnerPadding.Bottom,
            InnerPaddingRight = InnerPadding.Right,
            InnerPaddingTop = InnerPadding.Top,
            OuterPaddingLeft = OuterPadding.Left,
            OuterPaddingBottom = OuterPadding.Bottom,
            OuterPaddingRight = OuterPadding.Right,
            OuterPaddingTop = OuterPadding.Top,
            EdgeColoring = (int)EdgeColoring,
            AngleThreshold = AngleThreshold,
            ColoringSeed = ColoringSeed,
            ErrorCorrection = (int)ErrorCorrection,
            OverlapSupport = OverlapSupport ? 1 : 0,
            ScanlinePass = ScanlinePass ? 1 : 0,
            Kerning = Kerning ? 1 : 0,
            ThreadCount = ThreadCount,
        };
    }
}

/// <summary>The set of glyphs to place in the atlas.</summary>
public sealed class GlyphSelection
{
    private readonly uint[] _ids;

    private GlyphSelection(GlyphSelectionKind kind, uint[] ids)
    {
        Kind = kind;
        _ids = ids;
    }

    /// <summary>Printable ASCII, U+0020 to U+007E.</summary>
    public static GlyphSelection Ascii { get; } = new(GlyphSelectionKind.Ascii, []);

    /// <summary>Every glyph in the font, identified by glyph index.</summary>
    public static GlyphSelection AllGlyphs { get; } = new(GlyphSelectionKind.AllGlyphs, []);

    /// <summary>How <see cref="Ids"/> are interpreted.</summary>
    public GlyphSelectionKind Kind { get; }

    /// <summary>Codepoints or glyph indices; empty for <see cref="Ascii"/> and <see cref="AllGlyphs"/>.</summary>
    public ReadOnlySpan<uint> Ids => _ids;

    internal uint[] IdArray => _ids;

    /// <summary>Selects glyphs by Unicode codepoint.</summary>
    /// <exception cref="ArgumentException"><paramref name="codepoints"/> is empty.</exception>
    public static GlyphSelection Codepoints(params ReadOnlySpan<uint> codepoints) =>
        Create(GlyphSelectionKind.Codepoints, codepoints, nameof(codepoints));

    /// <summary>Selects glyphs by glyph index.</summary>
    /// <exception cref="ArgumentException"><paramref name="glyphIndices"/> is empty.</exception>
    public static GlyphSelection GlyphIndices(params ReadOnlySpan<uint> glyphIndices) =>
        Create(GlyphSelectionKind.GlyphIndices, glyphIndices, nameof(glyphIndices));

    /// <summary>Selects the codepoints of every character in <paramref name="text"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> is empty.</exception>
    public static GlyphSelection FromString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var codepoints = new List<uint>(text.Length);
        foreach (var rune in text.EnumerateRunes())
            codepoints.Add((uint)rune.Value);
        return Create(GlyphSelectionKind.Codepoints, codepoints.ToArray(), nameof(text));
    }

    private static GlyphSelection Create(GlyphSelectionKind kind, ReadOnlySpan<uint> ids, string paramName)
    {
        if (ids.IsEmpty)
            throw new ArgumentException("At least one glyph must be selected.", paramName);
        return new GlyphSelection(kind, ids.ToArray());
    }
}

/// <summary>Distance field range from <paramref name="Lower"/> to <paramref name="Upper"/>.</summary>
public readonly record struct DistanceRange(double Lower, double Upper, AtlasUnits Units)
{
    /// <summary>A range of <paramref name="width"/> pixels centred on the glyph edge.</summary>
    public static DistanceRange Pixels(double width) => new(-0.5 * width, 0.5 * width, AtlasUnits.Pixels);

    /// <summary>A range of <paramref name="width"/> ems centred on the glyph edge.</summary>
    public static DistanceRange Ems(double width) => new(-0.5 * width, 0.5 * width, AtlasUnits.Ems);
}

/// <summary>Padding on each side of a glyph quad.</summary>
public readonly record struct Padding(double Left, double Bottom, double Right, double Top, AtlasUnits Units)
{
    /// <summary>No padding.</summary>
    public static Padding None => new(0, 0, 0, 0, AtlasUnits.Ems);

    /// <summary>The same padding on every side.</summary>
    public static Padding Uniform(double value, AtlasUnits units) => new(value, value, value, value, units);
}
