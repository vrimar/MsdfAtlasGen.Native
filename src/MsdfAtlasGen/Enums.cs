namespace MsdfAtlasGen;

/// <summary>Content stored in the atlas image.</summary>
public enum ImageType
{
    /// <summary>Glyph coverage without anti-aliasing (1 channel).</summary>
    HardMask = 0,

    /// <summary>Anti-aliased glyph coverage (1 channel).</summary>
    SoftMask = 1,

    /// <summary>True signed distance field (1 channel).</summary>
    Sdf = 2,

    /// <summary>Signed perpendicular distance field (1 channel).</summary>
    Psdf = 3,

    /// <summary>Multi-channel signed distance field (3 channels).</summary>
    Msdf = 4,

    /// <summary>Multi-channel and true signed distance field (4 channels).</summary>
    Mtsdf = 5,
}

/// <summary>Constraint applied to the atlas dimensions when they are not fixed.</summary>
public enum DimensionsConstraint
{
    /// <summary>Resolved to <see cref="MultipleOfFourSquare"/>, like the CLI.</summary>
    None = 0,

    /// <summary>The smallest square that fits the glyphs.</summary>
    Square = 1,

    /// <summary>The smallest square with an even side.</summary>
    EvenSquare = 2,

    /// <summary>The smallest square with a side that is a multiple of four.</summary>
    MultipleOfFourSquare = 3,

    /// <summary>The smallest rectangle with power-of-two sides.</summary>
    PowerOfTwoRectangle = 4,

    /// <summary>The smallest square with a power-of-two side.</summary>
    PowerOfTwoSquare = 5,
}

/// <summary>Units for distance ranges and paddings.</summary>
public enum AtlasUnits
{
    /// <summary>Atlas pixels.</summary>
    Pixels = 0,

    /// <summary>Font ems.</summary>
    Ems = 1,
}

/// <summary>Edge coloring strategy for <see cref="ImageType.Msdf"/> and <see cref="ImageType.Mtsdf"/>.</summary>
public enum EdgeColoring
{
    /// <summary>Colors edges in order around each contour.</summary>
    Simple = 0,

    /// <summary>Like <see cref="Simple"/>, but keeps narrow ink traps sharp.</summary>
    InkTrap = 1,

    /// <summary>Picks colors by the distance between edges. Slowest and most accurate.</summary>
    ByDistance = 2,
}

/// <summary>The msdf-atlas-gen <c>-errorcorrection</c> presets.</summary>
public enum ErrorCorrectionMode
{
    /// <summary>Detects inversions by distance evaluation and other distance errors by range testing.</summary>
    AutoMixed = 0,

    /// <summary>Detects inversions and other distance errors by range testing.</summary>
    AutoFast = 1,

    /// <summary>Detects inversions and other distance errors by exact distance evaluation.</summary>
    AutoFull = 2,

    /// <summary>Applies no error correction.</summary>
    Disabled = 3,

    /// <summary>Detects distance errors by range testing, including those at edges and corners.</summary>
    DistanceFast = 4,

    /// <summary>Detects distance errors by exact distance evaluation, including those at edges and corners. Slow.</summary>
    DistanceFull = 5,

    /// <summary>Detects inversion artifacts only, by range testing.</summary>
    EdgeFast = 6,

    /// <summary>Detects inversion artifacts only, by exact distance evaluation.</summary>
    EdgeFull = 7,
}

/// <summary>Vertical orientation of pixel rows and of glyph and font metrics.</summary>
public enum YOrigin
{
    /// <summary>Y points up; pixel row 0 is the bottom row.</summary>
    Bottom = 0,

    /// <summary>Y points down; pixel row 0 is the top row.</summary>
    Top = 1,
}

/// <summary>How <see cref="GlyphSelection.Ids"/> are interpreted.</summary>
public enum GlyphSelectionKind
{
    /// <summary>Printable ASCII, U+0020 to U+007E.</summary>
    Ascii = 0,

    /// <summary>Unicode codepoints.</summary>
    Codepoints = 1,

    /// <summary>Glyph indices.</summary>
    GlyphIndices = 2,

    /// <summary>Every glyph in the font.</summary>
    AllGlyphs = 3,
}

/// <summary>Status codes reported by the native library.</summary>
public enum MsdfAtlasStatus
{
    /// <summary>The call succeeded.</summary>
    Ok = 0,

    /// <summary>An argument or option value is invalid.</summary>
    InvalidArgument = 1,

    /// <summary>The native library expects a different options layout than this assembly.</summary>
    StructSize = 2,

    /// <summary>The native library implements a different ABI version than this assembly.</summary>
    AbiMismatch = 3,

    /// <summary>The font data could not be parsed.</summary>
    FontLoad = 4,

    /// <summary>None of the selected glyphs exist in the font.</summary>
    NoGlyphs = 5,

    /// <summary>The glyphs could not be packed into an atlas.</summary>
    PackFailed = 6,

    /// <summary>Some glyphs did not fit; <see cref="MsdfAtlasException.Detail"/> holds how many.</summary>
    PackDidNotFit = 7,

    /// <summary>The atlas dimensions could not be determined.</summary>
    AtlasSize = 8,

    /// <summary>The image could not be encoded.</summary>
    EncodeFailed = 9,

    /// <summary>The output file could not be written.</summary>
    Io = 10,

    /// <summary>The native library ran out of memory.</summary>
    OutOfMemory = 11,

    /// <summary>The native library hit an unexpected error.</summary>
    Internal = 12,
}
