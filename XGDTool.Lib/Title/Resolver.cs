using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XGDTool.Lib.Exe;
using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Title;

public static class Resolver
{
    private static string XboxOriginalJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "Repackinator", "RepackList.json");

    private static string Xbox360JsonPath =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "XboxUnity-Scraper", "metadata.json");

    private static readonly Lazy<Dictionary<
        (uint TitleId, uint Version, XBE.Region Region), RepackEntry>> XboxOgByIdMain = new(() =>
        {
            if (!File.Exists(XboxOriginalJsonPath))
                return [];
            var entries = JsonSerializer.Deserialize<List<RepackEntry>>(
                File.ReadAllText(XboxOriginalJsonPath)) ?? [];
            return entries
                .Where(e => 
                    !string.IsNullOrEmpty(e.TitleId) && 
                    e.List.Equals("Main", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(e => 
                    (Convert.ToUInt32(e.TitleId, 16), 
                     Convert.ToUInt32(e.Version, 16),
                     XbeRegionFromString(e.Region)));
        });

    //private static readonly Lazy<Dictionary<
    //    (uint TitleId, uint Version, XBE.Region Region), RepackEntry>> XboxOgByIdAlt = new(() =>
    //    {
    //        var entries = JsonSerializer.Deserialize<List<RepackEntry>>(
    //            File.ReadAllText(XboxOriginalJsonPath)) ?? [];

    //        //var duplicates = entries
    //        //    .Where(e =>
    //        //        !string.IsNullOrEmpty(e.TitleId) &&
    //        //        e.List.Equals("Alt", StringComparison.OrdinalIgnoreCase))
    //        //    .GroupBy(e => (
    //        //        TitleId: Convert.ToUInt32(e.TitleId, 16),
    //        //        Version: Convert.ToUInt32(e.Version, 16),
    //        //        Region: XbeRegionFromString(e.Region)))
    //        //    .Where(g => g.Count() > 1);

    //        //foreach (var g in duplicates)
    //        //{
    //        //    Console.WriteLine($"{g.Key.TitleId:X8} {g.Key.Version:X8} {g.Key.Region}");

    //        //    foreach (var e in g)
    //        //        Console.WriteLine($"  {e.TitleName} | {e.IsoName} | {e.IsoChecksum}");
    //        //}

    //        return entries
    //            .Where(e =>
    //                !string.IsNullOrEmpty(e.TitleId) &&
    //                e.List.Equals("Alt", StringComparison.OrdinalIgnoreCase))
    //            .ToDictionary(e =>
    //                (Convert.ToUInt32(e.TitleId, 16),
    //                 Convert.ToUInt32(e.Version, 16),
    //                 XbeRegionFromString(e.Region)));
    //    });

    private static readonly Lazy<Dictionary<uint, MetaDataEntry>> Xbox360ById = new(() =>
        {
            if (!File.Exists(Xbox360JsonPath))
                return [];
            var root = JsonSerializer.Deserialize<MetaDataArray>(
                File.ReadAllText(Xbox360JsonPath)) ?? new MetaDataArray();
            return root.Items.ToDictionary(e => Convert.ToUInt32(e.TitleId, 16));
        });

    public static Info Resolve(Image.IReader reader)
    {
        var headerTool = IHeaderTool.Create(reader.Platform);
        headerTool.Initialize(reader);

        if (headerTool is Exe.HeaderTools.Xex xexTool)
            return InfoFromXbox360(reader, xexTool);
        else if (headerTool is Exe.HeaderTools.Xbe xbeTool)
            return InfoFromXboxOriginal(reader, xbeTool);
        else
            throw new NotSupportedException($"Unsupported platform: {headerTool.Platform}");
    }

    private static Info InfoFromXbox360(Image.IReader reader, Exe.HeaderTools.Xex headerTool)
    {
        var exe = headerTool.ExecutionInfo;
        string name;

        if (!Xbox360ById.Value.TryGetValue(exe.TitleId, out var e) || e == null)
            name = GetNameFromFile(reader);
        else
            name = e.Name;

        var info = new Info(headerTool);
        var baseFsName = FATX.SanitizeFileName(name);

        info.TitleName = name;
        info.FolderName = baseFsName;
        info.ImageName = baseFsName;
        info.GodFolderName = baseFsName;

        var titleIdStr = " [" + info.TitleId.ToString("X8") + "]";
        var discStr = (exe.DiscCount > 1) 
            ? ("(Disc " + exe.DiscNumber.ToString() + ")") 
            : string.Empty;

        if (info.FolderName.Length + discStr.Length > FATX.FILENAME_CHARS_MAX)
            info.FolderName = info.FolderName[0..^discStr.Length];

        if (info.ImageName.Length + discStr.Length > FATX.FILENAME_CHARS_MAX - 4 - 2)
            info.ImageName = info.ImageName[0..^(4 + 2 + discStr.Length)];

        if (info.GodFolderName.Length + titleIdStr.Length > FATX.FILENAME_CHARS_MAX)
            info.GodFolderName = info.GodFolderName[0..^titleIdStr.Length];

        info.FolderName += discStr;
        info.ImageName += discStr;
        info.GodFolderName += titleIdStr;
        info.GodUniqueName = CreateGodUniqueName(exe);

        return info;
    }

    private static Info InfoFromXboxOriginal(Image.IReader reader, Exe.HeaderTools.Xbe headerTool)
    {
        var cert = headerTool.CertificateHeader;
        var key = (cert.TitleID, cert.Version, cert.GameRegion);

        if (!XboxOgByIdMain.Value.TryGetValue(key, out var e) || e == null)
            e = GenerateRepackEntry(reader, cert);

        var info = new Info(headerTool);
        info.TitleName = e.XbeTitle;
        info.FolderName = FATX.SanitizeFileName(e.FolderName);
        info.ImageName = FATX.SanitizeFileName(e.IsoName);

        var baseName = FATX.SanitizeFileName(info.TitleName.Split(" (")[0]);
        var titleIdStr = " [" + info.TitleId.ToString("X8") + "]";

        if (baseName.Length + titleIdStr.Length > 31)
            baseName = baseName[0..31];

        info.GodFolderName = baseName + titleIdStr;
        info.GodUniqueName = CreateGodUniqueName(info.XexExecutionInfo);

        return info;
    }

    private static string GetNameFromFile(Image.IReader reader)
    {
        return reader.ImageFormat == Image.Format.Extract
            ? Path.GetFileName(reader.FilePaths.First())
            : Path.GetFileNameWithoutExtension(reader.FilePaths.First());
    }

    private static RepackEntry GenerateRepackEntry(Image.IReader reader, XBE.CertificateHeader cert)
    {
        var e = new RepackEntry();
        e.Region = StringFromXbeRegion(cert.GameRegion);

        var rSuffix = !string.IsNullOrEmpty(e.Region)
            ? " (" + e.Region + ")"
            : "";

        var rawName = GetNameFromFile(reader).Split(" (")[0];

        e.XbeTitle = rawName;
        e.FolderName = FATX.SanitizeFileName(rawName);
        e.IsoName = e.FolderName;

        if (e.XbeTitle.Length + rSuffix.Length > XBE.TITLE_NAME_CHARS_MAX)
            e.XbeTitle = rawName[0..^rSuffix.Length];

        if (e.FolderName.Length + rSuffix.Length > FATX.FILENAME_CHARS_MAX)
            e.FolderName = rawName[0..^rSuffix.Length];

        if (e.IsoName.Length + rSuffix.Length > FATX.FILENAME_CHARS_MAX - 4 - 2)
            e.IsoName = rawName[0..^(4 + 2 + rSuffix.Length)];

        e.XbeTitle += rSuffix;
        e.FolderName += rSuffix;
        e.IsoName += rSuffix;

        return e;
    }

    private static string CreateGodUniqueName(XEX.ExecutionInfo exeInfo)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(exeInfo.TitleId);
        bw.Write(exeInfo.MediaId);
        bw.Write(exeInfo.DiscNumber);
        bw.Write(exeInfo.DiscCount);

        byte[] hash = SHA1.HashData(ms.ToArray());

        var sb = new StringBuilder();

        foreach (byte b in hash)
            sb.Append(b.ToString("X2"));

        return sb.ToString();
    }

    private static XBE.Region XbeRegionFromString(string regionStr)
    {
        if (regionStr.Equals("USA", StringComparison.OrdinalIgnoreCase))
            return XBE.Region.USA;
        else if (regionStr.Equals("PAL", StringComparison.OrdinalIgnoreCase))
            return XBE.Region.PAL;
        else if (regionStr.Equals("JPN", StringComparison.OrdinalIgnoreCase))
            return XBE.Region.JPN;
        else if (regionStr.Equals("GLO", StringComparison.OrdinalIgnoreCase))
            return XBE.Region.GLO;
        else if (regionStr.Equals("DBG", StringComparison.OrdinalIgnoreCase))
            return XBE.Region.DBG;
        else
            return (XBE.Region)~0U;
    }

    private static string StringFromXbeRegion(XBE.Region region)
    {
        return region switch
        {
            XBE.Region.USA => "USA",
            XBE.Region.PAL => "PAL",
            XBE.Region.JPN => "JPN",
            XBE.Region.GLO => "GLO",
            XBE.Region.DBG => "DBG",
            _ => "UNK"
        };
    }
}
