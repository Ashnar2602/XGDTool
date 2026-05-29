using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.Format
{
    public enum Platform
    {
        [Description("Unknown")]
        Unknown,
        [Description("Original Xbox")]
        OriginalXbox,
        [Description("Xbox 360")]
        Xbox360
    }
}
