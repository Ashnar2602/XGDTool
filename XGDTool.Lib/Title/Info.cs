using XGDTool.Lib.Exe;

namespace XGDTool.Lib.Title;

public class Info
{
    private readonly IHeaderTool? _HeaderTool;
    public IHeaderTool HeaderTool => _HeaderTool ?? throw new InvalidOperationException("HeaderTool has not been initialized.");
    public Platform Platform => HeaderTool.Platform;
    public string TitleName = "";
    public string ImageName = "";
    public string FolderName = "";
    public string GodFolderName = "";
    public string GodUniqueName = "";
    
    public XEX.ExecutionInfo XexExecutionInfo => HeaderTool.ExecutionInfo;
    public long XbeCertificateOffset => (HeaderTool is Exe.HeaderTools.Xbe xbeTool) ? xbeTool.OffsetInFile : 0L;
    public XBE.CertificateHeader XbeCertificate => (HeaderTool is Exe.HeaderTools.Xbe xbeTool) ? xbeTool.CertificateHeader : new XBE.CertificateHeader();
    public byte[]? TitleIconData => HeaderTool.LogoData;
    public long TitleIconOffset => (HeaderTool is Exe.HeaderTools.Xbe xbeTool) ? xbeTool.LogoFileOffset : 0L;
    public uint TitleId => HeaderTool.TitleId;
    public ushort PublisherId => (ushort)(TitleId >> 16);
    public ushort GameId => (ushort)(TitleId & 0xFFFF);
    public int DiscNumber => Platform == Platform.Xbox360 ? HeaderTool.ExecutionInfo.DiscNumber : 1;
    public int DiscCount => Platform == Platform.Xbox360 ? HeaderTool.ExecutionInfo.DiscCount : 1;

    public Info(IHeaderTool headerTool)
    {
        _HeaderTool = headerTool;
    }

    public Info() 
    {
    }
}
