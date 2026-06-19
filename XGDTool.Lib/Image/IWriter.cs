namespace XGDTool.Lib.Image;

public interface IWriter
{
    public static IWriter Create(IReader reader, IWriterOptions options, Title.Info? titleInfo = null)
    {
        titleInfo ??= Title.Resolver.Resolve(reader);

        return options.WriterType switch
        {
            IWriterType.Rewrite => new Writers.Rewrite(reader, options, titleInfo),
            IWriterType.Reauthor => new Writers.Reauthor(reader, options, titleInfo),
            IWriterType.Extract => new Writers.Extract(reader, options, titleInfo),
            IWriterType.Zar => new Writers.Zar(reader, options, titleInfo),
            _ => throw new NotSupportedException($"Unsupported convert type: {options.WriterType}")
        };
    }

    public Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public void CleanupCancelled();
}