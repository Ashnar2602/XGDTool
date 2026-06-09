namespace XGDTool.Lib.Image;

public interface IWriter
{
    public static IWriter Create(IReader reader, IWriterOptions options)
    {
        var titleInfo = Title.Resolver.Resolve(reader);
        return options.WriterType switch
        {
            IWriterType.Extract => new Writer.Extract(reader, options, titleInfo),
            IWriterType.Rewrite => new Writer.Rewrite(reader, options, titleInfo),
            IWriterType.Reauthor => new Writer.Reauthor(reader, options, titleInfo),
            IWriterType.Zar => throw new NotImplementedException(),
            _ => throw new NotSupportedException($"Unsupported convert type: {options.WriterType}")
        };
    }

    public Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public void CleanupCancelled();
}