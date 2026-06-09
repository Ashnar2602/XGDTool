namespace XGDTool.Lib.Converter;

public static class Process
{
    public static async Task<IReadOnlyList<string>> ConvertEntry(Entry entry, IProgress<Progress>? progress = null, CancellationToken ct = default)
    {
        var reader = Image.IReader.Create(entry.InputFormat, entry.InputPaths);
        await reader.Initialize(progress, ct);

        var writer = Image.IWriter.Create(reader, entry);
        IReadOnlyList<string> ret;

        try
        {
            ret = await writer.Convert(progress, ct);
        }
        catch (Exception ex)
        {
            writer.CleanupCancelled();
            throw new Exception($"Failed to convert {entry.InputFormat} with paths: {string.Join(", ", entry.InputPaths)}", ex);
        }

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
