using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.GUI.ViewModels;

public enum ImageJobAutoFormat
{
    [Description("None")]
    None,
    [Description("Auto Xbox")]
    Xbox,
    [Description("Auto Xbox 360")]
    Xbox360,
    [Description("Auto Xemu")]
    Xemu,
    [Description("Auto Xenia")]
    Xenia
}
