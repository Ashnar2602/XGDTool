using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Exe;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Writers;

internal class Extract(IReader reader, IWriterOptions options, Title.Info titleInfo) : IWriter
{
    private readonly IReader Reader = reader;
    private readonly IWriterOptions Options = options;
    private readonly Title.Info TitleInfo = titleInfo;
    private const int BufferSectors = 256;
    private string TitleDirectoryPath => Path.Join(Options.OutputDirectory, TitleInfo.FolderName);

    public Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var dirEntries = Reader.DirectoryEntries;
        if (Options.SkipSystemUpdate == true && Reader.Platform == Platform.Xbox360)
        {
            dirEntries.RemoveAll(e => e.FilePath.StartsWith(
                XDVDFS.SYSTEM_UPDATE_DIRECTORY_NAME, 
                StringComparison.OrdinalIgnoreCase));
        }
        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.WritingData,
            Current = 0,
            Total = dirEntries.Where(e => !e.Attributes.HasFlag(XDVDFS.DirAttributes.Directory))
                .Sum(e => e.FileSize)
        };
        var readBuffer = new byte[BufferSectors * XDVDFS.SECTOR_SIZE];

        foreach (var entry in dirEntries)
        {
            ct.ThrowIfCancellationRequested();

            var outPath = Path.Join(TitleDirectoryPath, entry.FilePath);

            if (entry.Attributes.HasFlag(XDVDFS.DirAttributes.Directory))
            {
                Directory.CreateDirectory(outPath);
            }
            else
            {
                var outDir = 
                    Path.GetDirectoryName(outPath) ?? 
                    throw new InvalidOperationException("Invalid output directory.");

                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                byte[]? certBytes = null;
                long certOffset = 0;
                long readStart = XDVDFS.SectorToOffset(Reader.SectorOffset + (uint)entry.StartSector);

                if (TitleInfo.Platform == Platform.Xbox &&
                    entry.FilePath.Equals("default.xbe", StringComparison.OrdinalIgnoreCase) &&
                    (Options.RenameXbe == true || Options.AllowedMediaPatch == true))
                {
                    certBytes = TitleInfo.XbeCertificate.Serialize();
                    certOffset = TitleInfo.XbeCertificateOffset;
                }

                using var fOut = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
                uint bytesRead = 0;
                int certBytesRemain = certBytes?.Length ?? 0;
                var certPos = certOffset != 0 ? certOffset + readStart : 0;

                while (bytesRead < entry.FileSize)
                {
                    ct.ThrowIfCancellationRequested();

                    int readLen = (int)Math.Min(readBuffer.Length, entry.FileSize - bytesRead);
                    var readPos = readStart + bytesRead;

                    Reader.ReadBytes(readPos, readBuffer.AsSpan(0, readLen));

                    if (certBytes != null && certBytesRemain > 0)
                    {
                        var buffOffset = -1;
                        var certByteCount = 0;

                        if ((readPos <= certPos) && ((readPos + readLen) > certPos))
                            buffOffset = (int)(certPos - readPos);
                        else if ((readPos > certPos) && (readPos < (certPos + certBytes.Length)))
                            buffOffset = 0;

                        if (buffOffset > -1)
                        {
                            certByteCount = Math.Min(certBytesRemain, readLen - buffOffset);
                            certBytes
                                .AsSpan(certBytes.Length - certBytesRemain, certByteCount)
                                .CopyTo(readBuffer.AsSpan(buffOffset));
                            certBytesRemain -= certByteCount;
                        }
                    }

                    fOut.Write(readBuffer.AsSpan(0, readLen));
                    bytesRead += (uint)readLen;

                    progData.Current += readLen;
                    progress?.Report(progData);
                }
            }
        }

        progData.Current = progData.Total;
        progress?.Report(progData);

        return Task.FromResult<IReadOnlyList<string>>([ TitleDirectoryPath ]);
    }

    public void CleanupCancelled()
    {
        if (Directory.Exists(TitleDirectoryPath))
        {
            try
            {
                Directory.Delete(TitleDirectoryPath, true);
            }
            catch { }
        }
    }
}
