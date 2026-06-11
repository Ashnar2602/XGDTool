using XGDTool.Lib.Image;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Exe;

public class HeaderTool()
{
    public class XbeHeaderInfo
    {
        public required XBE.CertificateHeader CertificateHeader;
        public required long FileOffset;
        public byte[]? LogoData;
        public long LogoOffset;
    }

    public class XexHeaderInfo
    {
        public required XEX.ExecutionInfo ExecutionInfo;
        public long? FileOffset;
    }

    private XbeHeaderInfo? _XbeInfo = null;
    private XexHeaderInfo? _XexInfo = null;
    private Platform? _Platform = null;

    public XbeHeaderInfo XbeInfo => 
        _XbeInfo ?? throw new InvalidOperationException("XBE info is not available.");

    public XexHeaderInfo XexInfo =>
        _XexInfo ?? throw new InvalidOperationException("XEX info is not available.");

    public Platform Platform => 
        _Platform ?? throw new InvalidOperationException("Platform is not set.");

    public uint TitleId => 
        (Platform == Platform.Xbox) 
            ? XbeInfo.CertificateHeader.TitleID 
            : XexInfo.ExecutionInfo.TitleId;

    public void Initialize(IReader reader)
    {
        _Platform = reader.Platform;
        _XbeInfo = GetXbeInfo(reader);
        _XexInfo = GetXexInfo(reader, _XbeInfo);
    }

    private static XbeHeaderInfo? GetXbeInfo(IReader Reader)
    {
        if (Reader.Platform == Platform.Xbox360)
            return null;

        var exeEntry = Reader.ExecutableEntry;
        var exeOffset = (long)exeEntry.Header.StartSector * XISO.SECTOR_SIZE;
        exeOffset += Reader.ImageOffset;

        var header = Marshalable.FromBytes<XBE.FileHeader>(
            Reader.ReadBytes(exeOffset, XBE.HEADER_SIZE));

        if (!header.IsValid())
            throw new InvalidOperationException("Invalid XBE header magic.");

        var certOffset = header.Certificate - header.BaseAddress;
        var cert = Marshalable.FromBytes<XBE.CertificateHeader>(
            Reader.ReadBytes(exeOffset + certOffset, XBE.CERTIFICATE_SIZE));

        var logoOffset = header.MicrosoftLogo - header.BaseAddress;
        byte[]? logoData = null;

        // size limit is arbitrary, we just dont want a 4gb buffer by accident here
        const int MaxLogoSize = 512 * 2 * XISO.SECTOR_SIZE;

        if (header.SizeOfMicrosoftLogo > 0 && header.SizeOfMicrosoftLogo < MaxLogoSize)
            logoData = Reader.ReadBytes(
                exeOffset + logoOffset, 
                (int)header.SizeOfMicrosoftLogo);

        return new XbeHeaderInfo() 
        { 
            CertificateHeader = cert, 
            FileOffset = certOffset, 
            LogoData = logoData, 
            LogoOffset = logoOffset 
        };
    }

    private static XexHeaderInfo GetXexInfo(IReader Reader, XbeHeaderInfo? xbeInfo)
    {
        if (Reader.Platform == Platform.Xbox)
            return new XexHeaderInfo() { ExecutionInfo = GetXexExeInfoFromXbe(Reader, xbeInfo) };

        var exeEntry = Reader.ExecutableEntry;
        var exeOffset = (long)exeEntry.Header.StartSector * XISO.SECTOR_SIZE;
        exeOffset += Reader.ImageOffset;

        var header = Marshalable.FromBytes<XEX.FileHeader>(
            Reader.ReadBytes(exeOffset, XEX.HEADER_SIZE));

        if (!header.IsValid())
            throw new InvalidOperationException("Invalid XEX header magic.");

        var headerCount = header.HeaderCount;
        var tableSize = checked((int)(headerCount * XEX.DIRECTORY_ENTRY_SIZE));
        var tableOffset = exeOffset + header.Size();
        var table = Reader.ReadBytes(tableOffset, tableSize);
        var kvEntry = new XEX.DirectoryEntry();

        for (uint i = 0; i < headerCount; i++)
        {
            int baseIndex = checked((int)(i * XEX.DIRECTORY_ENTRY_SIZE));
            kvEntry.FromBytes(table.AsSpan(baseIndex, XEX.DIRECTORY_ENTRY_SIZE));

            if (kvEntry.Key == XEX.DirectoryEntryKey.EXECUTION_INFO)
            {
                var absOffset = exeOffset + kvEntry.Value;
                var exeInfo = Marshalable.FromBytes<XEX.ExecutionInfo>(
                    Reader.ReadBytes(absOffset, XEX.EXECUTION_INFO_SIZE));

                return new XexHeaderInfo()
                {
                    ExecutionInfo = exeInfo,
                    FileOffset = kvEntry.Value
                };
            }
        }

        throw new InvalidOperationException("XEX execution info entry not found.");
    }

    private static XEX.ExecutionInfo GetXexExeInfoFromXbe(IReader Reader, XbeHeaderInfo? xbeInfo)
    {
        var xbe = 
            xbeInfo ?? 
            GetXbeInfo(Reader) ??
            throw new InvalidOperationException("XBE certificate is unavailable.");
        var info = new XEX.ExecutionInfo();
        info.MediaId = 0;
        info.Platform = 0;
        info.ExecutableType = 0;
        info.TitleId = xbe.CertificateHeader.TitleID;
        info.DiscNumber = 1;
        info.DiscCount = 1;
        return info;
    }
}
