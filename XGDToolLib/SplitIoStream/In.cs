//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace XGDToolLib.SplitIoStream
//{
//    public class In
//    {
//        private readonly struct ReadSegment
//        {
//            public readonly Stream Stream;
//            public readonly long StreamOffset;
//            public readonly int BufferOffset;
//            public readonly int Count;

//            public ReadSegment(Stream stream, long streamOffset, int bufferOffset, int count)
//            {
//                Stream = stream;
//                StreamOffset = streamOffset;
//                BufferOffset = bufferOffset;
//                Count = count;
//            }
//        }

//        private class Stream
//        {
//            public string Filepath;
//            public FileStream Reader;
//            public long Size;

//            public Stream(string filepath)
//            {
//                Filepath = filepath;
//                Reader = new FileStream(filepath, FileMode.Open, FileAccess.Read);
//                Size = new FileInfo(filepath).Length;
//            }
//        }

//        public long Position => CurrentPosition;
//        public long Length => TotalSize;

//        private readonly List<Stream> Streams = new();
//        private readonly long TotalSize = 0;
//        private long CurrentPosition = 0;
//        private int CachedStreamIndex = 0;
//        private long CachedStreamStartPosition = 0;

//        public In(IReadOnlyList<string> files)
//        {
//            TotalSize = 0;
//            CurrentPosition = 0;
//            var sortedFiles = files.OrderBy(f => f).ToList();
            
//            foreach (var filepath in sortedFiles)
//            {
//                if (!File.Exists(filepath))
//                    throw new FileNotFoundException($"File not found: {filepath}");

//                var stream = new Stream(filepath);
//                TotalSize += stream.Size;
//                Streams.Add(stream);
//            }
//        }

//        ~In()
//        {
//            foreach (var stream in Streams)
//            {
//                stream.Reader.Close();
//            }
//        }

//        public void Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
//        {
//            long targetPosition = origin switch
//            {
//                SeekOrigin.Begin => offset,
//                SeekOrigin.Current => CurrentPosition + offset,
//                SeekOrigin.End => TotalSize - offset,
//                _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid origin value")
//            };

//            if (targetPosition < 0 || targetPosition > TotalSize)
//                throw new ArgumentOutOfRangeException(
//                    nameof(offset), 
//                    "Attempted to seek outside the bounds of the stream.");

//            CurrentPosition = targetPosition;
//        }

//        public int Read(int size, Span<byte> buffer)
//        {
//            if (CurrentPosition + size > TotalSize)
//                throw new EndOfStreamException("Attempted to read beyond the end of the stream.");

//            if (buffer.Length < size)
//                throw new ArgumentException($"Buffer must be at least {size} bytes in size.", nameof(buffer));

//            int bytesRead = 0;

//            while (bytesRead < size)
//            {
//                var (stream, streamOffset) = FindStreamForPosition(CurrentPosition);
//                stream.Reader.Seek(streamOffset, SeekOrigin.Begin);

//                int bytesToRead = (int)Math.Min(
//                    size - bytesRead,
//                    stream.Size - streamOffset);

//                int read = stream.Reader.Read(buffer.Slice(bytesRead, bytesToRead));
//                if (read == 0)
//                    throw new EndOfStreamException("Unexpected end of stream.");

//                bytesRead += read;
//                CurrentPosition += read;
//            }

//            return bytesRead;
//        }

//        //public byte[] Read(int size)
//        //{
//        //    byte[] buffer = new byte[size];
//        //    var bytesRead = Read(size, ref buffer);

//        //    if (bytesRead < size)
//        //        Array.Resize(ref buffer, bytesRead);

//        //    return buffer;
//        //}

//        public async Task<int> ReadAsync(long offset, Memory<byte> buffer, CancellationToken cancelToken = default)
//        {
//            long size = buffer.Length;

//            if (offset < 0)
//                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");

//            if (offset + size > TotalSize)
//                throw new EndOfStreamException("Attempted to read beyond the end of the stream.");

//            int totalToRead = buffer.Length;
//            var segments = new List<ReadSegment>();
//            long currentOffset = offset;
//            int bufferOffset = 0;
//            long remaining = size;

//            while (remaining > 0)
//            {
//                var (stream, streamOffset) = FindStreamForPosition(currentOffset);

//                int bytesToRead = (int)Math.Min(
//                    remaining,
//                    stream.Size - streamOffset);

//                segments.Add(new ReadSegment(stream, streamOffset, bufferOffset, bytesToRead));

//                bufferOffset += bytesToRead;
//                currentOffset += bytesToRead;
//                remaining -= bytesToRead;
//            }

//            await Task.WhenAll(segments.Select(segment => ReadExactAtAsync(
//                segment.Stream.Reader.SafeFileHandle,
//                buffer.Slice(segment.BufferOffset, segment.Count),
//                segment.StreamOffset,
//                cancelToken)));

//            return totalToRead;
//        }

//        //public async Task<byte[]> ReadAsync(long offset, long size, CancellationToken cancelToken = default)
//        //{
//        //    if (size < 0 || size > int.MaxValue)
//        //        throw new ArgumentOutOfRangeException(nameof(size), "ReadAsync size must be between 0 and Int32.MaxValue.");

//        //    byte[] buffer = new byte[(int)size];
//        //    await ReadAsync(offset, buffer.AsMemory(), cancelToken);

//        //    return buffer;
//        //}

//        private static async Task ReadExactAtAsync(
//            Microsoft.Win32.SafeHandles.SafeFileHandle handle,
//            Memory<byte> destination,
//            long fileOffset,
//            CancellationToken cancelToken)
//        {
//            int totalRead = 0;

//            while (totalRead < destination.Length)
//            {
//                int read = await RandomAccess.ReadAsync(
//                    handle,
//                    destination[totalRead..],
//                    fileOffset + totalRead,
//                    cancelToken);

//                if (read == 0)
//                    throw new EndOfStreamException("Unexpected end of stream.");

//                totalRead += read;
//            }
//        }

//        private (Stream stream, long streamOffset) FindStreamForPosition(long position)
//        {
//            var cachedStream = Streams[CachedStreamIndex];
//            if (position >= CachedStreamStartPosition && 
//                position < CachedStreamStartPosition + cachedStream.Size)
//            {
//                return (cachedStream, position - CachedStreamStartPosition);
//            }

//            long accumSize = 0;
//            for (int i = 0; i < Streams.Count; i++)
//            {
//                var stream = Streams[i];
//                if (position < accumSize + stream.Size)
//                {
//                    CachedStreamIndex = i;
//                    CachedStreamStartPosition = accumSize;
//                    return (stream, position - accumSize);
//                }
//                accumSize += stream.Size;
//            }

//            throw new InvalidOperationException("Position outside stream bounds");
//        }
//    }
//}
