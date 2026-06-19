using XGDTool.Lib.Image;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Exe.HeaderTools;

public class Xex : IHeaderTool
{
    private XEX.ExecutionInfo? _XexCert = null;
    private long _OffsetInFile = 0;
    private byte[]? _LogoData = null;

    public Platform Platform => Platform.Xbox360;
    public XEX.ExecutionInfo ExecutionInfo => _XexCert ?? throw new InvalidOperationException("XEX execution info is not available.");
    public uint TitleId => ExecutionInfo.TitleId;
    public long OffsetInFile => _OffsetInFile;
    public byte[]? LogoData => _LogoData;
    public ushort PublisherId => Bits.Upper16(TitleId);
    public ushort GameId => Bits.Lower16(TitleId);
    public byte DiscNumber => ExecutionInfo.DiscNumber;
    public byte DiscCount => ExecutionInfo.DiscCount;

    public void Initialize(IReader Reader)
    {
        var exeEntry = Reader.ExecutableEntry;
        var exeOffset = (long)exeEntry.StartSector * XDVDFS.SECTOR_SIZE;
        var readOffset = exeOffset + Reader.ImageOffset;

        var header = ISerializable.Deserialize<XEX.FileHeader>(
            Reader.ReadBytes(readOffset, XEX.FileHeader.SIZE));

        if (!header.IsValid())
            throw new InvalidOperationException("Invalid XEX header magic.");

        var headerCount = header.HeaderCount;
        var tableSize = checked((int)(headerCount * XEX.DirectoryEntry.SIZE));
        var tableOffset = readOffset + XEX.FileHeader.SIZE;
        var table = Reader.ReadBytes(tableOffset, tableSize);
        var kvEntry = new XEX.DirectoryEntry();

        for (uint i = 0; i < headerCount; i++)
        {
            int baseIndex = checked((int)(i * XEX.DirectoryEntry.SIZE));
            kvEntry.Deserialize(table.AsSpan(baseIndex, XEX.DirectoryEntry.SIZE));

            if (kvEntry.Key == XEX.EntryKey.EXECUTION_INFO)
            {
                var absOffset = readOffset + kvEntry.Value;
                var exeInfo = ISerializable.Deserialize<XEX.ExecutionInfo>(
                    Reader.ReadBytes(absOffset, XEX.ExecutionInfo.SIZE));

                _XexCert = exeInfo;
                _OffsetInFile = exeOffset;
                return;
            }
        }

        throw new InvalidOperationException("XEX execution info entry not found.");
    }

    public void SetTitleId(uint newTitleId)
    {
        if (_XexCert == null)
            throw new InvalidOperationException("XEX execution info is not available.");

        _XexCert.TitleId = newTitleId;
    }

    public void SetLogoData(byte[] logoData)
    {
        _LogoData = logoData;
    }
}
