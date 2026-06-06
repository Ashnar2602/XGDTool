using System.Text.Json.Serialization;

namespace XGDToolLib.Title;

public class RepackEntry
{
    [JsonPropertyName("List")]
    public string List { get; set; } = "";

    [JsonPropertyName("Title ID")]
    public string TitleId { get; set; } = "";

    [JsonPropertyName("Title Name")]
    public string TitleName { get; set; } = "";

    [JsonPropertyName("Version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("Region")]
    public string Region { get; set; } = "";

    [JsonPropertyName("Letter")]
    public string Letter { get; set; } = "";

    [JsonPropertyName("XBE Title")]
    public string XbeTitle { get; set; } = "";

    [JsonPropertyName("Folder Name")]
    public string FolderName { get; set; } = "";

    [JsonPropertyName("ISO Name")]
    public string IsoName { get; set; } = "";

    [JsonPropertyName("ISO Checksum")]
    public string IsoChecksum { get; set; } = "";

    [JsonPropertyName("Process")]
    public string Process { get; set; } = "";

    [JsonPropertyName("Scrub")]
    public string Scrub { get; set; } = "";

    [JsonPropertyName("Category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("Info")]
    public string Info { get; set; } = "";
}
