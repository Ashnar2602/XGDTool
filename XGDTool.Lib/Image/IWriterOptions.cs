namespace XGDTool.Lib.Image;

public class IWriterOptions
{
    public required Format OutputFormat { get; set; }
    public required IWriterType WriterType { get; set; }
    public required string OutputDirectory { get; set; }
    public bool? Scrub { get; set; } = false;
    public bool? Split { get; set; } = null;
    public bool? RenameXbe { get; set; } = null;
    public string? RenameTo { get; set; } = null;
    public bool? AllowedMediaPatch { get; set; } = null;
    public bool? SkipSystemUpdate { get; set; } = null;
    public string? IconPath { get; set; } = null;
}
