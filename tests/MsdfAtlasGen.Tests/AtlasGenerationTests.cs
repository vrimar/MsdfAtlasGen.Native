using Xunit;

namespace MsdfAtlasGen.Tests;

public sealed class AtlasGenerationTests
{
    [SkippableFact]
    public void NativeVersion_reports_the_bundled_upstream_versions()
    {
        TestSupport.SkipIfNativeMissing();

        var version = MsdfAtlas.NativeVersion;

        Assert.Equal(1u, version.AbiVersion);
        Assert.Equal("1.4.0", version.MsdfAtlasGen);
        Assert.False(string.IsNullOrEmpty(version.Msdfgen));
    }

    [SkippableFact]
    public void Default_options_generate_an_ascii_mtsdf_atlas()
    {
        TestSupport.SkipIfNativeMissing();

        var result = TestSupport.Generate();

        Assert.Equal(ImageType.Mtsdf, result.ImageType);
        Assert.Equal(YOrigin.Bottom, result.YOrigin);
        Assert.True(result.Width > 0 && result.Height > 0);
        Assert.Equal(result.Width, result.Height);
        Assert.Equal(0, result.Width % 4);
        Assert.Equal(4, result.Channels);
        Assert.Equal(result.Width * result.Height * 4, result.Pixels.Length);
        Assert.Contains(result.Pixels.ToArray(), b => b != 0);
        Assert.Equal(95, result.Glyphs.Count);
        Assert.Equal(95, result.Atlas.GlyphsRequested);
        Assert.Equal(95, result.Atlas.GlyphsLoaded);
        Assert.Equal(32, result.Atlas.Scale);
        Assert.Equal(2, result.Atlas.DistanceRange);
        Assert.Equal(0, result.Atlas.DistanceRangeMiddle);
        Assert.True(result.GlyphsByCodepoint);
    }

    [SkippableFact]
    public void Channels_follow_the_image_type()
    {
        TestSupport.SkipIfNativeMissing();

        foreach (var (type, channels) in new[] { (ImageType.HardMask, 1), (ImageType.SoftMask, 1), (ImageType.Sdf, 1), (ImageType.Psdf, 1), (ImageType.Msdf, 3), (ImageType.Mtsdf, 4) })
        {
            var result = TestSupport.Generate(new AtlasOptions { ImageType = type, Glyphs = GlyphSelection.FromString("Ag") });
            Assert.Equal(type, result.ImageType);
            Assert.Equal(channels, result.Channels);
            Assert.Equal(result.Width * result.Height * channels, result.Pixels.Length);
        }
    }

    [SkippableFact]
    public void Visible_glyphs_have_bounds_inside_the_atlas_and_whitespace_has_none()
    {
        TestSupport.SkipIfNativeMissing();

        var result = TestSupport.Generate();

        var a = result.Glyphs.Single(g => g.Codepoint == 'A');
        Assert.False(a.IsWhitespace);
        Assert.True(a.Advance > 0);
        var plane = Assert.NotNull(a.PlaneBounds);
        Assert.True(plane.Right > plane.Left);
        Assert.True(plane.Top > plane.Bottom);
        var atlas = Assert.NotNull(a.AtlasBounds);
        Assert.InRange(atlas.Left, 0, result.Width);
        Assert.InRange(atlas.Right, atlas.Left, result.Width);
        Assert.InRange(atlas.Bottom, 0, result.Height);
        Assert.InRange(atlas.Top, atlas.Bottom, result.Height);

        var space = result.Glyphs.Single(g => g.Codepoint == ' ');
        Assert.True(space.IsWhitespace);
        Assert.Null(space.PlaneBounds);
        Assert.Null(space.AtlasBounds);
        Assert.True(space.Advance > 0);
    }

    [SkippableFact]
    public void Kerning_pairs_reference_loaded_glyphs()
    {
        TestSupport.SkipIfNativeMissing();

        var result = TestSupport.Generate();

        Assert.NotEmpty(result.Kerning);
        var byIndex = result.Glyphs.ToDictionary(g => g.GlyphIndex);
        foreach (var pair in result.Kerning)
        {
            Assert.NotEqual(0, pair.Advance);
            Assert.Equal(byIndex[pair.GlyphIndex1].Codepoint, pair.Codepoint1);
            Assert.Equal(byIndex[pair.GlyphIndex2].Codepoint, pair.Codepoint2);
        }

        Assert.Empty(TestSupport.Generate(new AtlasOptions { Kerning = false }).Kerning);
    }

    [SkippableFact]
    public void Font_metrics_are_em_normalised_and_signed_per_y_origin()
    {
        TestSupport.SkipIfNativeMissing();

        var bottom = TestSupport.Generate().Font;
        var top = TestSupport.Generate(new AtlasOptions { YOrigin = YOrigin.Top }).Font;

        Assert.Equal(1, bottom.EmSize);
        Assert.True(bottom.Ascender > 0);
        Assert.True(bottom.Descender < 0);
        Assert.True(bottom.LineHeight > 0);
        Assert.Equal(-bottom.Ascender, top.Ascender);
        Assert.Equal(-bottom.Descender, top.Descender);
        Assert.Equal(-bottom.UnderlineY, top.UnderlineY);
        Assert.Equal(bottom.LineHeight, top.LineHeight);
    }

    [SkippableFact]
    public void Top_origin_reverses_rows_and_transforms_bounds()
    {
        TestSupport.SkipIfNativeMissing();

        var bottom = TestSupport.Generate();
        var top = TestSupport.Generate(new AtlasOptions { YOrigin = YOrigin.Top });

        Assert.Equal(bottom.Width, top.Width);
        Assert.Equal(bottom.Height, top.Height);
        for (var y = 0; y < bottom.Height; y++)
            Assert.True(TestSupport.Row(bottom, y).SequenceEqual(TestSupport.Row(top, bottom.Height - 1 - y)), $"Row {y} is not mirrored.");

        var a = bottom.Glyphs.Single(g => g.Codepoint == 'A');
        var aTop = top.Glyphs.Single(g => g.Codepoint == 'A');
        Assert.Equal(-a.PlaneBounds!.Value.Top, aTop.PlaneBounds!.Value.Top);
        Assert.Equal(bottom.Height - a.AtlasBounds!.Value.Top, aTop.AtlasBounds!.Value.Top);
        Assert.Equal(bottom.Height - a.AtlasBounds!.Value.Bottom, aTop.AtlasBounds!.Value.Bottom);

        Assert.Equal(bottom.EncodePng(), top.EncodePng());
    }

    [SkippableFact]
    public void EncodePng_produces_a_png_with_the_atlas_dimensions()
    {
        TestSupport.SkipIfNativeMissing();

        var result = TestSupport.Generate();
        var png = result.EncodePng();

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        Assert.Equal(result.Width, ReadBigEndian(png, 16));
        Assert.Equal(result.Height, ReadBigEndian(png, 20));
    }

    [SkippableFact]
    public void Thread_count_does_not_change_the_output()
    {
        TestSupport.SkipIfNativeMissing();

        foreach (var coloring in new[] { EdgeColoring.InkTrap, EdgeColoring.ByDistance })
        {
            var single = TestSupport.Generate(new AtlasOptions { EdgeColoring = coloring, ColoringSeed = 7, ThreadCount = 1 });
            var multi = TestSupport.Generate(new AtlasOptions { EdgeColoring = coloring, ColoringSeed = 7, ThreadCount = 8 });
            Assert.True(single.Pixels.Span.SequenceEqual(multi.Pixels.Span), $"{coloring} output differs across thread counts.");
        }
    }

    [SkippableFact]
    public void Codepoint_selection_loads_only_the_requested_glyphs()
    {
        TestSupport.SkipIfNativeMissing();

        var result = TestSupport.Generate(new AtlasOptions { Glyphs = GlyphSelection.FromString("ABBA") });

        Assert.Equal(new uint[] { 'A', 'B' }, result.Glyphs.Select(g => g.Codepoint));
        Assert.Equal(2, result.Atlas.GlyphsRequested);
        Assert.True(result.GlyphsByCodepoint);
    }

    [SkippableFact]
    public void Glyph_index_selection_reports_indices_without_codepoints()
    {
        TestSupport.SkipIfNativeMissing();

        var indexOfA = (uint)TestSupport.Generate(new AtlasOptions { Glyphs = GlyphSelection.FromString("A") }).Glyphs[0].GlyphIndex;

        var result = TestSupport.Generate(new AtlasOptions { Glyphs = GlyphSelection.GlyphIndices(indexOfA) });

        var glyph = Assert.Single(result.Glyphs);
        Assert.Equal((int)indexOfA, glyph.GlyphIndex);
        Assert.Equal(0u, glyph.Codepoint);
        Assert.False(result.GlyphsByCodepoint);
    }

    [SkippableFact]
    public void Invalid_font_data_throws_FontLoad()
    {
        TestSupport.SkipIfNativeMissing();

        var ex = Assert.Throws<MsdfAtlasException>(() => MsdfAtlas.Generate(Enumerable.Repeat((byte)0x42, 64).ToArray()));

        Assert.Equal(MsdfAtlasStatus.FontLoad, ex.Status);
    }

    [SkippableFact]
    public void Atlas_too_small_throws_PackDidNotFit_with_the_left_out_count()
    {
        TestSupport.SkipIfNativeMissing();

        var ex = Assert.Throws<MsdfAtlasException>(() => TestSupport.Generate(new AtlasOptions { FixedWidth = 16, FixedHeight = 16 }));

        Assert.Equal(MsdfAtlasStatus.PackDidNotFit, ex.Status);
        Assert.True(ex.Detail > 0);
    }

    [SkippableFact]
    public void Codepoints_missing_from_the_font_throw_NoGlyphs()
    {
        TestSupport.SkipIfNativeMissing();

        var ex = Assert.Throws<MsdfAtlasException>(() => TestSupport.Generate(new AtlasOptions { Glyphs = GlyphSelection.Codepoints(0x10FFFD) }));

        Assert.Equal(MsdfAtlasStatus.NoGlyphs, ex.Status);
    }

    [SkippableFact]
    public void Out_of_range_options_throw_InvalidArgument()
    {
        TestSupport.SkipIfNativeMissing();

        var ex = Assert.Throws<MsdfAtlasException>(() => TestSupport.Generate(new AtlasOptions { ImageType = (ImageType)42 }));

        Assert.Equal(MsdfAtlasStatus.InvalidArgument, ex.Status);
    }

    [Fact]
    public void Empty_font_data_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MsdfAtlas.Generate(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Empty_glyph_selections_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => GlyphSelection.Codepoints());
        Assert.Throws<ArgumentException>(() => GlyphSelection.GlyphIndices());
        Assert.Throws<ArgumentException>(() => GlyphSelection.FromString(""));
    }

    private static int ReadBigEndian(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
}
