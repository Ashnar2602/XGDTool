using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;

namespace XGDToolLib.Image.Writer;

internal class Reauthor : IWriter
{
    private readonly IReader Reader;
    private readonly IWriterOptions Options;
    private readonly ISectorSink SectorSink;
    private readonly Title.Info TitleInfo;
    private readonly Avl.Tree AvlTree;
    private Converter.Progress ProgData = new();
    private CancellationToken CancellationToken;
    private IProgress<Converter.Progress>? Progress;
    private const int BatchSectors = 256;

    public Reauthor(IReader reader, IWriterOptions options, Title.Info titleInfo)
    {
        Reader = reader;
        Options = options;
        TitleInfo = titleInfo;
        SectorSink = ISectorSinkFactory.Create(Reader, Options, TitleInfo);
        AvlTree = new Avl.Tree(TitleInfo.TitleName);
    }

    public async Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken cancellationToken = default)
    {
        CancellationToken = cancellationToken;
        Progress = progress;

        AvlTree.BuildTree(Reader.DirectoryEntries);

        await SectorSink.Initialize(progress, cancellationToken);
        CancellationToken.ThrowIfCancellationRequested();

        var totalSectors = XISO.AlignUp(XISO.CalculateTotalSize(AvlTree.RootNode));

        ProgData = new()
        {
            Stage = Converter.Stage.WritingData,
            Current = 0,
            Total = totalSectors
        };

        await WriteXisoHeader(totalSectors);
        CancellationToken.ThrowIfCancellationRequested();

        var avlIterator = new Avl.Iterator(AvlTree);
        var entries = avlIterator.Entries;
        uint currentSector = (uint)(entries[0].Offset / XISO.SECTOR_SIZE);

        for (var i = 0; i < entries.Count; i++)
        {
            CancellationToken.ThrowIfCancellationRequested();

            var entry = entries[i];

            if (entry.Offset != currentSector * XISO.SECTOR_SIZE)
                throw new InvalidOperationException(
                    $"Entry offset {entry.Offset} does not match expected offset {currentSector * XISO.SECTOR_SIZE}.");

            if (entry.IsDirectoryEntry)
            {
                var dBuffer = avlIterator.WriteDirectoriesToBuffer(i, out var count);
                i += count - 1;

                if (dBuffer.Length % XISO.SECTOR_SIZE != 0)
                    throw new InvalidOperationException(
                        "Directory buffer size is not a multiple of sector size.");

                await SectorSink.WriteSectorsAsync(currentSector, dBuffer, CancellationToken);

                var sectorsWritten = XISO.AlignUp(dBuffer.Length);
                currentSector += sectorsWritten;
                ProgData.Current += sectorsWritten;
                Progress?.Report(ProgData);
            }
            else
            {
                await WriteFile(entry.Node);
            }

            if (i != (entries.Count - 1) && entries[i + 1].Offset > (currentSector * XISO.SECTOR_SIZE))
            {
                uint padSectors =
                    (uint)(entries[i + 1].Offset / XISO.SECTOR_SIZE) -
                    currentSector;

                await WritePadSectors(currentSector, padSectors, XISO.PAD_BYTE);
            }
        }

        if (currentSector < totalSectors)
        {
            uint padSectors = totalSectors - currentSector;
            await WritePadSectors(currentSector, padSectors, 0);
        }

        ProgData.Current = totalSectors;
        Progress?.Report(ProgData);

        return SectorSink.FinalizeImage(Progress, CancellationToken);
    }

    private async Task WriteXisoHeader(uint numSectors)
    {
        var header = new XISO.FileHeader
        (
            (uint)AvlTree.RootNode.StartSector,
            (uint)AvlTree.RootNode.FileSize,
            numSectors
        );

        if (header.Size() % XISO.SECTOR_SIZE != 0)
            throw new InvalidOperationException("Header size is not a multiple of sector size.");

        await SectorSink.WriteSectorsAsync(0, header.ToBytes(), CancellationToken);

        ProgData.Current += XISO.AlignUp(header.Size());
        Progress?.Report(ProgData);
    }

    private async Task WriteFile(Avl.Node fileNode)
    {
        var writeSector = (uint)(fileNode.StartSector);
        var readSector = (uint)(fileNode.OldStartSector);
        var remainingBytes = fileNode.FileSize;
        int bufferIndex = 0;
        Task pendingWrite = Task.CompletedTask;
        byte[][] buffers = 
        {
            new byte[BatchSectors * XISO.SECTOR_SIZE],
            new byte[BatchSectors * XISO.SECTOR_SIZE]
        };

        while (remainingBytes > 0)
        {
            CancellationToken.ThrowIfCancellationRequested();

            int sectorsToWrite = (int)Math.Min(BatchSectors, XISO.AlignUp(remainingBytes));
            int bytesToWrite = sectorsToWrite * XISO.SECTOR_SIZE;

            bufferIndex ^= 1;

            var buffer = buffers[bufferIndex];

            await Reader.ReadSectorsAsync(
                readSector, 
                buffer.AsMemory(0, bytesToWrite),
                CancellationToken);

            await pendingWrite;

            pendingWrite = SectorSink.WriteSectorsAsync(
                writeSector, 
                buffer.AsMemory(0, bytesToWrite), 
                CancellationToken);

            readSector += (uint)sectorsToWrite;
            writeSector += (uint)sectorsToWrite;
            remainingBytes -= bytesToWrite;

            ProgData.Current += (uint)sectorsToWrite;
            Progress?.Report(ProgData);
        }

        await pendingWrite;
    }

    private async Task WritePadSectors(uint startSector, uint count, byte padByte)
    {
        uint sectorsWritten = 0;
        byte[] buffer = new byte[BatchSectors * XISO.SECTOR_SIZE];

        Array.Fill(buffer, padByte);

        while (sectorsWritten < count)
        {
            CancellationToken.ThrowIfCancellationRequested();

            int sectorsToWrite = (int)Math.Min(BatchSectors, count - sectorsWritten);
            int bytesToWrite = sectorsToWrite * XISO.SECTOR_SIZE;

            await SectorSink.WriteSectorsAsync(
                startSector + sectorsWritten, 
                buffer.AsMemory(0, bytesToWrite), 
                CancellationToken);

            sectorsWritten += (uint)sectorsToWrite;
        }

        ProgData.Current += count;
        Progress?.Report(ProgData);
    }
}