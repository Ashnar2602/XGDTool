using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.Lib.Image.Writer;

internal interface ISectorSink
{
    public static ISectorSink Create(IReader reader, IWriterOptions options, Title.Info titleInfo)
    {
        return options.OutputType switch
        {
            Type.XISO => new SectorSink.Xiso(reader, options, titleInfo),
            Type.GOD => new SectorSink.God(reader, options, titleInfo),
            Type.CCI => new SectorSink.Cci(reader, options, titleInfo),
            //Type.CSO => new SectorSinks.Cso(reader, options, titleInfo),
            _ => throw new NotSupportedException($"Image type {options.OutputType} is not supported for writing."),
        };
    }

    public Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken ct = default);
    public Task<List<string>> FinalizeImage(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public void CleanupCancelled();
}
