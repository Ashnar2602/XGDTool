using System.ComponentModel;

namespace XGDTool.Lib.Image;

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
