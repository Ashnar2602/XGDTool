using XGDTool.Lib.Image;

namespace XGDTool.Lib.Converter;

public static class InputHelper
{
    public static List<InputEntry> GenerateEntries(string[] inputPaths, int maxDepth = 1)
    {
        var entries = new List<InputEntry>();
        RecursiveDetect(inputPaths, 0, maxDepth, ref entries);
        return entries;
    }

    public static Task<List<InputEntry>> GenerateEntriesAsync(string[] inputPaths, int maxDepth = 1, CancellationToken ct = default)
    {
        var entries = new List<InputEntry>();
        RecursiveDetect(inputPaths, 0, maxDepth, ref entries, ct);
        return Task.FromResult(entries);
    }

    private static void RecursiveDetect(string[] inputPaths, int depth, int limit, ref List<InputEntry> entries, CancellationToken ct = default)
    {
        if (depth > limit)
            return;

        foreach (var inPath in inputPaths)
        {
            ct.ThrowIfCancellationRequested();

            var path = inPath;
            var type = DetectType(ref path, out var imageOffset);

            if (type == Format.Unknown)
            {
                if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    var dirs = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    var files = dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly);

                    var newInputPaths = inputPaths;

                    foreach (var dir in dirs)
                    {
                        newInputPaths = new string[] { dir.FullName };
                        RecursiveDetect(newInputPaths, depth + 1, limit, ref entries, ct);
                    }

                    foreach (var file in files)
                    {
                        newInputPaths = new string[] { file.FullName };
                        RecursiveDetect(newInputPaths, depth + 1, limit, ref entries, ct);
                    }
                }
            }
            else
            {
                List<string> newPaths;

                if (type != Format.Extract && type != Format.GOD)
                {
                    var parts = CollectFileParts(path);
                    if (parts == null)
                        continue;

                    newPaths = parts;
                }
                else
                {
                    newPaths = new List<string>() { path };
                }

                if (entries.Any(e => e.InputPaths.SequenceEqual(newPaths)))
                    continue;

                entries.Add(new InputEntry
                {
                    InputPaths = newPaths,
                    InputFormat = type,
                });
            }
        }
    }

    private static bool RecurseGodDirectory(string path, int depth, int limit, out string outPath)
    {
        outPath = "";

        if (depth > limit)
            return false;

        if (!Image.Reader.God.IsValid(path))
        {
            var dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
            foreach (var dir in dirs)
            {
                if (RecurseGodDirectory(dir, depth + 1, limit, out outPath))
                    return true;
            }
            return false;
        }

        outPath = path;
        return true;
    }

    public static Format DetectType(ref string path, out long? imageOffset)
    {
        imageOffset = null;

        if (Directory.Exists(path))
        {
            if (Image.Reader.Extract.IsValid(path))
                return Format.Extract;

            if (RecurseGodDirectory(path, 0, 3, out var newPath))
            {
                path = newPath;
                return Format.GOD;
            }
        }
        else if (File.Exists(path))
        {
            if (Image.Reader.Xiso.IsValid(path))
                return Format.XISO;
            else if (Image.Reader.Cci.IsValid(path))
               return Format.CCI;
            else if (Image.Reader.Zar.IsValid(path))
                return Format.ZAR;
        }

        return Format.Unknown;
    }

    public static List<string>? CollectFileParts(string path)
    {
        if (!File.Exists(path))
            return null;

        var baseName = Path.GetFileNameWithoutExtension(path);

        if (!char.IsDigit(baseName[^1]))
            return new() { path };

        
        var dir = Path.GetDirectoryName(path);
        if (dir == null)
            return new() { path };

        baseName = baseName.Substring(0, baseName.Length - 1);
        var files = Directory.GetFiles(dir, baseName + "*.*", SearchOption.TopDirectoryOnly);

        if (files.Length < 2)
            return new() { path };

        var parts = new List<string>();

        foreach (var file in files)
        {
            var fileBase = Path.GetFileNameWithoutExtension(file);

            if (fileBase.StartsWith(baseName) && char.IsDigit(fileBase[^1]))
                parts.Add(file);
        }

        parts.Sort();
        return parts;
    }
}
