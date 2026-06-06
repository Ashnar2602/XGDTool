using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Image.Reader;

internal class Xiso(IReadOnlyList<string> files) : Base(files)
{
    private readonly List<FileStream> Streams = new();

    public override Type ImageType => Type.XISO;
    public override uint TotalSectors { get; set; }
}
