using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XGDTool.Lib.Image;

namespace XGDTool.Lib.Converter;

public class OutputOptions : IWriterOptions
{
    public bool? AttachXbe { get; set; } = null;
}
