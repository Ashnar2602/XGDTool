using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Image.Writer;

internal interface ISectorSink
{
    public Task Initialize(long outImageSize, IProgress<Converter.Progress>? progress = null, CancellationToken cancellationToken = default);
    public Task WriteSectorsAsync(uint startSector, ReadOnlyMemory<byte> buffer, CancellationToken cancelToken = default);
    public Task<List<string>> FinalizeImage(IProgress<Converter.Progress>? progress = null, CancellationToken cancellationToken = default);
    public void CleanupCanceled();
}
