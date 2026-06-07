namespace XGDToolLib.Image;

public class IWriterOptions
{
    public Type OutputType { get; set; } = Type.XISO;
    public Converter.Type ConvertType { get; set; } = Converter.Type.Rewrite;
    public string OutDirectory { get; set; } = Environment.CurrentDirectory;
    public bool? Scrub { get; set; } = false;
    public bool? Split { get; set; } = null;
    public bool? RenameXbe { get; set; } = null;
    public string? RenameTo { get; set; } = null;
    public bool? AllowedMediaPatch { get; set; } = null;
}
