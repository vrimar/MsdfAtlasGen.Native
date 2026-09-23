namespace MsdfAtlasGen;

/// <summary>Raised when the native library reports a failure.</summary>
public sealed class MsdfAtlasException : Exception
{
    internal MsdfAtlasException(MsdfAtlasStatus status, int detail, string message)
        : base(message)
    {
        Status = status;
        Detail = detail;
    }

    /// <summary>Native status code.</summary>
    public MsdfAtlasStatus Status { get; }

    /// <summary>
    /// Additional detail for the status: the number of glyphs left out for
    /// <see cref="MsdfAtlasStatus.PackDidNotFit"/>, otherwise 0.
    /// </summary>
    public int Detail { get; }

    internal static unsafe MsdfAtlasException FromStatus(int status, int detail)
    {
        var code = (MsdfAtlasStatus)status;
        var message = NativeMethods.ReadString(NativeMethods.ErrorString(status));
        if (code == MsdfAtlasStatus.PackDidNotFit)
            message = $"{message} {detail} glyph(s) were left out.";
        return new MsdfAtlasException(code, detail, $"{message} ({code})");
    }
}
