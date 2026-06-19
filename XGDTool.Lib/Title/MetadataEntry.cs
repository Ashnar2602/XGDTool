using System.Text.Json.Serialization;

namespace XGDTool.Lib.Title;

public class MetaDataArray
{
    [JsonPropertyName("Items")]
    public List<MetaDataEntry> Items { get; set; } = [];
    
    [JsonPropertyName("Count")]
    public int Count;
    
    [JsonPropertyName("Filter")]
    public string Filter = "";
    
    [JsonPropertyName("Category")]
    public string Category = "";
    
    [JsonPropertyName("Sort")]
    public string Sort = "";
    
    [JsonPropertyName("Direction")]
    public string Direction = "";
}

public class MetaDataEntry
{
    [JsonPropertyName("TitleID")]
    public string TitleId { get; set; } = "";

    [JsonPropertyName("HBTitleID")]
    public string HBTitleID { get; set; } = "";

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("LinkEnabled")]
    public string LinkEnabled { get; set; } = "";

    [JsonPropertyName("TitleType")]
    public string TitleType { get; set; } = "";

    [JsonPropertyName("Covers")]
    public string Covers { get; set; } = "";

    [JsonPropertyName("Updates")]
    public string Updates { get; set; } = "";

    [JsonPropertyName("MediaIDCount")]
    public string MediaIDCount { get; set; } = "";

    [JsonPropertyName("UserCount")]
    public string UserCount { get; set; } = "";

    [JsonPropertyName("NewestContent")]
    public string NewestContent { get; set; } = "";
}