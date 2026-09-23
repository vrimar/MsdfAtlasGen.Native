using System.Diagnostics;
using MsdfAtlasGen;
using MsdfAtlasGen.Native;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: PackageSmoke <font.ttf>");
    return 2;
}

var fontPath = Path.GetFullPath(args[0]);

try
{
    CheckBindings(fontPath);
    CheckTool(fontPath);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAIL: {ex}");
    return 1;
}

static void CheckBindings(string fontPath)
{
    var version = MsdfAtlas.NativeVersion;
    var atlas = MsdfAtlas.Generate(File.ReadAllBytes(fontPath));

    Require(atlas.Width > 0 && atlas.Height > 0, $"atlas is {atlas.Width}x{atlas.Height}");
    Require(atlas.Atlas.GlyphsLoaded == 95, $"loaded {atlas.Atlas.GlyphsLoaded} of 95 ASCII glyphs");
    Require(IsPng(atlas.EncodePng()), "EncodePng did not return a PNG");
    Require(atlas.ToJson().Contains("\"glyphs\"", StringComparison.Ordinal), "ToJson has no glyphs");

    Console.WriteLine(
        $"MsdfAtlasGen: msdf-atlas-gen {version.MsdfAtlasGen}, msdfgen {version.Msdfgen}, "
            + $"{atlas.Width}x{atlas.Height} {atlas.ImageType}"
    );
}

static void CheckTool(string fontPath)
{
    var tool = MsdfAtlasGenTool.ExecutablePath;
    var outDir = Directory.CreateTempSubdirectory("msdf-smoke-");
    try
    {
        var png = Path.Combine(outDir.FullName, "atlas.png");
        var json = Path.Combine(outDir.FullName, "atlas.json");
        var psi = new ProcessStartInfo(tool)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { "-font", fontPath, "-type", "mtsdf", "-size", "32", "-imageout", png, "-json", json })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {tool}");
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Require(process.ExitCode == 0, $"{tool} exited {process.ExitCode}\n{stdout}{stderr.Result}");
        Require(IsPng(File.ReadAllBytes(png)), "msdf-atlas-gen did not write a PNG");
        Require(new FileInfo(json).Length > 0, "msdf-atlas-gen wrote an empty JSON layout");
    }
    finally
    {
        outDir.Delete(recursive: true);
    }

    Console.WriteLine($"MsdfAtlasGen.Native: {tool}");
}

static bool IsPng(ReadOnlySpan<byte> data)
{
    ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    return data.StartsWith(signature);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
