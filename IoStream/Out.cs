using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.IoStream
{
    public class Out
    {
        private class Stream
        {
            public string Filepath;
            public FileStream Writer;
            public long Size;

            public Stream(string filepath)
            {
                Filepath = filepath;
                Writer = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                Size = Writer.Length;
            }
        }

        public long Position => CurrentPosition;
        public long Length => GetTotalSize();

        private List<Stream> Streams = new();
        private readonly long? MaxSliceSize = null;
        private readonly string BasePath;
        private long CurrentPosition = 0;
        private bool FirstRenamed = false;
        private int CachedStreamIndex = 0;
        private long CachedStreamStartPosition = 0;

        public Out(string basePath, long? maxSliceSize = null)
        {
            if (String.IsNullOrEmpty(basePath))
                throw new ArgumentNullException(nameof(basePath));

            if (maxSliceSize.HasValue && maxSliceSize.Value <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maxSliceSize), 
                    "Max slice size must be greater than zero"
                );

            BasePath = basePath;
            MaxSliceSize = maxSliceSize;
        }

        ~Out()
        {
            foreach (var stream in Streams)
            {
                stream.Writer.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var stream in Streams)
            {
                stream.Writer.Dispose();
            }
            Streams.Clear();
            GC.SuppressFinalize(this);
        }

        public void Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
        {
            long targetPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => CurrentPosition + offset,
                SeekOrigin.End => GetTotalSize() - offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid origin value")
            };

            if (targetPosition < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(offset), 
                    "Target position cannot be negative"
                );

            CurrentPosition = targetPosition;
        }

        public void Write(byte[] data)
        {
            if (!MaxSliceSize.HasValue)
            {
                if (Streams.Count == 0)
                    CreateNewStream(0);

                var stream = Streams[0];
                stream.Writer.Seek(CurrentPosition, SeekOrigin.Begin);
                stream.Writer.Write(data, 0, data.Length);
                CurrentPosition += data.Length;
                stream.Size = Math.Max(stream.Size, CurrentPosition);
                return;
            }

            var buffPos = 0;

            while (buffPos < data.Length)
            {
                var (stream, streamOffset) = FindStreamForPosition(CurrentPosition);
                
                var writeSize = (int)Math.Min(
                    data.Length - buffPos, 
                    MaxSliceSize.Value - streamOffset
                );

                stream.Writer.Seek(streamOffset, SeekOrigin.Begin);
                stream.Writer.Write(data, buffPos, writeSize);
                
                buffPos += writeSize;
                CurrentPosition += writeSize;
                stream.Size = Math.Max(stream.Size, stream.Writer.Position);
            }
        }

        public async Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (!MaxSliceSize.HasValue)
            {
                if (Streams.Count == 0)
                    CreateNewStream(0);

                var stream = Streams[0];
                stream.Writer.Seek(CurrentPosition, SeekOrigin.Begin);
                await stream.Writer.WriteAsync(data, 0, data.Length, cancellationToken);
                CurrentPosition += data.Length;
                stream.Size = Math.Max(stream.Size, CurrentPosition);
                return;
            }

            var buffPos = 0;

            while (buffPos < data.Length)
            {
                var (stream, streamOffset) = FindStreamForPosition(CurrentPosition);
                
                var writeSize = (int)Math.Min(
                    data.Length - buffPos, 
                    MaxSliceSize.Value - streamOffset
                );

                stream.Writer.Seek(streamOffset, SeekOrigin.Begin);
                await stream.Writer.WriteAsync(data.AsMemory(buffPos, writeSize), cancellationToken);
                
                buffPos += writeSize;
                CurrentPosition += writeSize;
                stream.Size = Math.Max(stream.Size, stream.Writer.Position);
            }
        }

        private (Stream stream, long streamOffset) FindStreamForPosition(long position)
        {
            if (!MaxSliceSize.HasValue)
            {
                if (Streams.Count == 0)
                    CreateNewStream(0);
                return (Streams[0], position);
            }

            var streamIndex = (int)(position / MaxSliceSize.Value);
            
            // Fast path: check cached stream
            if (streamIndex == CachedStreamIndex && CachedStreamIndex < Streams.Count)
            {
                return (Streams[CachedStreamIndex], position - CachedStreamStartPosition);
            }

            // Ensure stream exists
            if (streamIndex >= Streams.Count)
                CreateNewStream(streamIndex);

            // Update cache
            CachedStreamIndex = streamIndex;
            CachedStreamStartPosition = streamIndex * MaxSliceSize.Value;

            return (Streams[streamIndex], position - CachedStreamStartPosition);
        }

        private string GetNewPath(int index)
        {
            string? dir = Path.GetDirectoryName(BasePath);
            string name = Path.GetFileNameWithoutExtension(BasePath);
            string ext = Path.GetExtension(BasePath);
            return Path.Combine(dir ?? string.Empty, $"{name}.{index+1}.{ext}");
        }

        private void CreateNewStream(int index)
        {
            if (index > 0 && !FirstRenamed && Streams.Count > 0)
            {
                var stream = Streams.First();
                var newPath = GetNewPath(0);

                stream.Writer.Dispose();
                File.Move(stream.Filepath, newPath);
                stream.Filepath = newPath;
                stream.Writer = new FileStream(newPath, FileMode.Open, FileAccess.Write, FileShare.None);
                FirstRenamed = true;
            }

            for (int i = Streams.Count; i <= index; i++)
            {
                string filepath = i == 0 ? BasePath : GetNewPath(i);
                var stream = new Stream(filepath);
                Streams.Add(stream);

                if (i < index && MaxSliceSize.HasValue)
                {
                    stream.Writer.SetLength(MaxSliceSize.Value);
                    stream.Size = MaxSliceSize.Value;
                }
            }
        }

        private long GetTotalSize()
        {
            if (Streams.Count == 0)
                return 0;

            if (!MaxSliceSize.HasValue)
                return Streams[0].Size;

            long totalSize = 0;
            for (int i = 0; i < Streams.Count - 1; i++)
            {
                totalSize += Math.Min(Streams[i].Size, MaxSliceSize.Value);
            }
            totalSize += Streams.Last().Size;
            return totalSize;
        }
    }
}
