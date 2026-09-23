using System.Text.Json;
using Xunit;

namespace MsdfAtlasGen.Tests;

public sealed class AtlasJsonTests
{
    [SkippableFact]
    public void ExportJson_writes_the_atlas_layout()
    {
        TestSupport.SkipIfNativeMissing();

        var result = TestSupport.Generate();
        var path = Path.Combine(Path.GetTempPath(), $"msdf-atlas-{Guid.NewGuid():N}.json");
        try
        {
            result.ExportJson(path);

            using var json = JsonDocument.Parse(File.ReadAllBytes(path));
            var atlas = json.RootElement.GetProperty("atlas");
            Assert.Equal("mtsdf", atlas.GetProperty("type").GetString());
            Assert.Equal(result.Width, atlas.GetProperty("width").GetInt32());
            Assert.Equal(result.Height, atlas.GetProperty("height").GetInt32());
            Assert.Equal(32, atlas.GetProperty("size").GetDouble());
            Assert.Equal(2, atlas.GetProperty("distanceRange").GetDouble());
            Assert.Equal("bottom", atlas.GetProperty("yOrigin").GetString());
            Assert.Equal(95, json.RootElement.GetProperty("glyphs").GetArrayLength());
            Assert.NotEqual(0, json.RootElement.GetProperty("kerning").GetArrayLength());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableTheory]
    [InlineData(YOrigin.Bottom, GlyphSelectionKind.Ascii)]
    [InlineData(YOrigin.Top, GlyphSelectionKind.Ascii)]
    [InlineData(YOrigin.Bottom, GlyphSelectionKind.GlyphIndices)]
    public unsafe void Managed_json_matches_the_native_exporter(YOrigin yOrigin, GlyphSelectionKind selection)
    {
        TestSupport.SkipIfNativeMissing();

        var glyphs = selection == GlyphSelectionKind.GlyphIndices ? GlyphSelection.GlyphIndices(0, 1, 2, 36, 37, 38, 68, 69) : GlyphSelection.Ascii;
        var options = new AtlasOptions { YOrigin = yOrigin, Glyphs = glyphs };
        var managed = TestSupport.Generate(options).ToJson();

        var path = Path.Combine(Path.GetTempPath(), $"msdf-atlas-native-{Guid.NewGuid():N}.json");
        var font = TestSupport.Font;
        try
        {
            fixed (byte* fontData = font)
            fixed (uint* ids = glyphs.IdArray)
            {
                var native = options.ToNative(ids);
                nint result = 0;
                Assert.Equal(0, NativeMethods.Generate(&native, fontData, (nuint)font.Length, &result));
                try
                {
                    Assert.Equal(0, NativeMethods.ResultExportJson(result, path));
                }
                finally
                {
                    NativeMethods.ResultFree(result);
                }
            }

            using var expected = JsonDocument.Parse(File.ReadAllBytes(path));
            using var actual = JsonDocument.Parse(managed);
            AssertJsonEquivalent(expected.RootElement, actual.RootElement, "$");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(expected.ValueKind == actual.ValueKind, $"{path}: {expected.ValueKind} != {actual.ValueKind}");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = expected.EnumerateObject().Select(p => p.Name).Order().ToArray();
                var actualNames = actual.EnumerateObject().Select(p => p.Name).Order().ToArray();
                Assert.True(expectedNames.SequenceEqual(actualNames), $"{path}: [{string.Join(",", expectedNames)}] != [{string.Join(",", actualNames)}]");
                foreach (var property in expected.EnumerateObject())
                    AssertJsonEquivalent(property.Value, actual.GetProperty(property.Name), $"{path}.{property.Name}");
                break;
            case JsonValueKind.Array:
                Assert.True(expected.GetArrayLength() == actual.GetArrayLength(), $"{path}: length {expected.GetArrayLength()} != {actual.GetArrayLength()}");
                for (var i = 0; i < expected.GetArrayLength(); i++)
                    AssertJsonEquivalent(expected[i], actual[i], $"{path}[{i}]");
                break;
            case JsonValueKind.Number:
                var e = expected.GetDouble();
                var a = actual.GetDouble();
                Assert.True(Math.Abs(e - a) <= 1e-9 * Math.Max(1, Math.Abs(e)), $"{path}: {e} != {a}");
                break;
            default:
                Assert.True(expected.ToString() == actual.ToString(), $"{path}: {expected} != {actual}");
                break;
        }
    }
}
