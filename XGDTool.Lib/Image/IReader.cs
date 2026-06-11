namespace XGDTool.Lib.Image;

public interface IReader
{
    public long ImageOffset { get; }
    public uint SectorOffset { get; }
    public uint TotalSectors { get; }
    public Format ImageFormat { get; }
    public Exe.Platform Platform { get; }
    public List<Reader.DirectoryEntry> DirectoryEntries { get; }
    public Reader.DirectoryEntry ExecutableEntry { get; }
    public List<string> FilePaths { get; }
    public long TotalSizeOfFiles { get; }

    public static IReader Create(Format type, IReadOnlyList<string> files)
    {
        return type switch
        {
            Format.Extract => new Reader.Extract(files),
            Format.XISO => new Reader.Xiso(files),
            Format.GOD => new Reader.God(files),
            Format.CCI => new Reader.Cci(files),
            //Format.CSO => new Reader.Cso(files),
            //Format.ZAR => new Reader.Zar(files),
            _ => throw new NotSupportedException($"Unsupported image type: {type}")
        };
    }

    public Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public Task<List<Reader.SectorRange>> GetSectorRanges(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default);
    public void ReadSectors(uint startSector, Span<byte> buffer);
    public int ReadBytes(long offset, Span<byte> buffer);
    public byte[] ReadBytes(long offset, int count);
    public uint ReadUInt32(long offset);
    public ushort ReadUInt16(long offset);
    public byte ReadByte(long offset);
}
