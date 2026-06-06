using XGDToolLib.Exe;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;
using static XGDToolLib.Image.Writer;

namespace XGDToolLib.Image.Writers;

internal class Extract(Reader reader, Options options, Title.Info titleInfo) 
    : Writer(reader, options, titleInfo)
{
    private readonly byte[] ReadBuffer = new byte[BUFFER_SIZE];
    private string TitleDirectoryPath => Path.Join(OutOptions.OutDirectory, TitleInfo.FolderName);

    public override Task<IReadOnlyList<string>> Convert(
        IProgress<Converter.Progress>? progress, 
        CancellationToken cancellationToken)
    {
        EnsureOutputDirectory(TitleDirectoryPath);

        if (!Directory.Exists(TitleDirectoryPath))
        {
            throw new IOException(
                $"Failed to create title output directory: {TitleDirectoryPath}");
        }

        var progData = new Converter.Progress
        {
            Stage = Converter.Stage.WritingData,
            Current = 0,
            Total = Reader.DirectoryEntries.Sum(e => e.Header.FileSize)
        };

        foreach (var entry in Reader.DirectoryEntries)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<IReadOnlyList<string>>(cancellationToken);

            if (entry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
            {
                if (string.IsNullOrEmpty(entry.Filepath))
                    throw new Exception($"Invalid directory entry with empty filepath.");

                var outPath = Path.Join(TitleDirectoryPath, entry.Filepath);

                if (!Directory.Exists(outPath))
                    Directory.CreateDirectory(outPath);
            }
            else
            {
                if (string.IsNullOrEmpty(entry.Filepath))
                    throw new Exception($"Invalid file entry with empty filepath.");

                ExtractFile(entry, ref progData, progress, cancellationToken);
            }
        }

        progData.Current = progData.Total;
        progress?.Report(progData);

        return Task.FromResult<IReadOnlyList<string>>(new[] { TitleDirectoryPath });
    }

    private void ExtractFile(
        Reader.DirectoryEntry entry, 
        ref Converter.Progress progData,
        IProgress<Converter.Progress>? progress,
        CancellationToken cancellationToken)
    {
        byte[]? xbeCert = null;
        long certOffset = 0;
        long readStart = XISO.SectorToOffset(Reader.SectorOffset + entry.Header.StartSector);

        if (TitleInfo.Platform == Platform.OriginalXbox &&
            entry.Filepath.Equals("default.xbe", StringComparison.OrdinalIgnoreCase) &&
            (OutOptions.RenameXbe == true || OutOptions.AllowedMediaPatch == true))
        {
            xbeCert = TitleInfo.XbeCertificate.ToBytes();
            certOffset = TitleInfo.XbeCertificateOffset;
        }

        var outPath = Path.Join(TitleDirectoryPath, entry.Filepath);
        var outDir = Path.GetDirectoryName(outPath);

        if (string.IsNullOrEmpty(outDir))
            throw new Exception($"Invalid output path with empty directory: {outPath}");

        if (!Directory.Exists(outDir))
            EnsureOutputDirectory(outDir);

        using var fOut = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
        uint bytesRead = 0;
        int certBytesRemain = xbeCert?.Length ?? 0;
        var certPos = certOffset != 0 ? certOffset + readStart : 0;

        while (bytesRead < entry.Header.FileSize)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            
            int readLen = (int)Math.Min(ReadBuffer.Length, entry.Header.FileSize - bytesRead);
            var readPos = readStart + bytesRead;

            Reader.ReadBytes(readPos, ReadBuffer.AsSpan(0, readLen));

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
                        .CopyTo(ReadBuffer.AsSpan(buffOffset));
                    certBytesRemain -= certByteCount;
                }
            }

            fOut.Write(ReadBuffer.AsSpan(0, readLen));
            bytesRead += (uint)readLen;

            progData.Current += readLen;
            progress?.Report(progData);
        }
    }
}
