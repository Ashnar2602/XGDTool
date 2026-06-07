using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Lib.Image.Format;
using XGDTool.Lib.Util;
using XGDTool.Lib.Exe;

namespace XGDTool.Lib.Image.Reader;

internal class Extract : Base
{
    private class EEntry
    {
        public required uint StartSector;
        public long Offset => StartSector * XISO.SECTOR_SIZE;

        public required long Size;
        public uint NumSectors => XISO.AlignUpToSector(Size);
        public uint EndSector => StartSector + NumSectors;
    }

    private class EFile : EEntry
    {
        public required FileStream Stream;
    }

    private class EDirectory : EEntry
    {
        public required byte[] Buffer;
    }

    private readonly Avl.Tree AvlTree;
    private readonly Avl.Iterator AvlIterator;
    private readonly List<EEntry> EEntries = new();
    private string DirPath => FilePaths[0];
    private long VirtualSize = 0;

    public override uint TotalSectors 
    { 
        get => XISO.AlignUpToSector(VirtualSize); 
        protected set => throw new NotImplementedException(); 
    }
    public override Type ImageType => Type.Extract;

    public Extract(IReadOnlyList<string> filePaths) : base(filePaths)
    {
        if (!Directory.Exists(DirPath))
            throw new ArgumentException($"The provided path '{DirPath}' is not a valid directory.");
        
        var dirName = Path.GetFileName(DirPath);
        AvlTree = new Avl.Tree(dirName);
        AvlIterator = new Avl.Iterator(AvlTree);
    }

    protected override Task InitializeType(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        AvlTree.BuildTree(DirPath);
        VirtualSize = XISO.CalculateTotalSize(AvlTree.RootNode);
        var totalSectors = XISO.AlignUpToSector(VirtualSize);

        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.Initializing,
            Current = 0,
            Total = AvlIterator.Entries.Count
        };
        progress?.Report(progData);

        var header = new XISO.FileHeader(
            (uint)AvlTree.RootNode.StartSector,
            (uint)AvlTree.RootNode.FileSize,
            totalSectors);

        EEntries.Add(new EDirectory
        {
            Buffer = header.ToBytes(),
            StartSector = 0,
            Size = header.Size()
        });

        for (var i = 0; i < AvlIterator.Entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = AvlIterator.Entries[i];

            if (!XISO.IsSectorAligned(entry.Offset))
                throw new InvalidOperationException($"Entry '{entry.Node.Filepath}' is not sector aligned.");

            if (entry.IsDirectoryEntry)
            {
                var dirBuffer = AvlIterator.WriteDirectoriesToBuffer(i, out var count);
                i += count - 1;

                if (!XISO.IsSectorAligned(dirBuffer.Length))
                    throw new InvalidOperationException("Directory buffer is not sector aligned.");

                EEntries.Add(new EDirectory
                {
                    Buffer = dirBuffer,
                    StartSector = XISO.AlignUpToSector(entry.Offset),
                    Size = dirBuffer.Length
                });
            }
            else
            {
                EEntries.Add(new EFile
                {
                    Stream = new FileStream(
                        entry.Node.Filepath,
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read),
                    StartSector = XISO.AlignUpToSector(entry.Offset),
                    Size = entry.Node.FileSize
                });
            }

            progData.Current = i + 1;
            progress?.Report(progData);
        }

        progData.Current = progData.Total;
        progress?.Report(progData);

        return Task.CompletedTask;
    }

    public override void ReadSectors(uint startSector, Span<byte> buffer)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be sector aligned.", 
                nameof(buffer));

        var entry = GetEntryForSector(startSector);
        if (entry == null)
        {
            if (startSector > TotalSectors)
                throw new ArgumentOutOfRangeException(
                    nameof(startSector), 
                    "Start sector is beyond the total sectors of the image.");
            
            buffer.Clear();
            return;
        }

        var offsetInEntry = (int)(startSector - entry.StartSector) * XISO.SECTOR_SIZE;
        var remainingLen = buffer.Length;
        var copyLen = Math.Min(remainingLen, entry.Size - offsetInEntry);

        if (entry is EDirectory dir)
        {
            dir.Buffer.AsSpan(offsetInEntry, (int)copyLen).CopyTo(buffer);
            remainingLen -= (int)copyLen;
        }
        else if (entry is EFile file)
        {
            var readLen = (int)Math.Min(copyLen, file.Stream.Length - offsetInEntry);

            file.Stream.Seek(offsetInEntry, SeekOrigin.Begin);
            {
                var bytesRead = file.Stream.Read(buffer.Slice(0, readLen));
                if (bytesRead != readLen)
                    throw new IOException($"Expected to read {readLen} bytes but only read {bytesRead} bytes.");
            }

            if (!XISO.IsSectorAligned(readLen))
            {
                var padLen = (int)(XISO.AlignUpToSector(readLen) - readLen);
                buffer.Slice(readLen, padLen).Fill(XISO.PAD_BYTE);
                readLen += padLen;
            }

            copyLen = readLen;
            remainingLen -= readLen;
        }
        else
        {
            throw new InvalidOperationException("Unknown entry type.");
        }

        if (remainingLen > 0) 
        {
            ReadSectors(
                startSector + XISO.AlignUpToSector(copyLen), 
                buffer.Slice((int)copyLen));
        }
    }

    public override async Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    {
        if (!XISO.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be sector aligned.", 
                nameof(buffer));

        var entry = GetEntryForSector(startSector);
        if (entry == null)
        {
            if (startSector > TotalSectors)
                throw new ArgumentOutOfRangeException(
                    nameof(startSector), 
                    "Start sector is beyond the total sectors of the image.");

            buffer.Span.Clear();
            return;
        }

        var offsetInEntry = (int)(startSector - entry.StartSector) * XISO.SECTOR_SIZE;
        var remainingLen = buffer.Length;
        var copyLen = Math.Min(remainingLen, entry.Size - offsetInEntry);

        if (entry is EDirectory dir)
        {
            dir.Buffer.AsSpan(offsetInEntry, (int)copyLen).CopyTo(buffer.Span);
            remainingLen -= (int)copyLen;
        }
        else if (entry is EFile file)
        {
            var readLen = (int)Math.Min(copyLen, file.Stream.Length - offsetInEntry);

            file.Stream.Seek(offsetInEntry, SeekOrigin.Begin);
            {
                var bytesRead = await file.Stream.ReadAsync(buffer.Slice(0, readLen), ct);
                if (bytesRead != readLen)
                    throw new IOException(
                        $"Expected to read {readLen} bytes but only read {bytesRead} bytes.");
            }

            if (!XISO.IsSectorAligned(readLen))
            {
                var padLen = (int)(XISO.AlignUpToSector(readLen) - readLen);
                buffer.Span.Slice(readLen, padLen).Fill(XISO.PAD_BYTE);
                readLen += padLen;
            }

            copyLen = readLen;
            remainingLen -= readLen;
        }
        else
        {
            throw new InvalidOperationException("Unknown entry type.");
        }

        if (remainingLen > 0)
        {
            await ReadSectorsAsync(
                startSector + XISO.AlignUpToSector(copyLen), 
                buffer.Slice((int)copyLen), 
                ct);
        }
    }

    public static bool IsValid(string dirPath)
    {
        if (!Directory.Exists(dirPath))
            return false;

        var xexPath = Path.Join(dirPath, "default.xex");
        var xbePath = Path.Join(dirPath, "default.xbe");
        FileStream? f = null;

        if (File.Exists(xexPath))
            f = new FileStream(xexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        else if (File.Exists(xbePath))
            f = new FileStream(xbePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        else
            return false;

        var buf = new byte[4];
        f.Read(buf, 0, 4);
        uint magic = BitConverter.ToUInt32(buf, 0);

        if (magic == XEX.MAGIC)
            return true;
        else if (magic == XBE.MAGIC)
            return true;
        else
            return false;
    }

    private EEntry? GetEntryForSector(uint sector) => 
        EEntries.FirstOrDefault(e => sector >= e.StartSector && sector < e.EndSector);
}
