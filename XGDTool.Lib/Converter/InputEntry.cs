using XGDTool.Lib.Image;

namespace XGDTool.Lib.Converter;

public class InputEntry
{
    public List<string> InputPaths { get; set; } = new();
    public Format InputFormat { get; set; } = Format.Unknown;
}
