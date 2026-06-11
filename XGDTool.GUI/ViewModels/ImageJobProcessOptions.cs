using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace XGDTool.GUI.ViewModels;

public enum ImageJobProcessOptions
{
    [Description("None")]
    None,
    [Description("Reauthor")]
    Reauthor,
    [Description("Scrub")]
    Scrub
}
