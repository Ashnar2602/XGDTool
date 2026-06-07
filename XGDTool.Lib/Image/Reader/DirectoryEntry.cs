using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Lib.Image.Format;

namespace XGDTool.Lib.Image.Reader;

public class DirectoryEntry : XISO.DirectoryEntry
{
    public long RelativeOffset;
    public long LROffsetFromParent;
    public string Filepath = "";
}
