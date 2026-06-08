namespace XGDTool.Lib.Converter;

public class Entry : Image.IWriterOptions
{
    public List<string> InputPaths { get; set; } = new();
    public Image.Type InputType { get; set; } = Image.Type.Unknown;
    public bool? AttachXbe { get; set; } = null;
}
