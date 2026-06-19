using System.Text;

namespace XGDTool.Lib.Image.Formats;

public static class FATX
{
    public const int FILENAME_CHARS_MAX = 42;
    
    private static readonly HashSet<char> InvalidFatChars =
    [
        '<', '>', ':', '"', '/', '\\', '|', '?', '*'
    ];

    public static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (InvalidFatChars.Contains(c))
                sb.Append('_');
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}
