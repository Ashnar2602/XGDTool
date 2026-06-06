using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;

namespace XGDToolLib.Converter;

public static class InputHelper
{
    public class Options
    {
        public string[] InputPaths { get; set; } = Array.Empty<string>();
        public string? OutputDirectory { get; set; } = null;
        public Image.Type OutputType { get; set; } = Image.Type.XISO;
        public Type ConvertType { get; set; } = Type.Passthrough;
        public bool? Split { get; set; } = null;
        public bool? GenAttachXbe { get; set; } = null;
        public bool? Rename { get; set; } = null;
        public string? NewName { get; set; } = null;
        public bool? AllowedMediaPatch { get; set; } = null;
        public string? IconPath { get; set; } = null;
    }

    public static List<Entry> GenerateEntries(Options options)
    {
        var entries = new List<Entry>();
        RecursiveDetect(options, 0, 1, ref entries);
        return entries;
    }

    private static void RecursiveDetect(Options options, int depth, int limit, ref List<Entry> entries)
    {
        if (depth > limit)
            return;

        foreach (var inPath in options.InputPaths)
        {
            var path = inPath;
            var type = DetectType(ref path, out var imageOffset);

            if (type == Image.Type.Unknown)
            {
                if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    var dirs = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    var files = dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly);

                    var newOptions = options;

                    foreach (var dir in dirs)
                    {
                        newOptions.InputPaths = new string[] { dir.FullName };
                        RecursiveDetect(newOptions, depth + 1, limit, ref entries);
                    }

                    foreach (var file in files)
                    {
                        newOptions.InputPaths = new string[] { file.FullName };
                        RecursiveDetect(newOptions, depth + 1, limit, ref entries);
                    }
                }
            }
            else
            {
                List<string> newPaths;

                if (type != Image.Type.Extract && type != Image.Type.GOD)
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

                entries.Add(new Entry
                {
                    ImageType = options.OutputType,
                    ConvertType = options.ConvertType,
                    OutDirectory = options.OutputDirectory ?? Environment.CurrentDirectory,
                    Split = options.Split,
                    RenameXbe = options.Rename,
                    RenameTo = options.NewName,
                    AllowedMediaPatch = options.AllowedMediaPatch,
                    InputPaths = newPaths,
                    InputType = type,
                    ImageOffset = imageOffset,
                    AttachXbe = options.GenAttachXbe
                });
            }
        }
    }

    private static void RecurseGodDirectory(string path, int depth, int limit, out string? outPath)
    {
        outPath = null;

        if (depth > limit)
            return;

        var dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
        var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);

        foreach (var dir in dirs)
            RecurseGodDirectory(dir, depth + 1, limit, out outPath);

        foreach (var file in files)
        {
            var parentDir = Path.GetDirectoryName(file);

            if (!string.IsNullOrEmpty(parentDir) &&
                file.StartsWith("Data", StringComparison.OrdinalIgnoreCase) &&
                parentDir.EndsWith(".data", StringComparison.OrdinalIgnoreCase))
            {
                outPath = parentDir;
                return;
            }
        }
    }

    public static Image.Type DetectType(ref string path, out long? imageOffset)
    {
        imageOffset = null;

        if (Directory.Exists(path))
        {
            if (Image.Readers.Extract.IsValid(path))
                return Image.Type.Extract;

            RecurseGodDirectory(path, 0, 2, out var newPath);

            if (!string.IsNullOrEmpty(newPath))
            {
                path = newPath;
                return Image.Type.GOD;
            }
        }
        else if (File.Exists(path))
        {
            if (Image.Readers.Xiso.IsValid(new[] { path }, out var offset))
                return Image.Type.XISO;
        }

        return Image.Type.Unknown;
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
