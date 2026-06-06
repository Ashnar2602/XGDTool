using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Exe;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;

namespace XGDToolLib.Image.Readers;

internal class Extract(string directory) 
    : Reader(new[] { directory })
{
    class FileEntry
    {
        public required Avl.Iterator.Entry Entry;
        public required FileStream Stream;
    }

    public override long ImageOffset { get; protected set; }
    public override uint TotalSectors { get; protected set; }
    public override Type ImageType => Type.Extract;

    private readonly string DirectoryPath = directory;
    private List<FileEntry> FileEntries = new();
    private byte[] HeaderBuffer = Array.Empty<byte>();
    private uint DirectoryStart;
    private byte[] DirectoryBuffer = Array.Empty<byte>();
    private uint FilesStart;
    private uint FilesEnd;

    public static bool IsValid(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        var buf = new byte[4];

        if (File.Exists(Path.Combine(directory, "default.xex")))
        {
            var f = new FileStream(
                Path.Combine(directory, "default.xex"), 
                FileMode.Open, 
                FileAccess.Read);

            f.Read(buf, 0, 4);
            f.Close();

            if (XEX.MAGIC.Equals(BitConverter.ToUInt32(buf, 0)))
                return true;
        }
        else if (File.Exists(Path.Combine(directory, "default.xbe")))
        {
            var f = new FileStream(
                Path.Combine(directory, "default.xbe"), 
                FileMode.Open, 
                FileAccess.Read);

            f.Read(buf, 0, 4);
            f.Close();

            if (XBE.MAGIC.Equals(BitConverter.ToUInt32(buf, 0)))
                return true;
        }
        return false;
    }

    protected override void InitializeType()
    {
        ImageOffset = 0;
        FileEntries.Clear();

        var avlTree = new Avl.Tree(Path.GetFileName(DirectoryPath));
        avlTree.BuildTree(DirectoryPath);

        TotalSectors = (uint)(XISO.CalculateTotalSize(avlTree.RootNode) / XISO.SECTOR_SIZE);
        var header = new XISO.FileHeader(
            (uint)avlTree.RootNode.StartSector,
            (uint)avlTree.RootNode.FileSize,
            TotalSectors);

        HeaderBuffer = header.ToBytes();

        var padding = XISO.SECTOR_SIZE - HeaderBuffer.Length;

        if (padding != XISO.SECTOR_SIZE)
            Array.Resize(ref HeaderBuffer, HeaderBuffer.Length + padding);

        DirectoryStart = (uint)avlTree.RootNode.StartSector;

        var iterator = new Avl.Iterator(avlTree);
        DirectoryBuffer = iterator.WriteDirectoriesToBuffer(0, out int count);

        padding = XISO.SECTOR_SIZE - (DirectoryBuffer.Length % XISO.SECTOR_SIZE);

        if (padding != XISO.SECTOR_SIZE)
            Array.Resize(ref DirectoryBuffer, DirectoryBuffer.Length + padding);

        foreach (var e in iterator.Entries)
        {
            FileEntries.Add(new FileEntry 
            { 
                Entry = e, 
                Stream = new FileStream(e.Node.Filepath, FileMode.Open, FileAccess.Read) 
            });
        }

        FileEntries = FileEntries.OrderBy(e => e.Entry.Node.StartSector).ToList();
        FilesStart = (uint)FileEntries.First().Entry.Node.StartSector;

        var lastEntry = FileEntries.Last();
        FilesEnd = (uint)(lastEntry.Entry.Node.StartSector + XISO.AlignUp(lastEntry.Entry.Node.FileSize));
    }

    //public override async Task ReadSectorAsync(uint sector, Memory<byte> buffer, CancellationToken cancelToken = default)
    //{
    //    if (buffer.Length < XISO.SECTOR_SIZE)
    //    {
    //        throw new ArgumentException(
    //            $"Buffer must be at least {XISO.SECTOR_SIZE} bytes in size.",
    //            nameof(buffer));
    //    }

    //    long isoOffset = (long)sector * XISO.SECTOR_SIZE;

    //    if (sector < DirectoryStart)
    //    {
    //        if (isoOffset + XISO.SECTOR_SIZE <= HeaderBuffer.Length)
    //            HeaderBuffer.AsMemory((int)isoOffset, XISO.SECTOR_SIZE).CopyTo(buffer);
    //        else
    //            buffer.Span.Clear();
    //    }
    //    else if (sector < FilesStart)
    //    {
    //        if (isoOffset + XISO.SECTOR_SIZE <= DirectoryBuffer.Length)
    //        {
    //            var dirOffset = (int)(isoOffset - (DirectoryStart * XISO.SECTOR_SIZE));
    //            DirectoryBuffer.AsMemory(dirOffset, XISO.SECTOR_SIZE).CopyTo(buffer);
    //        }
    //        else
    //        {
    //            buffer.Span.Clear();
    //        }
    //    }
    //    else if (sector < FilesEnd)
    //    {
    //        var fileEntry = FileEntries.LastOrDefault(e => e.Entry.Node.StartSector <= sector);
    //        if (fileEntry != null)
    //        {
    //            var fileOffset =
    //                isoOffset -
    //                (fileEntry.Entry.Node.StartSector * XISO.SECTOR_SIZE);

    //            if (fileOffset < fileEntry.Entry.Node.FileSize)
    //            {
    //                int len = await RandomAccess.ReadAsync(
    //                    fileEntry.Stream.SafeFileHandle, 
    //                    buffer, 
    //                    fileOffset, 
    //                    cancelToken);

    //                if (len < XISO.SECTOR_SIZE)
    //                    buffer.Span.Slice(len).Fill(XISO.PAD_BYTE);
    //            }
    //            else
    //            {
    //                //Array.Fill(buffer, XISO.PAD_BYTE);
    //                throw new Exception(
    //                    $"Sector {sector} is beyond the end of file {fileEntry.Entry.Node.Filename}.");
    //            }
    //        }
    //        else
    //        {
    //            throw new Exception($"No file entry found for sector {sector}.");
    //        }
    //    }
    //    else
    //    {
    //        buffer.Span.Fill(XISO.PAD_BYTE);
    //    }
    //}

    public override void ReadSector(uint sector, Span<byte> buffer)
    {
        if (buffer.Length < XISO.SECTOR_SIZE)
        {
            throw new ArgumentException(
                $"Buffer must be at least {XISO.SECTOR_SIZE} bytes in size.", 
                nameof(buffer));
        }

        long isoOffset = (long)sector * XISO.SECTOR_SIZE;

        if (sector < DirectoryStart)
        {
            if (isoOffset + XISO.SECTOR_SIZE <= HeaderBuffer.Length)
                HeaderBuffer.AsSpan((int)isoOffset, XISO.SECTOR_SIZE).CopyTo(buffer);
            else
                buffer.Clear();  
        }
        else if (sector < FilesStart)
        {
            if (isoOffset + XISO.SECTOR_SIZE <= DirectoryBuffer.Length)
            {
                int dirOffset = (int)(isoOffset - (DirectoryStart * XISO.SECTOR_SIZE));
                DirectoryBuffer.AsSpan(dirOffset, XISO.SECTOR_SIZE).CopyTo(buffer);
            }
            else
            {
                buffer.Clear();
            }
        }
        else if (sector < FilesEnd)
        {
            var fileEntry = FileEntries.LastOrDefault(e => e.Entry.Node.StartSector <= sector);
            if (fileEntry != null)
            {
                var fileOffset =
                    isoOffset -
                    (fileEntry.Entry.Node.StartSector * XISO.SECTOR_SIZE);

                if (fileOffset < fileEntry.Entry.Node.FileSize)
                {
                    fileEntry.Stream.Seek(fileOffset, SeekOrigin.Begin);
                    var len = fileEntry.Stream.Read(buffer.Slice(0, XISO.SECTOR_SIZE));

                    if (len < XISO.SECTOR_SIZE)
                        buffer.Slice(len).Fill(XISO.PAD_BYTE);
                }
                else
                {
                    //Array.Fill(buffer, XISO.PAD_BYTE);
                    throw new Exception(
                        $"Sector {sector} is beyond the end of file {fileEntry.Entry.Node.Filename}.");
                }
            }
            else
            {
                throw new Exception($"No file entry found for sector {sector}.");
            }
        }
        else
        {
            buffer.Fill(XISO.PAD_BYTE);
        }
    }
}
