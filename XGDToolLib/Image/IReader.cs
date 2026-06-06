using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;

namespace XGDToolLib.Image;

internal interface IReader
{
    public long ImageOffset { get; }
    public uint SectorOffset { get; }
    public uint TotalSectors { get; }
    public Type ImageType { get; }
    public Exe.Platform Platform { get; }
    public List<Reader.DirectoryEntry> DirectoryEntries { get; }
    public Reader.DirectoryEntry ExecutableEntry { get; }

    public Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default);
    public Task<HashSet<uint>> GetDataSectors(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default);
    public Task<List<Reader.SectorRange>> GetDataSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default);
    public Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken cancelToken = default);
    public void ReadSector(uint sector, Span<byte> buffer);
    public int ReadBytes(long offset, Span<byte> buffer);
    public uint ReadUInt32(long offset);
    public ushort ReadUInt16(long offset);
    public byte ReadByte(long offset);
}
