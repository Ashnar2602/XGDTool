class Program
{
    static void Main(string[] args)
    {
        try
        {
            var cli = new XGDToolCLI.Program();
            int exitCode = cli.Run(args).GetAwaiter().GetResult();
            Console.WriteLine($"Operation completed with exit code: {exitCode}");
            Environment.Exit(exitCode);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
