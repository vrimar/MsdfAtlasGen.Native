using Xunit;

namespace MsdfAtlasGen.Tests;

internal static class TestSupport
{
    private static readonly Lazy<string?> SkipReason = new(() =>
    {
        try
        {
            _ = MsdfAtlas.NativeVersion;
            return null;
        }
        catch (DllNotFoundException ex)
        {
            return ex.Message;
        }
    });

    private static readonly Lazy<byte[]> LazyFont = new(() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "test-font.ttf"))
    );

    public static byte[] Font => LazyFont.Value;

    public static void SkipIfNativeMissing()
    {
        var reason = SkipReason.Value;
        if (reason is null)
            return;
        if (Environment.GetEnvironmentVariable("MSDF_REQUIRE_NATIVE") is { Length: > 0 })
            Assert.Fail($"MSDF_REQUIRE_NATIVE is set but the native library is unavailable: {reason}");
        Skip.If(true, reason);
    }

    public static AtlasResult Generate(AtlasOptions? options = null) => MsdfAtlas.Generate(Font, options);

    public static ReadOnlySpan<byte> Row(AtlasResult result, int y) =>
        result.Pixels.Span.Slice(y * result.Width * result.Channels, result.Width * result.Channels);
}
