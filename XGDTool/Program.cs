namespace XGDTool;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            int exitCode = 0;
            if (args.Length > 0)
            {
                var cli = new CLI.Program();
                exitCode = cli.Run(args).GetAwaiter().GetResult();
            }
            else
            {
                GUI.Program.Main(args);
            }
            Environment.Exit(exitCode);
        }
        catch
        {
            Environment.Exit(1);
        }
    }
}
