using System.CommandLine;

namespace XGDTool.CLI;

internal class Options
{
    public Option<string[]> Input = new("--input", new[] { "-i" })
    {
        Description = "The path(s) to the input file(s) or directory. If using a split file, providing all parts is not required.",
        Required = true
    };

    public Option<string?> Output = new("--output", "-o")
    {
        Description = "The path to the output directory, defaults to current working directory."
    };

    public Option<bool?> Scrub = new("--scrub", "-s")
    {
        Description = "Whether to scrub the output file(s) (i.e. zero out all unused space)."
    };

    public Option<bool?> Reauthor = new("--reauthor", "-r")
    {
        Description = "Whether to reauthor the output file(s) (i.e. update the file system metadata to reflect the actual contents of the disc image)."
    };

    public Option<bool?> Split = new("--split", "-S")
    {
        Description = "Whether to split the output file(s) into 4GB parts."
    };

    public Option<bool?> GenerateXbe = new("--xbe", "-x")
    {
        Description = "Generate an attach XBE file for the output file(s)."
    };

    public Option<bool?> SkipSystemUpdate = new("--systemupdate", "-u")
    {
        Description = "Skip the system update files for Xbox 360 images."
    };

    public Option<string?> Rename = new("--rename", "-n")
    {
        Description = "Rename the output XBE to match the disc volume label, or a provided name."
    };

    public Option<bool?> AllowedMedia = new("--media", "-m")
    {
        Description = "Patch XBE allowed media flags."
    };

    public Option<string?> Icon = new("--icon", "-c")
    {
        Description = "Change the XBE title icon or GOD file icon."
    };
}

internal class Commands
{
    public Options Options = new();
    public Command Extract;
    public Command Xiso;
    public Command God;
    public Command Cci;
    public Command Cso;
    public Command Zar;
    public Command AutoXbox;
    public Command AutoXbox360;
    public Command AutoXemu;
    public Command AutoXenia;

    public Commands()
    {
        Extract = ExtractNew(Options);
        Xiso = XisoNew(Options);
        God = GodNew(Options);
        Cci = CciNew(Options);
        Cso = CsoNew(Options);
        Zar = ZarNew(Options);
        AutoXbox = AutoXboxNew(Options);
        AutoXbox360 = AutoXbox360New(Options);
        AutoXemu = AutoXemuNew(Options);
        AutoXenia = AutoXeniaNew(Options);
    }

    private static Command ExtractNew(Options options)
    {
        var cmd = new Command("extract", "Extracts files from an Xbox game disc image.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        cmd.Options.Add(options.Rename);
        cmd.Options.Add(options.SkipSystemUpdate);
        cmd.Options.Add(options.Icon);
        return cmd;
    }

    private static Command XisoNew(Options options)
    {
        var cmd = new Command("xiso", "Converts an Xbox game disc image to XISO format.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        cmd.Options.Add(options.Scrub);
        cmd.Options.Add(options.Split);
        cmd.Options.Add(options.Reauthor);
        cmd.Options.Add(options.AllowedMedia);
        cmd.Options.Add(options.SkipSystemUpdate);
        cmd.Options.Add(options.GenerateXbe);
        cmd.Options.Add(options.Rename);
        cmd.Options.Add(options.Icon);
        return cmd;
    }

    private static Command GodNew(Options options)
    {
        var cmd = new Command("god", "Converts an Xbox game disc image to Games on Demand format.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        cmd.Options.Add(options.Scrub);
        cmd.Options.Add(options.Reauthor);
        cmd.Options.Add(options.SkipSystemUpdate);
        cmd.Options.Add(options.Rename);
        cmd.Options.Add(options.Icon);
        return cmd;
    }

    private static Command CciNew(Options options)
    {
        var cmd = new Command("cci", "Converts an Xbox game disc image to CCI format.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        cmd.Options.Add(options.Scrub);
        cmd.Options.Add(options.Reauthor);
        cmd.Options.Add(options.Split);
        cmd.Options.Add(options.GenerateXbe);
        cmd.Options.Add(options.Rename);
        cmd.Options.Add(options.Icon);
        return cmd;
    }

    private static Command CsoNew(Options options)
    {
        var cmd = new Command("cso", "Converts an Xbox game disc image to CSO format.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        cmd.Options.Add(options.Scrub);
        cmd.Options.Add(options.Reauthor);
        cmd.Options.Add(options.Split);
        cmd.Options.Add(options.GenerateXbe);
        cmd.Options.Add(options.Rename);
        cmd.Options.Add(options.Icon);
        return cmd;
    }

    private static Command ZarNew(Options options)
    {
        var cmd = new Command("zar", "Converts an Xbox game disc image to ZAR format.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        cmd.Options.Add(options.SkipSystemUpdate);
        return cmd;
    }

    private static Command AutoXboxNew(Options options)
    {
        var cmd = new Command("autoxbox", "Automatically configures options for Xbox format.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        return cmd;
    }

    private static Command AutoXbox360New(Options options)
    {
        var cmd = new Command("autoxbox360", "Automatically configures options for Xbox 360 format.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        return cmd;
    }

    private static Command AutoXemuNew(Options options)
    {
        var cmd = new Command("autoxemu", "Automatically configures options for Xemu.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        return cmd;
    }

    private static Command AutoXeniaNew(Options options)
    {
        var cmd = new Command("autoxenia", "Automatically configures options for Xenia.");
        cmd.Options.Add(options.Input);
        cmd.Options.Add(options.Output);
        return cmd;
    }
}
