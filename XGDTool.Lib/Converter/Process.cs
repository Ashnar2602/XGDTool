namespace XGDTool.Lib.Converter;

public static class Process
{
    public static async Task<IReadOnlyList<string>> ConvertEntry(
        InputEntry inEntry, 
        OutputOptions outOptions,
        IProgress<Progress>? progress = null, 
        CancellationToken ct = default)
    {
        var reader = Image.IReader.Create(inEntry.InputFormat, inEntry.InputPaths);
        await reader.Initialize(progress, ct);

        var writer = Image.IWriter.Create(reader, outOptions);
        IReadOnlyList<string> ret;

        try
        {
            ret = await writer.Convert(progress, ct);
        }
        catch (Exception ex)
        {
            writer.CleanupCancelled();
            throw new Exception($"Failed to convert {inEntry.InputFormat} with paths: {string.Join(", ", inEntry.InputPaths)}", ex);
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
