namespace XGDTool.Lib.Image.Writers;

internal interface ISectorSink
{
    public static ISectorSink Create(IWriterOptions options, Title.Info titleInfo)
    {
        return options.OutputFormat switch
        {
            Format.XISO => new SectorSink.Xiso(options, titleInfo),
            Format.GOD => new SectorSink.God(options, titleInfo),
            Format.CCI => new SectorSink.Cci(options, titleInfo),
            Format.CSO => new SectorSink.Cso(options, titleInfo),
            _ => throw new NotSupportedException($"Image type {options.OutputFormat} is not supported for ISectorSink."),
        };
    }

    public Task Initialize(long totalOutSize, IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default);
    public Task<List<string>> FinalizeImage(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public void CleanupCancelled();
}
