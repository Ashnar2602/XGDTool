using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Lib.Image;

namespace XGDTool.Lib.Converter;

public static class InputHelper
{
    public class Options
    {
        public string[] InputPaths { get; set; } = Array.Empty<string>();
        public string? OutputDirectory { get; set; } = null;
        public Format OutputFormat { get; set; } = Format.XISO;
        public IWriterType WriterType { get; set; } = IWriterType.Rewrite;
        public bool? Scrub { get; set; } = null;
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

            if (type == Format.Unknown)
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

                entries.Add(new Entry
                {
                    OutputFormat = options.OutputFormat,
                    WriterType = options.WriterType,
                    OutDirectory = options.OutputDirectory ?? Environment.CurrentDirectory,
                    Scrub = options.Scrub,
                    Split = options.Split,
                    RenameXbe = options.Rename,
                    RenameTo = options.NewName,
                    AllowedMediaPatch = options.AllowedMediaPatch,
                    InputPaths = newPaths,
                    InputFormat = type,
                    AttachXbe = options.GenAttachXbe
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

            if (RecurseGodDirectory(path, 0, 2, out var newPath))
            {
                path = newPath;
                return Format.GOD;
            }
        }
        else if (File.Exists(path))
        {
            if (Image.Reader.Xiso.IsValid(path))
                return Format.XISO;
            //else if (Image.Reader.Cci.IsValid(path))
            //    return Image.Type.CCI;
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
