using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;
using XGDTool.Lib.Exe;
using XGDTool.Lib.Image.Authoring;

namespace XGDTool.Lib.Image.Readers;

internal class Extract : Base
{
    private class EEntry
    {
        public required uint StartSector;
        public long Offset => StartSector * XDVDFS.SECTOR_SIZE;

        public required long Size;
        public uint NumSectors => XDVDFS.SectorCount(Size);
        public uint EndSector => StartSector + NumSectors - 1;
    }

    private class EFile : EEntry
    {
        public required FileStream Stream;
    }

    private class EDirectory : EEntry
    {
        public required byte[] Buffer;
    }

    private readonly List<EEntry> EEntries = [];
    private string RootDirectory => FilePaths[0];
    private long VirtualSize = 0;

    public override uint TotalSectors => XDVDFS.SectorCount(VirtualSize);
    public override Format ImageFormat => Format.Extract;

    public Extract(IReadOnlyList<string> filePaths) : base(filePaths)
    {
        if (!Directory.Exists(RootDirectory))
            throw new ArgumentException($"The provided path '{RootDirectory}' is not a valid directory.");
    }

    protected override Task InitializeType(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var entryList = new List<DirectoryEntryExt>();
        
        void PopulateEntries(string basePath, string currentPath)
        {
            ct.ThrowIfCancellationRequested();

            if (!Directory.Exists(currentPath))
                throw new InvalidOperationException($"Directory '{currentPath}' does not exist.");

            foreach (var path in Directory.EnumerateFileSystemEntries(currentPath))
            {
                var isDir = Directory.Exists(path);
                
                if (!isDir && !File.Exists(path))
                    continue;

                var fileSize = isDir ? 0 : new FileInfo(path).Length;

                if (fileSize > uint.MaxValue)
                    throw new InvalidOperationException($"File '{path}' exceeds the maximum supported file size of 4GB.");

                var fileName = Path.GetFileName(path);

                if (fileName.Length > byte.MaxValue)
                    throw new InvalidOperationException($"File name '{fileName}' exceeds the maximum length of {byte.MaxValue} characters.");

                if (FATX.SanitizeFileName(fileName) != fileName)
                    throw new InvalidOperationException($"File name '{fileName}' contains invalid FAT characters.");

                var relPath = Path.GetRelativePath(basePath, path);

                if (relPath.StartsWith(".."))
                    throw new InvalidOperationException($"Entry '{path}' is outside of the root directory.");

                entryList.Add(new DirectoryEntryExt
                {
                    FileName = fileName,
                    FilePath = relPath,
                    Attributes = isDir ? XDVDFS.DirAttributes.Directory : XDVDFS.DirAttributes.Normal,
                    FileSize = fileSize
                });

                if (isDir)
                    PopulateEntries(basePath, path);
            }
        }

        PopulateEntries(RootDirectory, RootDirectory);

        var authorer = new XDvdFsAuthorer();
        authorer.CreateTree(entryList);

        VirtualSize = authorer.TotalXisoBytes;

        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.Initializing,
            Current = 0,
            Total = authorer.EntryRecordList.Count
        };
        progress?.Report(progData);

        var volDesc = new XDVDFS.VolumeDescriptor
        {
            RootDirectoryTableSector = authorer.RootStartSector,
            RootDirectoryTableSize = authorer.RootSize,
        };

        EEntries.Add(new EDirectory
        {
            Buffer = volDesc.Serialize(),
            StartSector = XDVDFS.VOLUME_DESCRIPTOR_SECTOR,
            Size = XDVDFS.VolumeDescriptor.SIZE
        });

        for (var i = 0; i < authorer.EntryRecordList.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = authorer.EntryRecordList[i];

            if (!XDVDFS.IsSectorAligned(entry.AbsoluteOffset))
                throw new InvalidOperationException($"Entry '{entry.Node.FilePath}' is not sector aligned.");

            if (entry.IsDirectoryEntry)
            {
                var dirBuffer = authorer.SerializeDirectoryEntryRange(i, out var count);
                i += count - 1;

                if (!XDVDFS.IsSectorAligned(dirBuffer.Length))
                    throw new InvalidOperationException("Directory buffer is not sector aligned.");

                EEntries.Add(new EDirectory
                {
                    Buffer = dirBuffer,
                    StartSector = XDVDFS.SectorIndex(entry.AbsoluteOffset),
                    Size = dirBuffer.Length
                });
            }
            else if (File.Exists(Path.Combine(RootDirectory, entry.Node.FilePath)))
            {
                EEntries.Add(new EFile
                {
                    Stream = new FileStream(
                        Path.Combine(RootDirectory, entry.Node.FilePath),
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read),
                    StartSector = XDVDFS.SectorIndex(entry.AbsoluteOffset),
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

    public override void ReadSectors(uint startSector, Span<byte> buffer, CancellationToken ct = default)
    {
        if (!XDVDFS.IsSectorAligned(buffer.Length))
            throw new ArgumentException(
                "Buffer length must be sector aligned.", 
                nameof(buffer));

        var entry = GetEntryForSector(startSector);
        if (entry == null)
        {
            if (startSector >= TotalSectors)
                throw new ArgumentOutOfRangeException(
                    nameof(startSector),
                    "Start sector is beyond the total sectors of the image.");

            var nextEntry = GetNextEntryAfterSector(startSector);
            var clearBytes = buffer.Length;

            if (nextEntry != null)
            {
                clearBytes = Math.Min(
                    buffer.Length,
                    checked((int)((nextEntry.StartSector - startSector) * XDVDFS.SECTOR_SIZE)));
            }

            buffer.Slice(0, clearBytes).Clear();

            if (clearBytes < buffer.Length)
            {
                ReadSectors(
                    startSector + XDVDFS.SectorCount(clearBytes),
                    buffer.Slice(clearBytes),
                    ct);
            }

            return;
        }

        var offsetInEntry = (int)(startSector - entry.StartSector) * XDVDFS.SECTOR_SIZE;
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
            ReadExactlyAt(file.Stream, buffer.Slice(0, readLen), offsetInEntry);

            if (!XDVDFS.IsSectorAligned(readLen))
            {
                var padLen = checked((int)(XDVDFS.SectorCount(readLen) * XDVDFS.SECTOR_SIZE - readLen));
                buffer.Slice(readLen, padLen).Fill(XDVDFS.PAD_BYTE);
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
                startSector + XDVDFS.SectorCount(copyLen),
                buffer.Slice((int)copyLen),
                ct);
        }
    }

    // public override async Task ReadSectorsAsync(uint startSector, Memory<byte> buffer, CancellationToken ct = default)
    // {
    //     if (!XISO.IsSectorAligned(buffer.Length))
    //         throw new ArgumentException(
    //             "Buffer length must be sector aligned.", 
    //             nameof(buffer));

    //     var entry = GetEntryForSector(startSector);
    //     if (entry == null)
    //     {
    //         if (startSector >= TotalSectors)
    //             throw new ArgumentOutOfRangeException(
    //                 nameof(startSector),
    //                 "Start sector is beyond the total sectors of the image.");

    //         var nextEntry = GetNextEntryAfterSector(startSector);
    //         var clearBytes = buffer.Length;

    //         if (nextEntry != null)
    //         {
    //             clearBytes = Math.Min(
    //                 buffer.Length,
    //                 checked((int)((nextEntry.StartSector - startSector) * XISO.SECTOR_SIZE)));
    //         }

    //         buffer.Span.Slice(0, clearBytes).Clear();

    //         if (clearBytes < buffer.Length)
    //         {
    //             await ReadSectorsAsync(
    //                 startSector + XISO.SectorCount(clearBytes),
    //                 buffer.Slice(clearBytes),
    //                 ct);
    //         }

    //         return;
    //     }

    //     var offsetInEntry = (int)(startSector - entry.StartSector) * XISO.SECTOR_SIZE;
    //     var remainingLen = buffer.Length;
    //     var copyLen = Math.Min(remainingLen, entry.Size - offsetInEntry);

    //     if (entry is EDirectory dir)
    //     {
    //         dir.Buffer.AsSpan(offsetInEntry, (int)copyLen).CopyTo(buffer.Span);
    //         remainingLen -= (int)copyLen;
    //     }
    //     else if (entry is EFile file)
    //     {
    //         var readLen = (int)Math.Min(copyLen, file.Stream.Length - offsetInEntry);
    //         await ReadExactlyAtAsync(file.Stream, buffer.Slice(0, readLen), offsetInEntry, ct);

    //         if (!XISO.IsSectorAligned(readLen))
    //         {
    //             var padLen = checked((int)(XISO.SectorCount(readLen) * XISO.SECTOR_SIZE - readLen));
    //             buffer.Span.Slice(readLen, padLen).Fill(XISO.PAD_BYTE);
    //             readLen += padLen;
    //         }

    //         copyLen = readLen;
    //         remainingLen -= readLen;
    //     }
    //     else
    //     {
    //         throw new InvalidOperationException("Unknown entry type.");
    //     }

    //     if (remainingLen > 0)
    //     {
    //         await ReadSectorsAsync(
    //             startSector + XISO.SectorCount(copyLen),
    //             buffer.Slice((int)copyLen),
    //             ct);
    //     }
    // }

    public static bool IsValid(string dirPath)
    {
        if (!Directory.Exists(dirPath))
            return false;

        var xexPath = Path.Join(dirPath, "default.xex");
        var xbePath = Path.Join(dirPath, "default.xbe");
        FileStream? f;

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
        EEntries.FirstOrDefault(e => sector >= e.StartSector && sector <= e.EndSector);

    private EEntry? GetNextEntryAfterSector(uint sector) =>
        EEntries
            .Where(e => e.StartSector > sector)
            .OrderBy(e => e.StartSector)
            .FirstOrDefault();
}
