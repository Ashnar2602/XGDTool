using Xunit;
using XGDTool.Lib.Converter;
using XGDTool.Lib.Image;

namespace XGDTool.Lib.Tests;

public class ConvertTests : IDisposable
{
    // Path to the extracted Xbox app used as conversion input
    private static readonly string SourceDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources", "sdl_image"));

    // Temp directory cleaned up after each test
    private readonly string TempDir = Path.Combine(Path.GetTempPath(), "XGDToolTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(TempDir))
            Directory.Delete(TempDir, recursive: true);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static InputEntry ExtractInput() => new()
    {
        InputPaths = [SourceDir],
        InputFormat = Format.Extract
    };

    private static OutputOptions MakeOptions(Format format, IWriterType writerType, string outputDir) => new()
    {
        OutputFormat = format,
        WriterType = writerType,
        OutputDirectory = outputDir,
        Scrub = false,
        Split = false,
        RenameXbe = false,
        AllowedMediaPatch = false,
        SkipSystemUpdate = false
    };

    private static void AssertOutputFilesValid(IReadOnlyList<string> outputPaths)
    {
        Assert.NotEmpty(outputPaths);
        foreach (var path in outputPaths)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                Assert.True(files.Length > 0, $"Expected output directory to be non-empty: {path}");
                Assert.True(files.Any(f => new FileInfo(f).Length > 0), $"Expected at least one non-empty file in: {path}");
            }
            else
            {
                Assert.True(File.Exists(path), $"Expected output file to exist: {path}");
                Assert.True(new FileInfo(path).Length > 0, $"Expected output file to be non-empty: {path}");
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Single-format output tests (Extract → <format>)
    // ---------------------------------------------------------------------------

    public static IEnumerable<object[]> SingleFormatCases() =>
    [
        [Format.XISO,  IWriterType.Reauthor],
        [Format.CCI,   IWriterType.Reauthor],
        [Format.CSO,   IWriterType.Reauthor],
        [Format.GOD,   IWriterType.Reauthor],
        [Format.ZAR,   IWriterType.Zar],
    ];

    [Theory]
    [MemberData(nameof(SingleFormatCases))]
    public async Task ConvertFromExtract_ProducesValidOutput(Format outputFormat, IWriterType writerType)
    {
        var outDir = Path.Combine(TempDir, outputFormat.ToString());
        var options = MakeOptions(outputFormat, writerType, outDir);

        var outputPaths = await Process.ConvertEntry(ExtractInput(), options);

        AssertOutputFilesValid(outputPaths);
    }

    // ---------------------------------------------------------------------------
    // Round-trip tests (Extract → <format> → Extract, verify original files survive)
    // ---------------------------------------------------------------------------

    public static IEnumerable<object[]> RoundTripCases() =>
    [
        [Format.XISO, IWriterType.Reauthor],
        [Format.CCI,  IWriterType.Reauthor],
        [Format.CSO,  IWriterType.Reauthor],
        [Format.ZAR,  IWriterType.Zar],
    ];

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public async Task RoundTrip_ExtractToFormatAndBack_PreservesFiles(Format intermediateFormat, IWriterType writerType)
    {
        // Step 1: Extract → intermediate format
        var step1Dir = Path.Combine(TempDir, intermediateFormat.ToString());
        var step1Options = MakeOptions(intermediateFormat, writerType, step1Dir);
        var step1Paths = await Process.ConvertEntry(ExtractInput(), step1Options);
        AssertOutputFilesValid(step1Paths);

        // Step 2: intermediate format → Extract
        var step2Dir = Path.Combine(TempDir, intermediateFormat + "_back");
        var step2Entry = new InputEntry
        {
            InputPaths = [step1Paths[0]],
            InputFormat = intermediateFormat
        };
        var step2Options = MakeOptions(Format.Extract, IWriterType.Extract, step2Dir);
        var step2Paths = await Process.ConvertEntry(step2Entry, step2Options);

        Assert.NotEmpty(step2Paths);

        // The writer returns the title folder path; walk it to find expected files
        var allExtracted = Directory.GetFiles(step2Dir, "*", SearchOption.AllDirectories);
        Assert.Contains(allExtracted, f => Path.GetFileName(f).Equals("default.xbe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allExtracted, f => Path.GetFileName(f).Equals("testimg.jpg", StringComparison.OrdinalIgnoreCase));
    }
}
