using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image;

public interface IReader
{
    public long ImageOffset { get; }
    public uint SectorOffset { get; }
    public uint TotalSectors { get; }
    public Format ImageFormat { get; }
    public Exe.Platform Platform { get; }
    public List<DirectoryEntryExt> DirectoryEntries { get; }
    public DirectoryEntryExt ExecutableEntry { get; }
    public List<string> FilePaths { get; }
    public long TotalSizeOfFiles { get; }
    public ulong FileTime { get; }

    public static IReader Create(Format type, IReadOnlyList<string> files)
    {
        return type switch
        {
            Format.Extract => new Readers.Extract(files),
            Format.XISO => new Readers.Xiso(files),
            Format.GOD => new Readers.God(files),
            Format.CCI => new Readers.Cci(files),
            Format.CSO => new Readers.Cso(files),
            Format.ZAR => new Readers.Zar(files),
            _ => throw new NotSupportedException($"Unsupported image type: {type}")
        };
    }

    public Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default);
    public Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default);
    public void ReadSectors(uint startSector, Span<byte> buffer, CancellationToken ct = default);
    public int ReadBytes(long offset, Span<byte> buffer);
    public byte[] ReadBytes(long offset, int count);
    public uint ReadUInt32(long offset);
    public ushort ReadUInt16(long offset);
    public byte ReadByte(long offset);
    public XDVDFS.VolumeDescriptor ReadVolumeDescriptor(long? imageOffset = null);
    public DirectoryEntryExt GetRootEntry();
    public DirectoryEntryExt ReadEntry(long offset, byte[]? buf = null);
}
