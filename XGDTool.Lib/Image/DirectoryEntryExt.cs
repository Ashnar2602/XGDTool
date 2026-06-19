using XGDTool.Lib.Image.Formats;

namespace XGDTool.Lib.Image;

public class DirectoryEntryExt
{
    public long LeftOffset;
    public long RightOffset;
    public long StartSector;
    public long FileSize;
    public XDVDFS.DirAttributes Attributes;
    public string FileName = "";
    public string FilePath = "";
    public long RelativeOffset;
    public long LROffsetFromParent;

    public static DirectoryEntryExt FromDirectoryEntry(XDVDFS.DirectoryEntry entry)
    {
        return new DirectoryEntryExt
        {
            LeftOffset = entry.LeftOffset,
            RightOffset = entry.RightOffset,
            StartSector = entry.StartSector,
            FileSize = entry.FileSize,
            Attributes = entry.Attributes,
            FileName = entry.FileName,
        };
    }

    public DirectoryEntryExt Clone()
    {
        return new DirectoryEntryExt
        {
            LeftOffset = this.LeftOffset,
            RightOffset = this.RightOffset,
            StartSector = this.StartSector,
            FileSize = this.FileSize,
            Attributes = this.Attributes,
            FileName = new string(this.FileName),
            FilePath = new string(this.FilePath),
            RelativeOffset = this.RelativeOffset,
            LROffsetFromParent = this.LROffsetFromParent,
        };
    }
}
