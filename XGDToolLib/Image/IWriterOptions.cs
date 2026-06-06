using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Image;

public class IWriterOptions
{
    public Type ImageType { get; set; } = Type.XISO;
    public Converter.Type ConvertType { get; set; } = Converter.Type.Passthrough;
    public string OutDirectory { get; set; } = Environment.CurrentDirectory;
    public bool? Split { get; set; } = null;
    public bool? RenameXbe { get; set; } = null;
    public string? RenameTo { get; set; } = null;
    public bool? AllowedMediaPatch { get; set; } = null;
}
