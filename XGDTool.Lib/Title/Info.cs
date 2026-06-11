using XGDTool.Lib.Exe;

namespace XGDTool.Lib.Title;

public class Info
{
    private readonly HeaderTool HeaderTool;
    public Platform Platform => HeaderTool.Platform;
    public uint TitleId => HeaderTool.TitleId;
    public string TitleName = "";
    public string ImageName = "";
    public string FolderName = "";
    public string GodFolderName = "";
    public string GodUniqueName = "";
    
    public XEX.ExecutionInfo XexExecutionInfo => 
        HeaderTool.XexInfo.ExecutionInfo;

    public long XbeCertificateOffset => 
        HeaderTool.XbeInfo.FileOffset;

    public XBE.CertificateHeader XbeCertificate => 
        HeaderTool.XbeInfo.CertificateHeader;

    public byte[]? TitleIconData => 
        Platform == Platform.Xbox 
            ? HeaderTool.XbeInfo?.LogoData 
            : null;

    public long TitleIconOffset =>
        Platform == Platform.Xbox
            ? HeaderTool.XbeInfo?.LogoOffset ?? 0L 
            : 0L;

    public int DiscNumber =>
        Platform == Platform.Xbox360
            ? HeaderTool.XexInfo.ExecutionInfo.DiscNumber
            : 1;

    public int DiscCount =>
        Platform == Platform.Xbox360
            ? HeaderTool.XexInfo.ExecutionInfo.DiscCount
            : 1;

    public Info(HeaderTool headerTool)
    {
        HeaderTool = headerTool;
    }

    public Info() 
    {
        HeaderTool = new HeaderTool();
    }
}
