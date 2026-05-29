using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.IoStream
{
    public class In
    {
        private class Stream
        {
            public string Filepath;
            public StreamReader Reader;
            public long Size;

            public Stream(string filepath)
            {
                Filepath = filepath;
                Reader = new StreamReader(filepath);
                Size = new FileInfo(filepath).Length;
            }
        }

        public long Position => CurrentPosition;
        public long Length => TotalSize;

        private List<Stream> Streams = new();
        private readonly long TotalSize = 0;
        private long CurrentPosition = 0;
        private int CachedStreamIndex = 0;
        private long CachedStreamStartPosition = 0;

        public In(IReadOnlyList<string> files)
        {
            TotalSize = 0;
            CurrentPosition = 0;
            var sortedFiles = files.OrderBy(f => f).ToList();
            
            foreach (var filepath in sortedFiles)
            {
                if (!File.Exists(filepath))
                    throw new FileNotFoundException($"File not found: {filepath}");

                var stream = new Stream(filepath);
                TotalSize += stream.Size;
                Streams.Add(stream);
            }
        }

        ~In()
        {
            foreach (var stream in Streams)
            {
                stream.Reader.Close();
            }
        }

        public void Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
        {
            long targetPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => CurrentPosition + offset,
                SeekOrigin.End => TotalSize - offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid origin value")
            };

            if (targetPosition < 0 || targetPosition > TotalSize)
                throw new ArgumentOutOfRangeException(
                    nameof(offset), 
                    "Attempted to seek outside the bounds of the stream.");

            CurrentPosition = targetPosition;
        }

        public byte[] Read(long size)
        {
            if (CurrentPosition + size > TotalSize)
                throw new EndOfStreamException("Attempted to read beyond the end of the stream.");

            byte[] buffer = new byte[size];
            int bytesRead = 0;

            while (bytesRead < size)
            {
                var (stream, streamOffset) = FindStreamForPosition(CurrentPosition);
                stream.Reader.BaseStream.Seek(streamOffset, SeekOrigin.Begin);

                int bytesToRead = (int)Math.Min(
                    size - bytesRead,
                    stream.Size - streamOffset);

                int read = stream.Reader.BaseStream.Read(buffer, bytesRead, bytesToRead);

                if (read == 0)
                    throw new EndOfStreamException("Unexpected end of stream.");

                bytesRead += read;
                CurrentPosition += read;
            }

            return buffer;
        }

        public async Task<byte[]> ReadAsync(long size, CancellationToken cancelToken = default)
        {
            if (CurrentPosition + size > TotalSize)
                throw new EndOfStreamException("Attempted to read beyond the end of the stream.");

            byte[] buffer = new byte[size];
            int bytesRead = 0;

            while (bytesRead < size)
            {
                var (stream, streamOffset) = FindStreamForPosition(CurrentPosition);
                stream.Reader.BaseStream.Seek(streamOffset, SeekOrigin.Begin);

                int bytesToRead = (int)Math.Min(
                    size - bytesRead, 
                    stream.Size - streamOffset);

                int read = await stream.Reader.BaseStream.ReadAsync(
                    buffer.AsMemory(bytesRead, bytesToRead), 
                    cancelToken);

                if (read == 0)
                    throw new EndOfStreamException("Unexpected end of stream.");

                bytesRead += read;
                CurrentPosition += read;
            }

            return buffer;
        }

        private (Stream stream, long streamOffset) FindStreamForPosition(long position)
        {
            var cachedStream = Streams[CachedStreamIndex];
            if (position >= CachedStreamStartPosition && 
                position < CachedStreamStartPosition + cachedStream.Size)
            {
                return (cachedStream, position - CachedStreamStartPosition);
            }

            long accumSize = 0;
            for (int i = 0; i < Streams.Count; i++)
            {
                var stream = Streams[i];
                if (position < accumSize + stream.Size)
                {
                    CachedStreamIndex = i;
                    CachedStreamStartPosition = accumSize;
                    return (stream, position - accumSize);
                }
                accumSize += stream.Size;
            }

            throw new InvalidOperationException("Position outside stream bounds");
        }
    }
}
