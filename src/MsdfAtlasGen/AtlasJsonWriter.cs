using System.Text.Json;

namespace MsdfAtlasGen;

internal static class AtlasJsonWriter
{
    public static void Write(Utf8JsonWriter writer, AtlasResult result)
    {
        var top = result.YOrigin == YOrigin.Top;

        writer.WriteStartObject();

        writer.WriteStartObject("atlas");
        writer.WriteString("type", TypeName(result.ImageType));
        if (result.ImageType is ImageType.Sdf or ImageType.Psdf or ImageType.Msdf or ImageType.Mtsdf)
        {
            writer.WriteNumber("distanceRange", result.Atlas.DistanceRange);
            writer.WriteNumber("distanceRangeMiddle", result.Atlas.DistanceRangeMiddle);
        }
        writer.WriteNumber("size", result.Atlas.Scale);
        writer.WriteNumber("width", result.Width);
        writer.WriteNumber("height", result.Height);
        writer.WriteString("yOrigin", top ? "top" : "bottom");
        writer.WriteEndObject();

        writer.WriteStartObject("metrics");
        writer.WriteNumber("emSize", result.Font.EmSize);
        writer.WriteNumber("lineHeight", result.Font.LineHeight);
        writer.WriteNumber("ascender", result.Font.Ascender);
        writer.WriteNumber("descender", result.Font.Descender);
        writer.WriteNumber("underlineY", result.Font.UnderlineY);
        writer.WriteNumber("underlineThickness", result.Font.UnderlineThickness);
        writer.WriteEndObject();

        writer.WriteStartArray("glyphs");
        foreach (var glyph in result.Glyphs)
        {
            writer.WriteStartObject();
            if (result.GlyphsByCodepoint)
                writer.WriteNumber("unicode", glyph.Codepoint);
            else
                writer.WriteNumber("index", glyph.GlyphIndex);
            writer.WriteNumber("advance", glyph.Advance);
            if (glyph.PlaneBounds is { } plane)
                WriteBounds(writer, "planeBounds", plane, top);
            if (glyph.AtlasBounds is { } atlas)
                WriteBounds(writer, "atlasBounds", atlas, top);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        if (result.KerningEnabled)
        {
            writer.WriteStartArray("kerning");
            foreach (var pair in result.Kerning)
            {
                if (result.GlyphsByCodepoint)
                {
                    if (pair.Codepoint1 == 0 || pair.Codepoint2 == 0)
                        continue;
                    writer.WriteStartObject();
                    writer.WriteNumber("unicode1", pair.Codepoint1);
                    writer.WriteNumber("unicode2", pair.Codepoint2);
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("index1", pair.GlyphIndex1);
                    writer.WriteNumber("index2", pair.GlyphIndex2);
                }
                writer.WriteNumber("advance", pair.Advance);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteBounds(Utf8JsonWriter writer, string name, GlyphBounds bounds, bool top)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("left", bounds.Left);
        if (top)
        {
            writer.WriteNumber("top", bounds.Top);
            writer.WriteNumber("right", bounds.Right);
            writer.WriteNumber("bottom", bounds.Bottom);
        }
        else
        {
            writer.WriteNumber("bottom", bounds.Bottom);
            writer.WriteNumber("right", bounds.Right);
            writer.WriteNumber("top", bounds.Top);
        }
        writer.WriteEndObject();
    }

    private static string TypeName(ImageType type) =>
        type switch
        {
            ImageType.HardMask => "hardmask",
            ImageType.SoftMask => "softmask",
            ImageType.Sdf => "sdf",
            ImageType.Psdf => "psdf",
            ImageType.Msdf => "msdf",
            ImageType.Mtsdf => "mtsdf",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
