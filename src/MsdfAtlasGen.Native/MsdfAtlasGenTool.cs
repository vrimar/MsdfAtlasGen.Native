using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MsdfAtlasGen.Native;

/// <summary>
/// Locates the bundled <c>msdf-atlas-gen</c> executable for the current runtime. The package copies
/// the RID-appropriate binary into the application's output directory (via its
/// <c>buildTransitive</c> targets), and this type resolves that path and ensures the executable bit
/// on Unix. The caller supplies the atlas parameters (<c>-type</c>, <c>-size</c>, …) and drives the
/// process itself.
/// </summary>
public static class MsdfAtlasGenTool
{
    private static readonly Lazy<string> LazyPath = new(Resolve, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Absolute path to the bundled <c>msdf-atlas-gen[.exe]</c> for the current RID. Resolved and
    /// cached on first access; the executable bit is applied on Unix if missing.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">No binary is bundled for the current OS.</exception>
    /// <exception cref="FileNotFoundException">The binary was not copied into the output directory.</exception>
    public static string ExecutablePath => LazyPath.Value;

    private static string Resolve()
    {
        if (
            !OperatingSystem.IsWindows()
            && !OperatingSystem.IsLinux()
            && !OperatingSystem.IsMacOS()
        )
        {
            throw new PlatformNotSupportedException(
                $"No msdf-atlas-gen binary is bundled for {RuntimeInformation.OSDescription}."
            );
        }

        var exeName = OperatingSystem.IsWindows() ? "msdf-atlas-gen.exe" : "msdf-atlas-gen";
        var baseDir = AppContext.BaseDirectory;

        // The buildTransitive targets copies the binary to the output root; fall back to a
        // tools/<rid>/ layout in case a consumer stages the package tree directly.
        var candidates = new[]
        {
            Path.Combine(baseDir, exeName),
            Path.Combine(baseDir, "tools", RuntimeInformation.RuntimeIdentifier, exeName),
        };

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;

            if (!OperatingSystem.IsWindows())
                EnsureExecutable(candidate);

            return candidate;
        }

        throw new FileNotFoundException(
            $"Bundled msdf-atlas-gen executable '{exeName}' not found in the application output "
                + $"(looked in '{baseDir}'). Ensure the MsdfAtlasGen.Native package restored and its "
                + "buildTransitive targets ran.",
            candidates[0]
        );
    }

    [UnsupportedOSPlatform("windows")]
    private static void EnsureExecutable(string path)
    {
        try
        {
            var mode = File.GetUnixFileMode(path);
            var execBits =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((mode & execBits) == 0)
                File.SetUnixFileMode(path, mode | execBits);
        }
        catch
        {
            // Best effort — if this fails, starting the process will surface the real error.
        }
    }
}
