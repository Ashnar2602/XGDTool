using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XGDToolLib.Image;

public enum Type
{
    [Description("Unknown")]
    Unknown,
    [Description("Extract")]
    Extract,
    [Description("XISO")]
    XISO,
    [Description("CCI")]
    CCI,
    [Description("CSO")]
    CSO,
    [Description("GOD")]
    GOD,
    [Description("ZAR")]
    ZAR
}
