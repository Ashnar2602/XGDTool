using XGDTool.Lib.Image;

namespace XGDTool.Lib.Exe;

public interface IHeaderTool
{
    public Platform Platform { get; }
    public XEX.ExecutionInfo ExecutionInfo { get; }
    public uint TitleId { get; }
    public ushort PublisherId { get; }
    public ushort GameId { get; }
    public byte DiscNumber { get; }
    public byte DiscCount { get; }
    public long OffsetInFile { get; }
    public byte[]? LogoData { get; }

    public static IHeaderTool Create(Platform platform)
    {
        return platform switch
        {
            Platform.Xbox => new HeaderTools.Xbe(),
            Platform.Xbox360 => new HeaderTools.Xex(),
            _ => throw new NotSupportedException($"Platform {platform} is not supported.")
        };
    }

    public static IHeaderTool CreateAndInitialize(IReader reader)
    {
        var tool = Create(reader.Platform);
        tool.Initialize(reader);
        return tool;
    }

    public void Initialize(IReader Reader);
    public void SetTitleId(uint newTitleId);
    public void SetLogoData(byte[] logoData);
}
