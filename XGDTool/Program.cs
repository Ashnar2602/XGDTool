namespace XGDTool;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var cli = new CLI.Program();
            int exitCode = cli.Run(args).GetAwaiter().GetResult();
            Environment.Exit(exitCode);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
