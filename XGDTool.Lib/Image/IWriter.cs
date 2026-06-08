namespace XGDTool.Lib.Image;

public interface IWriter
{
    public static IWriter Create(IReader reader, IWriterOptions options)
    {
        var titleInfo = Title.Resolver.Resolve(reader);
        return options.OutputType switch
        {
            Image.Type.Extract => new Writer.Extract(reader, options, titleInfo),
            Image.Type.ZAR => throw new NotImplementedException(),
            _ => options.ConvertType switch
            {
                Converter.Type.Rewrite => new Writer.Rewrite(reader, options, titleInfo),
                Converter.Type.Reauthor => new Writer.Reauthor(reader, options, titleInfo),
                _ => throw new NotSupportedException($"Unsupported convert type: {options.ConvertType}")
            }
        };
    }

    public Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public void CleanupCancelled();
}