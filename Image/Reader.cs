using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Format;

namespace XGDTool.Image
{
    public abstract class Reader
    {
        //public enum InitStage
        //{
        //    ParseDirectories,
        //    LoadDataSectors,
        //    LoadSecuritySectors
        //}

        public class Directory : XISO.DirectoryEntry
        {
            public long RelativeOffset;
            public long LROffsetFromParent;
            public string Filepath = "";
        }

        public IReadOnlyList<string> Filepaths;
        public abstract long ImageOffset { get; protected set; }
        public abstract long TotalSectors { get; protected set; }
        public Platform Platform { get; private set; } = Platform.Unknown;
        public abstract Format.Image ImageType { get; }
        public HashSet<uint> DataSectors { get; private set; } = new();
        public List<Directory> DirectoryEntries { get; private set; } = new();
        public Directory ExecutableEntry { get; private set; } = new();

        protected Reader(IReadOnlyList<string> files)
        {
            Filepaths = new List<string>(files);
        }

        public Task InitializeDirectories()
        {
            InitializeType();
            LoadDirectoryEntries();

            if (DirectoryEntries.Count == 0)
                throw new InvalidDataException("No directory entries found in the image.");

            return Task.CompletedTask;
        }

        public Task InitializeSectors
        (
            IProgress<double>? dataSectorProgress = null,
            IProgress<double>? securitySectorProgress = null,
            CancellationToken cancelToken = default
        )
        {
            var retDs = LoadDataSectors(dataSectorProgress, cancelToken);
            if (cancelToken.IsCancellationRequested)
                return Task.FromCanceled(cancelToken);

            DataSectors = retDs.Result;
            var maxDataSector = DataSectors.Max();
            var securitySectors = new HashSet<uint>();

            if (Platform == Platform.OriginalXbox)
            {
                var retSs = LoadSecuritySectors(securitySectorProgress, cancelToken);
                if (cancelToken.IsCancellationRequested)
                    return Task.FromCanceled(cancelToken);

                securitySectors = retSs.Result;
            }

            if (securitySectors.Count == 0)
            {
                var startSector = (uint)(ImageOffset / XISO.SECTOR_SIZE);
                for (uint i = startSector; i < maxDataSector; i++)
                {
                    if (i % 100 == 0)
                    {
                        securitySectorProgress?.Report(
                            (double)(i - startSector) / 
                            (maxDataSector - startSector));
                    }

                    DataSectors.Add(i);
                }
            }
            else
            {
                DataSectors.UnionWith(securitySectors);
            }

            return Task.CompletedTask;
        }

        protected virtual void InitializeType() { }

        public abstract byte[] ReadSector(uint sector);

        public virtual byte[] ReadBytes(long offset, long size)
        {
            var bytes = new byte[size];
            var bytesRead = 0;

            while (bytesRead < size)
            {
                var sector = (offset + bytesRead) / XISO.SECTOR_SIZE;
                var sectorOffset = (offset + bytesRead) % XISO.SECTOR_SIZE;
                var toRead = Math.Min(size - bytesRead, XISO.SECTOR_SIZE - sectorOffset);
                var sectorData = ReadSector((uint)sector);
                Array.Copy(sectorData, sectorOffset, bytes, bytesRead, toRead);
                bytesRead += (int)toRead;
            }

            return bytes;
        }

        private void LoadDirectoryEntries()
        {
            var unprocessed = new List<Directory>();
            DirectoryEntries = new();

            {
                var rootEntry = new Directory();
                var rootStart = ImageOffset + XISO.MAGIC_OFFSET + XISO.MAGIC_SIZE;

                rootEntry.Header.FromBytes(ReadBytes(rootStart, rootEntry.Header.Size()));
                rootEntry.RelativeOffset = 0;
                rootEntry.LROffsetFromParent = rootEntry.Header.StartSector * XISO.SECTOR_SIZE;

                unprocessed.Add(rootEntry);
            }

            while (unprocessed.Count > 0)
            {
                var entry = unprocessed.First();
                unprocessed.RemoveAt(0);

                if (entry.LROffsetFromParent * 4 >= entry.Header.FileSize)
                    continue;

                var readEntry = new Directory();
                {
                    var currPos = ImageOffset + entry.RelativeOffset + (entry.LROffsetFromParent * 4);
                    readEntry.Header.FromBytes(ReadBytes(currPos, readEntry.Header.Size()));

                    var nameBytes = ReadBytes(currPos + readEntry.Header.Size(), readEntry.Header.NameLength);
                    readEntry.SetNameFromBytes(nameBytes);
                }

                if (readEntry.Header.LeftOffset == XISO.PAD_BYTE)
                    continue;

                if (readEntry.Header.LeftOffset != 0)
                {
                    entry.LROffsetFromParent = readEntry.Header.LeftOffset;
                    unprocessed.Add(entry);
                }

                if (readEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
                {
                    var dirEntry = readEntry;
                    dirEntry.RelativeOffset = 0;
                    dirEntry.LROffsetFromParent = dirEntry.Header.StartSector * XISO.SECTOR_SIZE;
                    dirEntry.Filepath = entry.Filepath + "/" + readEntry.GetName();

                    if (readEntry.Header.FileSize > 0)
                        unprocessed.Add(dirEntry);
                }
                else if (readEntry.Header.FileSize > 0)
                {
                    if (readEntry.GetName().Equals("default.xbe", StringComparison.OrdinalIgnoreCase))
                    {
                        ExecutableEntry = readEntry;
                        Platform = Format.Platform.OriginalXbox;
                    }
                    else if (readEntry.GetName().Equals("default.xex", StringComparison.OrdinalIgnoreCase))
                    {
                        ExecutableEntry = readEntry;
                        Platform = Format.Platform.Xbox360;
                    }

                    readEntry.Filepath = entry.Filepath + "/" + readEntry.GetName();
                    DirectoryEntries.Add(readEntry);
                }

                if (readEntry.Header.RightOffset != 0)
                {
                    entry.LROffsetFromParent = readEntry.Header.RightOffset;
                    unprocessed.Add(entry);
                }
            }

            if (string.IsNullOrEmpty(ExecutableEntry.GetName()) || Platform == Platform.Unknown)
                throw new InvalidDataException("Executable file was not found.");

            DirectoryEntries.Sort((a, b) =>
            {
                var aDir = a.Header.Attributes.HasFlag(XISO.DirAttribute.Directory);
                var bDir = b.Header.Attributes.HasFlag(XISO.DirAttribute.Directory);

                if (aDir != bDir)
                    return bDir.CompareTo(aDir);

                return string.Compare(a.Filepath, b.Filepath, StringComparison.OrdinalIgnoreCase);
            });
        }

        private async Task<HashSet<uint>> LoadDataSectors(IProgress<double>? progress = null, CancellationToken cancelToken = default)
        {
            var dataSectors = new HashSet<uint>();
            var unprocessed = new List<Directory>();
            long processedSize = 0;
            long totalSize = DirectoryEntries.Sum(e => e.Header.FileSize);

            uint sectorOffset = (uint)(ImageOffset / XISO.SECTOR_SIZE);
            uint headerSector = sectorOffset + (XISO.MAGIC_OFFSET / XISO.SECTOR_SIZE);

            dataSectors.Add(headerSector);
            dataSectors.Add(headerSector + 1);

            {
                var rootEntry = new Directory();
                var rootStart = ImageOffset + XISO.MAGIC_OFFSET + XISO.MAGIC_SIZE;

                rootEntry.Header.FromBytes(ReadBytes(rootStart, rootEntry.Header.Size()));
                rootEntry.RelativeOffset = 0;
                rootEntry.LROffsetFromParent = rootEntry.Header.StartSector * XISO.SECTOR_SIZE;

                unprocessed.Add(rootEntry);
            }

            while (unprocessed.Count > 0)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    dataSectors.Clear();
                    break;
                }

                var readEntry = new Directory();
                var entry = unprocessed.First();
                unprocessed.RemoveAt(0);

                {
                    var currOffset = ImageOffset + entry.RelativeOffset + (entry.LROffsetFromParent * 4);
                    var currSector = currOffset >> 11;
                    var totalSectors = (entry.Header.FileSize - (entry.LROffsetFromParent * 4) + 2047) >> 11;

                    //dataSectors.AddRange(Enumerable.Range((int)currSector, (int)totalSectors).Select(s => (uint)s));

                    for (var i = currSector; i < (currSector + totalSectors); i++)
                    {
                        dataSectors.Add((uint)i);
                    }

                    if (entry.LROffsetFromParent * 4 >= entry.Header.FileSize)
                        continue;

                    readEntry.Header.FromBytes(ReadBytes(currOffset, readEntry.Header.Size()));
                }

                if (readEntry.Header.LeftOffset == XISO.PAD_BYTE)
                    continue;

                if (readEntry.Header.LeftOffset != 0)
                {
                    entry.LROffsetFromParent = readEntry.Header.LeftOffset;
                    unprocessed.Add(entry);
                }

                if (readEntry.Header.Attributes.HasFlag(XISO.DirAttribute.Directory))
                {
                    if (readEntry.Header.FileSize > 0)
                    {
                        var dirEntry = readEntry;
                        dirEntry.RelativeOffset = 0;
                        dirEntry.LROffsetFromParent = entry.Header.StartSector * XISO.SECTOR_SIZE;
                        unprocessed.Add(dirEntry);
                    }
                }
                else
                {
                    if (readEntry.Header.FileSize > 0)
                    {
                        var startSector = sectorOffset + readEntry.Header.StartSector;
                        var endSector = startSector + ((readEntry.Header.FileSize + XISO.SECTOR_SIZE - 1) / XISO.SECTOR_SIZE);
                        //dataSectors.AddRange(Enumerable.Range((int)startSector, (int)(endSector - startSector)).Select(s => (uint)s));
                        for (var i = startSector; i < endSector; i++)
                        {
                            dataSectors.Add((uint)i);
                        }

                        processedSize += readEntry.Header.FileSize;
                        progress?.Report((double)processedSize / totalSize);
                    }
                }

                if (readEntry.Header.RightOffset != 0)
                {
                    entry.LROffsetFromParent = readEntry.Header.RightOffset;
                    unprocessed.Add(entry);
                }
            }

            progress?.Report(1);
            return dataSectors;
        }

        private async Task<HashSet<uint>> LoadSecuritySectors(IProgress<double>? progress = null, CancellationToken cancelToken = default)
        {
            var securitySectors = new HashSet<uint>();

            if (DataSectors.Count == 0)
            {
                throw new InvalidOperationException(
                    "Data sectors must be loaded before loading security sectors.");
            }
            else if ((TotalSectors != XISO.REDUMP_GAME_SECTORS) && 
                     (TotalSectors != XISO.REDUMP_TOTAL_SECTORS))
            {
                progress?.Report(1);
                return securitySectors;
            }

            var compareMode = false;
            uint sectorOffset = (uint)(ImageOffset / XISO.SECTOR_SIZE);
            bool flag = false;
            uint start = 0;
            const uint endSector = 0x345B60u;

            for (uint sectorIdx = 0; sectorIdx <= endSector; sectorIdx++)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    securitySectors.Clear();
                    break;
                }

                uint currSector = sectorOffset + sectorIdx;
                var sectorData = ReadSector(currSector);
                var isDataSector = DataSectors.Contains(currSector);
                var isEmptySector = sectorData.All(b => b == 0);

                if (isEmptySector && !flag && !isDataSector)
                {
                    start = currSector;
                    flag = true;
                }
                else if (!isEmptySector && flag)
                {
                    uint end = currSector - 1;
                    flag = false;

                    if (end - start == 0xFFFF)
                    {
                        for (uint i = start; i <= end; i++)
                        {
                            if (!DataSectors.Contains(i))
                                securitySectors.Add(i);
                        }
                    }
                    else if (compareMode && ((end - start) > 0xFFFF))
                    {
                        progress?.Report(1);
                        securitySectors.Clear();
                        return securitySectors;
                    }
                }

                progress?.Report((double)sectorIdx / endSector);
            }
            
            progress?.Report(1);
            return securitySectors;
        }
    }
}
