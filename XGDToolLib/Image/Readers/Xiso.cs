using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;

namespace XGDToolLib.Image.Readers;

internal class Xiso : Reader
{
    private class FileEntry
    {
        public required string Path;
        public required FileStream Stream;
    }

    private readonly List<FileEntry> FileEntries = new();
    private readonly long TotalLength;

    public override uint TotalSectors { get; protected set; }
    public override long ImageOffset { get; protected set; }
    public override Type ImageType => Type.XISO;

    public Xiso(IReadOnlyList<string> files) : base(files)
    {
        if (!IsValid(files, out var offset) || offset == null)
            throw new ArgumentException("Input files do not contain a valid XISO image.");

        foreach (var file in files)
        {
            FileEntries.Add(new FileEntry
            {
                Path = file,
                Stream = new FileStream(file, FileMode.Open, FileAccess.Read)
            });
        }

        ImageOffset = offset.Value;
        TotalLength = FileEntries.Sum(fe => fe.Stream.Length);
        TotalSectors = XISO.AlignUp(TotalLength);
    }

    public static bool IsValid(IReadOnlyList<string> files, out long? imageOffset)
    {
        var fList = files.ToList();
        fList.Sort();
        imageOffset = null;

        var f = new FileStream(fList.First(), FileMode.Open, FileAccess.Read);
        var buf = new byte[XISO.MAGIC_SIZE];

        foreach (var offset in XISO.ImageOffsets)
        {
            f.Seek(offset + XISO.MAGIC_OFFSET, SeekOrigin.Begin);
            f.Read(buf);

            if (buf.AsSpan().SequenceEqual(XISO.MAGIC))
            {
                f.Close();
                imageOffset = offset;
                return true;
            }
        }

        f.Close();
        return false;
    }

    public override void ReadSector(uint sector, Span<byte> buffer) =>
        ReadBytes(sector * XISO.SECTOR_SIZE, buffer.Slice(0, XISO.SECTOR_SIZE));

    //public override async Task ReadSectorAsync(uint sector, Memory<byte> buffer, CancellationToken cancelToken = default) =>
    //    await ReadBytesAsync(sector * XISO.SECTOR_SIZE, buffer.Slice(0, XISO.SECTOR_SIZE), cancelToken);

    public override int ReadBytes(long offset, Span<byte> buffer)
    {
        if (buffer.Length == 0)
            return 0;

        var (fe, feOffset) = MapOffset(offset);
        var readLen = (int)Math.Min(buffer.Length, fe.Stream.Length - feOffset);

        fe.Stream.Seek(feOffset, SeekOrigin.Begin);
        var len = fe.Stream.Read(buffer.Slice(0, readLen));

        if (len < readLen)
            return len;

        if (readLen < buffer.Length)
            return len + ReadBytes(offset + readLen, buffer.Slice(readLen));

        return len;
    }

    //public override async Task<int> ReadBytesAsync(long offset, Memory<byte> buffer, CancellationToken cancelToken = default)
    //{
    //    if (buffer.Length == 0)
    //        return 0;

    //    var (fe, feOffset) = MapOffset(offset);
    //    var readLen = (int)Math.Min(buffer.Length, fe.Stream.Length - feOffset);

    //    var len = await RandomAccess.ReadAsync(
    //        fe.Stream.SafeFileHandle,
    //        buffer.Slice(0, readLen),
    //        feOffset,
    //        cancelToken);

    //    if (len < readLen)
    //        return len;

    //    if (len < buffer.Length)
    //        return len + await ReadBytesAsync(offset + readLen, buffer.Slice(readLen), cancelToken);

    //    return len;
    //}

    private (FileEntry, long) MapOffset(long offset)
    {
        if (offset > TotalLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Offset is out of range of the total length of the image.");
        }
        else if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Offset cannot be negative.");
        }

        if (FileEntries.Count == 1)
            return (FileEntries[0], offset);

        foreach (var entry in FileEntries)
        {
            if (offset < entry.Stream.Length)
                return (entry, offset);
            else
                offset -= entry.Stream.Length;
        }

        throw new ArgumentOutOfRangeException(
            nameof(offset),
            "Offset is out of range of the total length of the image.");
    }
}
