namespace XGDToolLib.Image;

public interface IReader
{
    public long ImageOffset { get; }
    public uint SectorOffset { get; }
    public uint TotalSectors { get; }
    public Type ImageType { get; }
    public Exe.Platform Platform { get; }
    public List<Reader.DirectoryEntry> DirectoryEntries { get; }
    public Reader.DirectoryEntry ExecutableEntry { get; }
    public List<string> FilePaths { get; }

    public static IReader Create(Type type, IReadOnlyList<string> files)
    {
        return type switch
        {
            Type.Extract => new Reader.Extract(files),
            Type.XISO => new Reader.Xiso(files),
            //Type.GOD => new Reader.God(files),
            //Type.CCI => new Reader.Cci(files),
            //Type.CSO => new Reader.Cso(files),
            //Type.ZAR => new Reader.Zar(files),
            _ => throw new NotSupportedException($"Unsupported image type: {type}")
        };
    }

    public Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default);
    public Task<List<Reader.SectorRange>> GetDataSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken cancelToken = default);
    public Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken cancelToken = default);
    public void ReadSectors(uint startSector, Span<byte> buffer);
    public int ReadBytes(long offset, Span<byte> buffer);
    public byte[] ReadBytes(long offset, int count);
    public uint ReadUInt32(long offset);
    public ushort ReadUInt16(long offset);
    public byte ReadByte(long offset);
}
