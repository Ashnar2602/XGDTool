namespace XGDToolLib.Converter;

public static class Process
{
    public static async Task<IReadOnlyList<string>> ConvertEntry(
        Entry entry,
        IProgress<Progress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        var reader = Image.ReaderFactory.Create(entry.InputPaths);

        await reader.Initialize(progress, cancellationToken);

        var writer = Image.IWriterFactory.Create(reader, entry);
        writer.Initialize();

        var ret = await writer.Convert(progress, cancellationToken);

        //if (entry.AttachXbe == true)
        //{
        //    var xbePath = reader.GetXbePath();
        //    if (xbePath != null)
        //    {
        //        ret.Add(xbePath);
        //    }
        //}

        return ret;
    }
}
