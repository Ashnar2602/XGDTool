using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;
using XGDToolLib.Exe;
using XGDToolLib.Util;

namespace XGDToolLib.Image.Writer;

internal class Extract : IWriter
{
    private readonly IReader Reader;
    private readonly IWriterOptions Options;
    private readonly Title.Info TitleInfo;
    private const int BufferSectors = 256;
    private string TitleDirectoryPath => Path.Join(Options.OutDirectory, TitleInfo.FolderName);

    public Extract(IReader reader, IWriterOptions options, Title.Info titleInfo)
    {
        Reader = reader;
        Options = options;
        TitleInfo = titleInfo;
    }

    public async Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken ct = default)
    {
        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.WritingData,
            Current = 0,
            Total = Reader.DirectoryEntries.Sum(e => e.Header.FileSize)
        };
        var readBuffer = new byte[BufferSectors * XISO.SECTOR_SIZE];

        foreach (var entry in Reader.DirectoryEntries)
        {
            ct.ThrowIfCancellationRequested();

            var outPath = Path.Join(TitleDirectoryPath, entry.Filepath);

            if (entry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                Directory.CreateDirectory(outPath);
            }
            else
            {
                var outDir = Path.GetDirectoryName(outPath);

                if (outDir == null)
                    throw new InvalidOperationException("Invalid output directory.");

                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                byte[]? xbeCert = null;
                long certOffset = 0;
                long readStart = XISO.SectorToOffset(Reader.SectorOffset + entry.Header.StartSector);

                if (TitleInfo.Platform == Platform.OriginalXbox &&
                    entry.Filepath.Equals("default.xbe", StringComparison.OrdinalIgnoreCase) &&
                    (Options.RenameXbe == true || Options.AllowedMediaPatch == true))
                {
                    xbeCert = MarshalableExt.ToBytes(TitleInfo.XbeCertificate);
                    certOffset = TitleInfo.XbeCertificateOffset;
                }

                using var fOut = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
                uint bytesRead = 0;
                int certBytesRemain = xbeCert?.Length ?? 0;
                var certPos = certOffset != 0 ? certOffset + readStart : 0;

                while (bytesRead < entry.Header.FileSize)
                {
                    ct.ThrowIfCancellationRequested();

                    int readLen = (int)Math.Min(readBuffer.Length, entry.Header.FileSize - bytesRead);
                    var readPos = readStart + bytesRead;

                    Reader.ReadBytes(readPos, readBuffer.AsSpan(0, readLen));

                    if (xbeCert != null && certBytesRemain > 0)
                    {
                        var buffOffset = -1;
                        var certByteCount = 0;

                        if ((readPos <= certPos) && ((readPos + readLen) > certPos))
                            buffOffset = (int)(certPos - readPos);
                        else if ((readPos > certPos) && (readPos < (certPos + xbeCert.Length)))
                            buffOffset = 0;

                        if (buffOffset > -1)
                        {
                            certByteCount = Math.Min(certBytesRemain, readLen - buffOffset);
                            xbeCert
                                .AsSpan(xbeCert.Length - certBytesRemain, certByteCount)
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
        return new[] { TitleDirectoryPath };
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
