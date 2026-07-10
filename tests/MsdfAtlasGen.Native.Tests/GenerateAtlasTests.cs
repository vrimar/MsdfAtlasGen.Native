using System.Diagnostics;
using Xunit;

namespace MsdfAtlasGen.Native.Tests;

public sealed class GenerateAtlasTests
{
    private static string? ResolveToolOrSkipReason(out string toolPath)
    {
        toolPath = string.Empty;
        try
        {
            toolPath = MsdfAtlasGenTool.ExecutablePath;
            return null;
        }
        catch (FileNotFoundException ex)
        {
            // No native binary staged into the output (expected in a managed-only local build; CI
            // copies the freshly built binary into the test output before running).
            return ex.Message;
        }
        catch (PlatformNotSupportedException ex)
        {
            return ex.Message;
        }
    }

    [SkippableFact]
    public void ExecutablePath_resolves_and_points_at_an_existing_file()
    {
        var skip = ResolveToolOrSkipReason(out var tool);
        Skip.If(skip is not null, skip ?? "");
        Assert.True(File.Exists(tool), $"Resolved tool path does not exist: {tool}");
    }

    [SkippableFact]
    public void Generates_a_valid_mtsdf_atlas_from_the_bundled_font()
    {
        var skip = ResolveToolOrSkipReason(out var tool);
        Skip.If(skip is not null, skip ?? "");

        var font = Path.Combine(AppContext.BaseDirectory, "assets", "test-font.ttf");
        Assert.True(File.Exists(font), $"Bundled test font missing: {font}");

        var outDir = Path.Combine(Path.GetTempPath(), $"msdf-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        var json = Path.Combine(outDir, "atlas.json");
        var png = Path.Combine(outDir, "atlas.png");

        try
        {
            var psi = new ProcessStartInfo(tool)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (
                var arg in new[]
                {
                    "-font", font,
                    "-charset", InlineAsciiCharset(outDir),
                    "-type", "mtsdf",
                    "-format", "png",
                    "-size", "32",
                    "-pxrange", "6",
                    "-yorigin", "top",
                    "-json", json,
                    "-imageout", png,
                }
            )
                psi.ArgumentList.Add(arg);

            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            Assert.True(
                proc.ExitCode == 0,
                $"msdf-atlas-gen exited {proc.ExitCode}.\nstdout: {stdout}\nstderr: {stderr}"
            );
            Assert.True(new FileInfo(json).Length > 0, "atlas.json is empty");
            Assert.True(new FileInfo(png).Length > 0, "atlas.png is empty");
        }
        finally
        {
            try
            {
                Directory.Delete(outDir, recursive: true);
            }
            catch
            {
                /* best effort */
            }
        }
    }

    // ASCII printable range 0x20..0x7E — written to a charset file msdf-atlas-gen reads.
    private static string InlineAsciiCharset(string dir)
    {
        var path = Path.Combine(dir, "charset.txt");
        File.WriteAllText(path, "[0x20, 0x7E]");
        return path;
    }
}
