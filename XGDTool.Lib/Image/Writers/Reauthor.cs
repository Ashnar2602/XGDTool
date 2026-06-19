using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Exe;
using XGDTool.Lib.Util;
using XGDTool.Lib.Converter;
using XGDTool.Lib.Image.Authoring;

namespace XGDTool.Lib.Image.Writers;

internal class Reauthor : IWriter
{
    private readonly IReader Reader;
    private readonly IWriterOptions Options;
    private readonly ISectorSink SectorSink;
    private readonly Title.Info TitleInfo;
    private const int BatchSectors = 256;

    public Reauthor(IReader reader, IWriterOptions options, Title.Info titleInfo)
    {
        Reader = reader;
        Options = options;
        TitleInfo = titleInfo;
        SectorSink = ISectorSink.Create(Options, TitleInfo);
    }

    public async Task<IReadOnlyList<string>> Convert(IProgress<Progress>? progress = null, CancellationToken ct = default)
    {
        var dirEntries = Reader.DirectoryEntries;
        if (Options.SkipSystemUpdate == true && Reader.Platform == Platform.Xbox360)
        {
            dirEntries.RemoveAll(e => e.FilePath.StartsWith(
                XDVDFS.SYSTEM_UPDATE_DIRECTORY_NAME, 
                StringComparison.OrdinalIgnoreCase));
        }

        var authorer = new XDvdFsAuthorer();
        authorer.CreateTree(dirEntries);

        var totalSize = authorer.TotalXisoBytes;
        var totalSectors = XDVDFS.SectorCount(totalSize);

        await SectorSink.Initialize(totalSize, progress, ct);
        ct.ThrowIfCancellationRequested();

        var progCtx = new ProgressContext
        {
            Progress = new Progress
            {
                Stage = Stage.WritingData,
                Current = 0,
                Total = totalSectors
            },
            Reporter = progress,
            Ct = ct
        };

        await WriteXisoHeader(authorer.RootNode, Reader.FileTime, progCtx);

        var entryRecords = authorer.EntryRecordList;
        uint sectorIndex = XDVDFS.SectorIndex(entryRecords[0].AbsoluteOffset);

        for (var i = 0; i < entryRecords.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = entryRecords[i];

            if (entry.AbsoluteOffset != sectorIndex * XDVDFS.SECTOR_SIZE)
                throw new InvalidOperationException(
                    $"Entry offset {entry.AbsoluteOffset} does not match expected offset {sectorIndex * XDVDFS.SECTOR_SIZE}.");

            if (entry.IsDirectoryEntry)
            {
                var dBuffer = authorer.SerializeDirectoryEntryRange(i, out int count);
                i += count - 1;

                if (!XDVDFS.IsSectorAligned(dBuffer.Length))
                    throw new InvalidOperationException(
                        "Directory buffer size is not a multiple of sector size.");

                await SectorSink.WriteSectorsAsync(sectorIndex, dBuffer, ct);
                sectorIndex += XDVDFS.SectorCount(dBuffer.Length);
            }
            else
            {
                await WriteFile(entry.Node, progCtx);
                sectorIndex += XDVDFS.SectorCount(entry.Node.FileSize);
            }

            if (i != (entryRecords.Count - 1) && 
                entryRecords[i + 1].AbsoluteOffset > (sectorIndex * XDVDFS.SECTOR_SIZE))
            {
                uint padSectors =
                    (uint)(entryRecords[i + 1].AbsoluteOffset / XDVDFS.SECTOR_SIZE) -
                    sectorIndex;

                await WritePadSectors(sectorIndex, padSectors, XDVDFS.PAD_BYTE, progCtx);
                sectorIndex += padSectors;
            }

            progCtx.Report(sectorIndex + 1);
        }

        if (sectorIndex < totalSectors)
        {
            uint padSectors = totalSectors - sectorIndex;
            await WritePadSectors(sectorIndex, padSectors, 0, progCtx);
        }

        progCtx.Report(totalSectors);
        return await SectorSink.FinalizeImage(progCtx.Reporter, progCtx.Ct);
    }

    public void CleanupCancelled() => SectorSink.CleanupCancelled();

    private async Task WriteXisoHeader(DirectoryNode root, ulong fileTime, ProgressContext progCtx)
    {
        var volDesc = new XDVDFS.VolumeDescriptor
        {
            RootDirectoryTableSector = (uint)root.StartSector,
            RootDirectoryTableSize = (uint)root.FileSize,
            FileTime = fileTime
        };

        await SectorSink.WriteSectorsAsync(XDVDFS.VOLUME_DESCRIPTOR_SECTOR, volDesc.Serialize(), progCtx.Ct);

        progCtx.Report(XDVDFS.VOLUME_DESCRIPTOR_SECTOR + 1);
    }

    private async Task WriteFile(DirectoryNode fileNode, ProgressContext progCtx)
    {
        var writeSector = (uint)fileNode.StartSector;
        var readSector = (uint)fileNode.OldEntry.StartSector + Reader.SectorOffset;
        var remainingBytes = fileNode.FileSize;
        int bufferIndex = 0;
        Task pendingWrite = Task.CompletedTask;
        byte[][] buffers = 
        {
            new byte[BatchSectors * XDVDFS.SECTOR_SIZE],
            new byte[BatchSectors * XDVDFS.SECTOR_SIZE]
        };

        while (remainingBytes > 0)
        {
            progCtx.Ct.ThrowIfCancellationRequested();

            int sectorsToWrite = (int)Math.Min(BatchSectors, XDVDFS.SectorCount(remainingBytes));
            int bytesToWrite = sectorsToWrite * XDVDFS.SECTOR_SIZE;

            bufferIndex ^= 1;

            var buffer = buffers[bufferIndex];

            await Reader.ReadSectorsAsync(
                readSector, 
                buffer.AsMemory(0, bytesToWrite),
                progCtx.Ct);

            await pendingWrite;

            pendingWrite = SectorSink.WriteSectorsAsync(
                writeSector, 
                buffer.AsMemory(0, bytesToWrite), 
                progCtx.Ct);

            readSector += (uint)sectorsToWrite;
            writeSector += (uint)sectorsToWrite;
            remainingBytes -= bytesToWrite;

            progCtx.ReportIncrement(sectorsToWrite);
        }

        await pendingWrite;
    }

    private async Task WritePadSectors(uint startSector, uint count, byte padByte, ProgressContext progCtx)
    {
        uint sectorsWritten = 0;
        byte[] buffer = new byte[BatchSectors * XDVDFS.SECTOR_SIZE];

        Array.Fill(buffer, padByte);

        while (sectorsWritten < count)
        {
            progCtx.Ct.ThrowIfCancellationRequested();

            int sectorsToWrite = (int)Math.Min(BatchSectors, count - sectorsWritten);
            int bytesToWrite = sectorsToWrite * XDVDFS.SECTOR_SIZE;

            await SectorSink.WriteSectorsAsync(
                startSector + sectorsWritten, 
                buffer.AsMemory(0, bytesToWrite), 
                progCtx.Ct);

            sectorsWritten += (uint)sectorsToWrite;
        }

        progCtx.ReportIncrement(count);
    }
}