using System.Runtime.InteropServices;

namespace MsdfAtlasGen;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeOptions
{
    public uint StructSize;
    public uint AbiVersion;

    public int GlyphSelection;
    public uint IdCount;
    public uint* Ids;

    public int ImageType;
    public int YOrigin;

    public double EmSize;
    public double MinEmSize;

    public int RangeUnits;
    public int Reserved0;
    public double RangeLower;
    public double RangeUpper;

    public int DimensionsConstraint;
    public int Width;
    public int Height;
    public int Spacing;

    public double MiterLimit;
    public int PxAlignOriginX;
    public int PxAlignOriginY;

    public int InnerPaddingUnits;
    public int OuterPaddingUnits;
    public double InnerPaddingLeft;
    public double InnerPaddingBottom;
    public double InnerPaddingRight;
    public double InnerPaddingTop;
    public double OuterPaddingLeft;
    public double OuterPaddingBottom;
    public double OuterPaddingRight;
    public double OuterPaddingTop;

    public int EdgeColoring;
    public int Reserved1;
    public double AngleThreshold;
    public ulong ColoringSeed;

    public int ErrorCorrection;
    public int OverlapSupport;
    public int ScanlinePass;
    public int Kerning;
    public int ThreadCount;
    public int Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeFontMetrics
{
    public double EmSize;
    public double AscenderY;
    public double DescenderY;
    public double LineHeight;
    public double UnderlineY;
    public double UnderlineThickness;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeGlyph
{
    public int GlyphIndex;
    public uint Codepoint;
    public int IsWhitespace;
    public int HasPlaneBounds;
    public int HasAtlasBounds;
    public int Reserved0;
    public double Advance;
    public double PlaneLeft;
    public double PlaneBottom;
    public double PlaneRight;
    public double PlaneTop;
    public double AtlasLeft;
    public double AtlasBottom;
    public double AtlasRight;
    public double AtlasTop;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeKerning
{
    public int GlyphIndex1;
    public int GlyphIndex2;
    public uint Codepoint1;
    public uint Codepoint2;
    public double Advance;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeAtlasMetrics
{
    public double Scale;
    public double PxRangeLower;
    public double PxRangeUpper;
    public int Width;
    public int Height;
    public int Channels;
    public int ImageType;
    public int YOrigin;
    public int GlyphsLoaded;
    public int GlyphsRequested;
    public int Reserved0;
}
