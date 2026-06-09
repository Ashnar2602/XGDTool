namespace XGDTool.Lib.Image;

public class IWriterOptions
{
    public Format OutputFormat { get; set; } = Format.XISO;
    public IWriterType WriterType { get; set; } = IWriterType.Rewrite;
    public string OutDirectory { get; set; } = Environment.CurrentDirectory;
    public bool? Scrub { get; set; } = false;
    public bool? Split { get; set; } = null;
    public bool? RenameXbe { get; set; } = null;
    public string? RenameTo { get; set; } = null;
    public bool? AllowedMediaPatch { get; set; } = null;
}
