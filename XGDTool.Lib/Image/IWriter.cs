namespace XGDTool.Lib.Image;

public interface IWriter
{
    public static IWriter Create(IReader reader, IWriterOptions options)
    {
        var titleInfo = Title.Resolver.Resolve(reader);
        return options.ConvertType switch
        {
            Converter.Type.Rewrite => new Writer.Rewrite(reader, options, titleInfo),
            Converter.Type.Reauthor => new Writer.Reauthor(reader, options, titleInfo),
            Converter.Type.Extract => new Writer.Extract(reader, options, titleInfo),
            //Converter.Type.Zar => new Writer.Zar(reader, options, titleInfo),
            _ => throw new NotSupportedException($"Unsupported convert type: {options.ConvertType}")
        };
    }

    public Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public void CleanupCancelled();
}