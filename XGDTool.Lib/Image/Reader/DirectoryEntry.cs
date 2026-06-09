using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Reader;

public class DirectoryEntry : XISO.DirectoryEntry
{
    public long RelativeOffset;
    public long LROffsetFromParent;
    public string Filepath = "";

    public DirectoryEntry Clone()
    {
        var newDir = new DirectoryEntry
        {
            RelativeOffset = this.RelativeOffset,
            LROffsetFromParent = this.LROffsetFromParent,
            Filepath = this.Filepath,
        };
        newDir.Header.FromBytes(this.Header.ToBytes());
        newDir.SetName(this.GetName());
        return newDir;
    }
}
