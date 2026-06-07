using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Converter;

public class Entry : Image.IWriterOptions
{
    public List<string> InputPaths { get; set; } = new();
    public Image.Type InputType { get; set; } = Image.Type.Unknown;
    public bool? AttachXbe { get; set; } = null;
}
