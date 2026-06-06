using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;

namespace XGDToolLib.Image.Writers;

public class GodReauthor : GodBase
{
    private readonly Avl.Tree AvlTree;
    private long TotalXisoBytes;
    private CancellationToken CancellationToken;
    private IProgress<Converter.Progress>? Progress;

    public GodReauthor(Reader reader, Options options, Title.Info titleInfo)
        : base(reader, options, titleInfo)
    {
        AvlTree = new Avl.Tree(TitleInfo.TitleName);
    }

    protected override long GetTotalOutDataBytes()
    {
        if (TotalXisoBytes == 0)
        {
            if (AvlTree.RootNode is Avl.EmptyNode)
                AvlTree.BuildTree(Reader.DirectoryEntries);

            TotalXisoBytes = XISO.CalculateTotalSize(AvlTree.RootNode);
        }
        return TotalXisoBytes;
    }

    protected override Task WriteData(
        IProgress<Converter.Progress>? progress,
        CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        Progress = progress;
        ProgData.Stage = Converter.Stage.WritingData;

        var startProgress = ProgData.Current;

        if (AvlTree.RootNode is Avl.EmptyNode)
            AvlTree.BuildTree(Reader.DirectoryEntries);

        if (TotalXisoBytes == 0)
            TotalXisoBytes = XISO.CalculateTotalSize(AvlTree.RootNode);

        WriteHeader(AvlTree);

        var avlIterator = new Avl.Iterator(AvlTree);
        var entries = avlIterator.Entries;
        var totalOutSectors = XISO.AlignUp(TotalXisoBytes);
        uint currentSector = (uint)(entries[0].Offset / XISO.SECTOR_SIZE);

        for (var i = 0; i < entries.Count; i++)
        {
            if (CancellationToken.IsCancellationRequested)
                return Task.FromCanceled(CancellationToken);

            var entry = entries[i];

            if (entry.Offset != (currentSector * XISO.SECTOR_SIZE))
            {
                throw new InvalidOperationException(
                    $"Unexpected sector offset for entry {i}.");
            }

            if (entry.IsDirectoryEntry)
            {
                var dirBuffer = avlIterator.WriteDirectoriesToBuffer(
                    i, out var dirCount);

                i += dirCount - 1;

                for (var j = 0; j < dirBuffer.Length; j += XISO.SECTOR_SIZE)
                {
                    WriteXisoSector(
                        currentSector, 
                        dirBuffer.AsSpan(j, XISO.SECTOR_SIZE));

                    currentSector++;

                    ProgData.Current += XISO.SECTOR_SIZE;
                    Progress?.Report(ProgData);
                }
            }
            else
            {
                WriteFile(entry.Node);

                var numSectors = XISO.AlignUp(entry.Node.FileSize);
                currentSector += numSectors;

                ProgData.Current += numSectors * XISO.SECTOR_SIZE;
                Progress?.Report(ProgData);
            }

            if ((i != (entries.Count - 1)) && 
                (entries[i + 1].Offset > (currentSector * XISO.SECTOR_SIZE)))
            {
                uint padSectors = 
                    (uint)(entries[i + 1].Offset / XISO.SECTOR_SIZE) - 
                    currentSector;

                WritePadSectors(currentSector, padSectors, XISO.PAD_BYTE);
                currentSector += padSectors;
            }
        }

        if (currentSector < totalOutSectors)
        {
            uint padSectors = totalOutSectors - currentSector;
            WritePadSectors(currentSector, padSectors, 0);
        }

        ProgData.Current = startProgress + TotalXisoBytes;
        Progress?.Report(ProgData);

        return Task.CompletedTask;
    }

    private void WritePadSectors(uint startSector, uint count, byte padByte)
    {
        var buffer = new byte[XISO.SECTOR_SIZE];
        Array.Fill(buffer, padByte);

        for (uint i = 0; i < count; i++)
            WriteXisoSector(startSector + i, buffer);

        ProgData.Current += XISO.SECTOR_SIZE * count;
        Progress?.Report(ProgData);
    }

    private void WriteFile(Avl.Node node)
    {
        var writeSector = (uint)(node.StartSector);
        var readSector = Reader.SectorOffset + node.OldStartSector;
        var bytesRemaining = node.FileSize;
        var buffer = new byte[XISO.SECTOR_SIZE * 512];

        while (bytesRemaining > 0)
        {
            var readLen = (int)Math.Min(buffer.Length, bytesRemaining);
            bytesRemaining -= readLen;

            Reader.ReadBytes(
                readSector * XISO.SECTOR_SIZE, 
                buffer.AsSpan(0, readLen));

            var numSectors = XISO.AlignUp(readLen);

            if ((numSectors * XISO.SECTOR_SIZE) > readLen)
            {
                int padLen = (int)((numSectors * XISO.SECTOR_SIZE) - readLen);
                Array.Fill(buffer, XISO.PAD_BYTE, readLen, padLen);
            }

            for (uint i = 0; i < numSectors; i++)
            {
                WriteXisoSector(
                    writeSector + i, 
                    buffer.AsSpan((int)(i * XISO.SECTOR_SIZE), 
                    XISO.SECTOR_SIZE));

                ProgData.Current += XISO.SECTOR_SIZE;
                Progress?.Report(ProgData);

                writeSector++;
                readSector++;
            }
        }
    }

    private void WriteHeader(Avl.Tree avlTree)
    {
        var header = new XISO.FileHeader
        (
            (uint)avlTree.RootNode.StartSector,
            (uint)avlTree.RootNode.FileSize,
            (uint)(TotalXisoBytes / XISO.SECTOR_SIZE)
        );

        var numSectors = XISO.AlignUp(header.Size());
        var buffer = new byte[numSectors * XISO.SECTOR_SIZE];

        header.ToBytes(buffer);

        for (uint i = 0; i < numSectors; i++)
            WriteXisoSector(i, buffer);

        ProgData.Current += numSectors * XISO.SECTOR_SIZE;
        Progress?.Report(ProgData);
    }
}
