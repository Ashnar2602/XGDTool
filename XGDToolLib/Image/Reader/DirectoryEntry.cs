using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDToolLib.Image.Format;

namespace XGDToolLib.Image.Reader;

public class DirectoryEntry : XISO.DirectoryEntry
{
    public long RelativeOffset;
    public long LROffsetFromParent;
    public string Filepath = "";
}
