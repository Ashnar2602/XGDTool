using XGDTool.Lib.Image;

namespace XGDTool.Lib.Converter;

public class Entry : IWriterOptions
{
    public List<string> InputPaths { get; set; } = new();
    public Format InputFormat { get; set; } = Format.Unknown;
    public bool? AttachXbe { get; set; } = null;
}
