using System.CommandLine;
using XGDTool.Lib.Image;
using XGDTool.Lib.Converter;
using XGDTool.Lib.Util;

namespace XGDTool.CLI;

public class Program
{
    private class ParsedOptions : OutputOptions
    {
        public required string[] InputPaths;
    }

    private Commands Commands = new();

    public async Task<int> Run(string[] args)
    {
        Console.WriteLine("XGDTool - Xbox Game Disc Tool");

        Commands = new();
        Commands.Extract.SetAction(HandleExtract);
        Commands.Xiso.SetAction(HandleXiso);
        Commands.God.SetAction(HandleGod);
        Commands.Cci.SetAction(HandleCci);
        Commands.Cso.SetAction(HandleCso);
        Commands.Zar.SetAction(HandleZar);
        Commands.AutoXbox.SetAction(HandleAutoXbox);
        Commands.AutoXbox360.SetAction(HandleAutoXbox360);
        Commands.AutoXemu.SetAction(HandleAutoXemu);
        Commands.AutoXenia.SetAction(HandleAutoXenia);

        RootCommand rootCmd = new("XGDTool - A tool for working with Xbox game discs.")
        {
            Commands.Extract,
            Commands.Xiso,
            Commands.God,
            Commands.Cci,
            Commands.Cso,
            Commands.Zar,
            Commands.AutoXbox,
            Commands.AutoXbox360,
            Commands.AutoXemu,
            Commands.AutoXenia
        };

        return await rootCmd.Parse(args).InvokeAsync();
    }

    private static async Task ProcessOptions(ParsedOptions options)
    {
        var startTime = DateTime.Now;
        var entries = new List<InputEntry>();
        try
        {
            entries = InputHelper.GenerateEntries(options.InputPaths);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return;
        }

        if (entries.Count == 0)
        {
            Console.WriteLine("No valid input files found.");
            return;
        }

        var consoleLock = new object();

        static void PrintProgress(double progress, DateTime stageStartTime)
        {
            const int barWidth = 50;
            progress = Math.Clamp(progress, 0, 1);
            int filled = (int)(progress * barWidth);
            var elapsed = DateTime.Now - stageStartTime;

            string bar = new string('#', filled) + new string('-', barWidth - filled);

            Console.Write($"\r[{bar}] {(int)(progress * 100),3}% ({elapsed:mm\\:ss})");
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            Console.WriteLine("\nTask " + (i + 1) + " of " + entries.Count);
            Console.WriteLine("Processing files: ");

            foreach (var path in entry.InputPaths)
                Console.WriteLine("  " + path);

            Console.WriteLine(
                "Output format: " + 
                EnumExt.GetDescription(options.OutputFormat));

            var entryStartTime = DateTime.Now;
            var prevStage = Stage.Idle;
            var prevPercent = 0.0;
            var stageStartTime = entryStartTime;
            var reporter = new Progress<Progress>(p =>
            {
                if (p.Stage != prevStage)
                {
                    lock (consoleLock)
                    {
                        Console.Out.Flush();

                        var stageName = EnumExt.GetDescription(p.Stage);

                        if (prevStage != Stage.Idle)
                        {
                            PrintProgress(1, stageStartTime);
                            Console.WriteLine();
                        }

                        stageStartTime = DateTime.Now;
                        Console.WriteLine(stageName + "...");
                        prevStage = p.Stage;
                        prevPercent = p.Percent;
                        PrintProgress(p.Percent, stageStartTime);
                    }
                }
                else if (p.Current >= p.Total || 
                         (p.Percent - prevPercent) >= 0.01 || 
                         (DateTime.Now - stageStartTime) > TimeSpan.FromSeconds(1))
                {
                    lock (consoleLock)
                    {
                        Console.Out.Flush();
                        prevPercent = p.Percent;
                        PrintProgress(p.Percent, stageStartTime);
                    }
                }
            });

            try
            {
                var paths = await Process.ConvertEntry(entry, options, reporter);
                var elapsed = DateTime.Now - entryStartTime;

                Console.WriteLine($"\nTask completed ({elapsed:hh\\:mm\\:ss}), output files:");

                foreach (var path in paths)
                    Console.WriteLine("  " + path);
            }
            catch (Exception ex)
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nError: " + ex.Message + "\n");
                Console.ForegroundColor = color;
            }
        }

        var totalElapsed = DateTime.Now - startTime;
        Console.WriteLine($"\nAll tasks completed, total elapsed time: ({totalElapsed:hh\\:mm\\:ss})");
    }

    private async Task HandleExtract(ParseResult results) =>
        await ProcessOptions(ParseOptions(Format.Extract, results));

    private async Task HandleXiso(ParseResult results) =>
        await ProcessOptions(ParseOptions(Format.XISO, results));

    private async Task HandleGod(ParseResult results) =>
        await ProcessOptions(ParseOptions(Format.GOD, results));

    private async Task HandleCci(ParseResult results) =>
        await ProcessOptions(ParseOptions(Format.CCI, results));

    private async Task HandleCso(ParseResult results) =>
        await ProcessOptions(ParseOptions(Format.CSO, results));

    private async Task HandleZar(ParseResult results) =>
        await ProcessOptions(ParseOptions(Format.ZAR, results));

    private async Task HandleAutoXbox(ParseResult results)
    {
        await ProcessOptions(new ParsedOptions
        {
            InputPaths = results.GetRequiredValue(Commands.Options.Input),
            OutputDirectory = results.GetValue(Commands.Options.Output) ?? Environment.CurrentDirectory,
            OutputFormat = Format.Extract,
            WriterType = IWriterType.Extract,
            Scrub = null,
            Split = null,
            AttachXbe = null,
            RenameXbe = true,
            RenameTo = null,
            AllowedMediaPatch = true,
            SkipSystemUpdate = null,
            IconPath = null
        });
    }

    private async Task HandleAutoXbox360(ParseResult results)
    {
        await ProcessOptions(new ParsedOptions
        {
            InputPaths = results.GetRequiredValue(Commands.Options.Input),
            OutputDirectory = results.GetValue(Commands.Options.Output) ?? Environment.CurrentDirectory,
            OutputFormat = Format.GOD,
            WriterType = IWriterType.Rewrite,
            Scrub = true,
            Split = null,
            AttachXbe = null,
            RenameXbe = null,
            RenameTo = null,
            AllowedMediaPatch = null,
            SkipSystemUpdate = true,
            IconPath = null
        });
    }

    private async Task HandleAutoXemu(ParseResult results)
    {
        await ProcessOptions(new ParsedOptions
        {
            InputPaths = results.GetRequiredValue(Commands.Options.Input),
            OutputDirectory = results.GetValue(Commands.Options.Output) ?? Environment.CurrentDirectory,
            OutputFormat = Format.XISO,
            WriterType = IWriterType.Reauthor,
            Scrub = null,
            Split = null,
            AttachXbe = null,
            RenameXbe = null,
            RenameTo = null,
            AllowedMediaPatch = true,
            SkipSystemUpdate = null,
            IconPath = null
        });
    }

    private async Task HandleAutoXenia(ParseResult results)
    {
        await ProcessOptions(new ParsedOptions
        {
            InputPaths = results.GetRequiredValue(Commands.Options.Input),
            OutputDirectory = results.GetValue(Commands.Options.Output) ?? Environment.CurrentDirectory,
            OutputFormat = Format.ZAR,
            WriterType = IWriterType.Zar,
            Scrub = null,
            Split = null,
            AttachXbe = null,
            RenameXbe = true,
            RenameTo = null,
            AllowedMediaPatch = true,
            SkipSystemUpdate = true,
            IconPath = null
        });
    }

    private ParsedOptions ParseOptions(Format format, ParseResult results)
    {
        var writerType = format switch
        {
            Format.Extract => IWriterType.Extract,
            Format.ZAR => IWriterType.Zar,
            _ => (results.GetValue(Commands.Options.Reauthor) == true)
                ? IWriterType.Reauthor
                : IWriterType.Rewrite
        };

        return new ParsedOptions()
        {
            InputPaths = results.GetRequiredValue(Commands.Options.Input),
            OutputDirectory = results.GetValue(Commands.Options.Output) ?? Environment.CurrentDirectory,
            OutputFormat = format,
            WriterType = writerType,
            Scrub = results.GetValue(Commands.Options.Scrub),
            Split = results.GetValue(Commands.Options.Split),
            AttachXbe = results.GetValue(Commands.Options.GenerateXbe),
            RenameXbe = !string.IsNullOrEmpty(results.GetValue(Commands.Options.Rename)),
            RenameTo = results.GetValue(Commands.Options.Rename),
            AllowedMediaPatch = results.GetValue(Commands.Options.AllowedMedia),
            SkipSystemUpdate = results.GetValue(Commands.Options.SkipSystemUpdate),
            IconPath = results.GetValue(Commands.Options.Icon)
        };
    }
}
