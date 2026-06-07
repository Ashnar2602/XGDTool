using XGDTool.Lib.Exe;

namespace XGDTool.Lib.Title;

public class Info(HeaderTool headerTool)
{
    private readonly HeaderTool HeaderTool = headerTool;
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
        HeaderTool.XbeInfo.CertificateOffset;

    public XBE.CertificateHeader XbeCertificate => 
        HeaderTool.XbeInfo.CertificateHeader;

    public byte[]? TitleIconData => 
        Platform == Platform.OriginalXbox 
            ? HeaderTool.XbeInfo?.LogoData 
            : null;

    public long TitleIconOffset =>
        Platform == Platform.OriginalXbox
            ? HeaderTool.XbeInfo?.LogoOffset ?? 0L 
            : 0L;
}
