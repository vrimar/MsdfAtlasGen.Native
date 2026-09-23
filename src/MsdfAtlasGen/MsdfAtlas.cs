namespace MsdfAtlasGen;

/// <summary>
/// Generates MSDF/MTSDF font atlases in-process with the bundled native msdf-atlas-gen library.
/// </summary>
public static class MsdfAtlas
{
    private static readonly Lazy<NativeVersionInfo> LazyNativeVersion = new(
        LoadNative,
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>Versions of the loaded native library. Loads and validates the library on first access.</summary>
    /// <exception cref="DllNotFoundException">The native library for the current runtime is missing.</exception>
    /// <exception cref="MsdfAtlasException">The native library is incompatible with this assembly.</exception>
    public static NativeVersionInfo NativeVersion => LazyNativeVersion.Value;

    /// <summary>Generates an atlas from a TrueType or OpenType font. Safe to call concurrently.</summary>
    /// <param name="font">Font file contents.</param>
    /// <param name="options">Generation options; <see langword="null"/> uses the defaults.</param>
    /// <exception cref="ArgumentException"><paramref name="font"/> is empty.</exception>
    /// <exception cref="DllNotFoundException">The native library for the current runtime is missing.</exception>
    /// <exception cref="MsdfAtlasException">The native library reported a failure.</exception>
    public static unsafe AtlasResult Generate(ReadOnlySpan<byte> font, AtlasOptions? options = null)
    {
        if (font.IsEmpty)
            throw new ArgumentException("Font data is empty.", nameof(font));
        options ??= new AtlasOptions();
        _ = NativeVersion;

        fixed (byte* fontData = font)
        fixed (uint* ids = options.Glyphs.IdArray)
        {
            var native = options.ToNative(ids);
            nint result = 0;
            var status = NativeMethods.Generate(&native, fontData, (nuint)font.Length, &result);
            if (status != (int)MsdfAtlasStatus.Ok)
                throw MsdfAtlasException.FromStatus(status, NativeMethods.LastErrorDetail());
            try
            {
                return AtlasResult.FromNative(result, options);
            }
            finally
            {
                NativeMethods.ResultFree(result);
            }
        }
    }

    private static unsafe NativeVersionInfo LoadNative()
    {
        var abi = NativeMethods.AbiVersion();
        if (abi != NativeMethods.AbiVersionExpected)
        {
            throw new MsdfAtlasException(
                MsdfAtlasStatus.AbiMismatch,
                (int)abi,
                $"Native {NativeMethods.LibraryName} ABI version {abi} does not match the expected version {NativeMethods.AbiVersionExpected}."
            );
        }

        var optionsSize = NativeMethods.OptionsSize();
        if (optionsSize != (uint)sizeof(NativeOptions))
        {
            throw new MsdfAtlasException(
                MsdfAtlasStatus.StructSize,
                (int)optionsSize,
                $"Native {NativeMethods.LibraryName} options size {optionsSize} does not match the managed size {sizeof(NativeOptions)}."
            );
        }

        return new NativeVersionInfo(
            abi,
            NativeMethods.ReadString(NativeMethods.UpstreamVersion()),
            NativeMethods.ReadString(NativeMethods.MsdfgenVersion())
        );
    }
}
