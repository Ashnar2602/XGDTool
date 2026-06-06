using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;
//using static XGDToolLib.Image.Writer;

namespace XGDToolLib.Image;

//public abstract class Writer(Reader reader, WriterOptions options, Title.Info titleTinfo)
//{
//    protected Reader Reader = reader;
//    protected WriterOptions OutOptions = options;
//    protected Title.Info TitleInfo = titleTinfo;

//    protected const int BUFFER_SIZE = 512 * XISO.SECTOR_SIZE;

//    protected virtual void Initialize() { }

//    public abstract Task<IReadOnlyList<string>> Convert(
//        IProgress<Converter.Progress>? progress = null, 
//        CancellationToken cancellationToken = default);

//    protected abstract void WriteSector(uint sector, ReadOnlySpan<byte> buffer);

//    protected static void EnsureOutputDirectory(string path)
//    {
//        if (!Directory.Exists(path))
//            Directory.CreateDirectory(path);

//        if (!Directory.Exists(path))
//            throw new IOException($"Failed to create output directory: {path}");
//    }
//}

public interface IWriter
{
    //public Task Initialize(IProgress<Converter.Progress>? progress = null, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<string>> Convert(IProgress<Converter.Progress>? progress = null, CancellationToken cancellationToken = default);

    protected static void EnsureOutputDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        if (!Directory.Exists(path))
            throw new IOException($"Failed to create output directory: {path}");
    }
}