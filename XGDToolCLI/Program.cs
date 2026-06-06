using System.CommandLine;
using System.ComponentModel;
using XGDToolLib.Image;
using XGDToolLib.Converter;
using XGDToolLib.Util;

namespace XGDToolCLI;

public class Program
{
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

        RootCommand rootCmd = new("XGDTool - A tool for working with Xbox game discs.")
        {
            Commands.Extract,
            Commands.Xiso,
            Commands.God,
            Commands.Cci,
            Commands.Cso,
            Commands.Zar
        };

        var parseResult = rootCmd.Parse(args);
        return await parseResult.InvokeAsync();
    }

    private static void PrintProgress(Progress p)
    {
        const int barWidth = 50;
        var progress = Math.Clamp(p.Percent, 0, 1);
        int filled = (int)(progress * barWidth);

        string bar = new string('#', filled) + new string('-', barWidth - filled);

        Console.Write($"\r[{bar}] {(int)(progress * 100),3}%");
    }

    private static async Task ProcessOptions(InputHelper.Options options)
    {
        var entries = InputHelper.GenerateEntries(options);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            Console.WriteLine("Task " + (i + 1) + " of " + entries.Count);

            Console.WriteLine(
                "Processing: " + 
                entry.InputPaths.First() + 
                (entry.InputPaths.Count > 1 ? "..." : ""));

            Console.WriteLine(
                "Output Type: " + 
                EnumExt.GetDescription(options.OutputType));

            var prevStage = Stage.Idle;

            var progressReporter = new Progress<Progress>(p =>
            {
                if (p.Stage != prevStage)
                {
                    var stageName = EnumExt.GetDescription(p.Stage);

                    if (prevStage != Stage.Idle) 
                        Console.WriteLine();

                    if (p.Stage == Stage.Done)
                        return;

                    Console.WriteLine(stageName + "...");
                    prevStage = p.Stage;
                }

                PrintProgress(p);
            });

            var paths = await Process.ConvertEntry(entry, progressReporter);

            Console.WriteLine("\nDone: " + string.Join(", ", paths) + "\n");
        }

        Console.WriteLine("All tasks completed.");
    }

    private Task HandleExtract(ParseResult results) =>
        ProcessOptions(ParseOptions(XGDToolLib.Image.Type.Extract, results));

    private Task HandleXiso(ParseResult results) =>
        ProcessOptions(ParseOptions(XGDToolLib.Image.Type.XISO, results));

    private Task HandleGod(ParseResult results) =>
        ProcessOptions(ParseOptions(XGDToolLib.Image.Type.GOD, results));

    private Task HandleCci(ParseResult results) =>
        ProcessOptions(ParseOptions(XGDToolLib.Image.Type.CCI, results));

    private Task HandleCso(ParseResult results) =>
        ProcessOptions(ParseOptions(XGDToolLib.Image.Type.CSO, results));

    private Task HandleZar(ParseResult results) =>
        ProcessOptions(ParseOptions(XGDToolLib.Image.Type.ZAR, results));

    private InputHelper.Options ParseOptions(XGDToolLib.Image.Type type, ParseResult results)
    {
        return new InputHelper.Options()
        {
            InputPaths = results.GetRequiredValue(Commands.Options.Input),
            OutputDirectory = results.GetValue(Commands.Options.Output),
            OutputType = type,
            ConvertType = GetConvertType(
                results.GetValue(Commands.Options.Scrub), 
                results.GetValue(Commands.Options.Reauthor)),
            Split = results.GetValue(Commands.Options.Split),
            GenAttachXbe = results.GetValue(Commands.Options.Xbe),
            Rename = !string.IsNullOrEmpty(results.GetValue(Commands.Options.Rename)),
            NewName = results.GetValue(Commands.Options.Rename),
            AllowedMediaPatch = results.GetValue(Commands.Options.AllowedMedia),
            IconPath = results.GetValue(Commands.Options.Icon)
        };
    }

    private static XGDToolLib.Converter.Type GetConvertType(bool? scrub, bool? reauthor)
    {
        if (scrub == true)
            return XGDToolLib.Converter.Type.Scrub;
        else if (reauthor == true)
            return XGDToolLib.Converter.Type.Reauthor;
        else
            return XGDToolLib.Converter.Type.Passthrough;
    }
}
