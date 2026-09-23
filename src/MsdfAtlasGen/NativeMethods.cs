using System.Runtime.InteropServices;

namespace MsdfAtlasGen;

internal static unsafe partial class NativeMethods
{
    internal const string LibraryName = "msdf_atlas_c";
    internal const uint AbiVersionExpected = 1;
    internal const int SpacingDefault = int.MinValue;

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_abi_version")]
    internal static partial uint AbiVersion();

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_upstream_version")]
    internal static partial byte* UpstreamVersion();

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_msdfgen_version")]
    internal static partial byte* MsdfgenVersion();

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_options_size")]
    internal static partial uint OptionsSize();

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_options_init")]
    internal static partial void OptionsInit(NativeOptions* options);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_error_string")]
    internal static partial byte* ErrorString(int status);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_last_error_detail")]
    internal static partial int LastErrorDetail();

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_generate")]
    internal static partial int Generate(NativeOptions* options, byte* fontData, nuint fontSize, nint* result);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_result_free")]
    internal static partial void ResultFree(nint result);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_result_image")]
    internal static partial byte* ResultImage(nint result, int* width, int* height, int* channels);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_result_atlas_metrics")]
    internal static partial NativeAtlasMetrics* ResultAtlasMetrics(nint result);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_result_font_metrics")]
    internal static partial NativeFontMetrics* ResultFontMetrics(nint result);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_result_glyphs")]
    internal static partial NativeGlyph* ResultGlyphs(nint result, uint* count);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_result_kerning")]
    internal static partial NativeKerning* ResultKerning(nint result, uint* count);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_result_export_json", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ResultExportJson(nint result, string utf8Path);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_encode_png")]
    internal static partial int EncodePng(byte* pixels, int width, int height, int channels, int yOrigin, byte** data, nuint* size);

    [LibraryImport(LibraryName, EntryPoint = "msdf_atlas_buffer_free")]
    internal static partial void BufferFree(void* buffer);

    internal static string ReadString(byte* value) => Marshal.PtrToStringUTF8((nint)value) ?? string.Empty;
}
