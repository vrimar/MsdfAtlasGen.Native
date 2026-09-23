using Xunit;

namespace MsdfAtlasGen.Tests;

public sealed unsafe class NativeInteropTests
{
    [SkippableFact]
    public void Options_struct_layout_matches_the_native_library()
    {
        TestSupport.SkipIfNativeMissing();

        Assert.Equal(224, sizeof(NativeOptions));
        Assert.Equal((uint)sizeof(NativeOptions), NativeMethods.OptionsSize());
        Assert.Equal(96, sizeof(NativeGlyph));
        Assert.Equal(24, sizeof(NativeKerning));
        Assert.Equal(48, sizeof(NativeFontMetrics));
        Assert.Equal(56, sizeof(NativeAtlasMetrics));
    }

    [SkippableFact]
    public void Managed_defaults_match_the_native_defaults()
    {
        TestSupport.SkipIfNativeMissing();

        NativeOptions native;
        NativeMethods.OptionsInit(&native);
        var managed = new AtlasOptions().ToNative(null);

        var nativeBytes = new ReadOnlySpan<byte>(&native, sizeof(NativeOptions));
        var managedBytes = new ReadOnlySpan<byte>(&managed, sizeof(NativeOptions));
        Assert.Equal(nativeBytes.ToArray(), managedBytes.ToArray());
    }

    [SkippableFact]
    public void Struct_size_and_abi_version_are_validated()
    {
        TestSupport.SkipIfNativeMissing();

        var font = TestSupport.Font;
        fixed (byte* fontData = font)
        {
            NativeOptions options;
            NativeMethods.OptionsInit(&options);
            options.StructSize -= 8;
            nint result = 1;
            Assert.Equal((int)MsdfAtlasStatus.StructSize, NativeMethods.Generate(&options, fontData, (nuint)font.Length, &result));
            Assert.Equal(0, result);

            NativeMethods.OptionsInit(&options);
            options.AbiVersion = 99;
            Assert.Equal((int)MsdfAtlasStatus.AbiMismatch, NativeMethods.Generate(&options, fontData, (nuint)font.Length, &result));
            Assert.Equal(0, result);
        }
    }

    [SkippableFact]
    public void Error_strings_cover_every_status()
    {
        TestSupport.SkipIfNativeMissing();

        foreach (var status in Enum.GetValues<MsdfAtlasStatus>())
            Assert.NotEqual("Unknown status.", NativeMethods.ReadString(NativeMethods.ErrorString((int)status)));
    }
}
