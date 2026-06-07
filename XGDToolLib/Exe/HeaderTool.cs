using XGDToolLib.Image;
using XGDToolLib.Image.Format;
using XGDToolLib.Util;

namespace XGDToolLib.Exe;

public class HeaderTool(IReader reader)
{
    public class XbeHeaderInfo
    {
        public required XBE.CertificateHeader CertificateHeader;
        public required long CertificateOffset;
        public byte[]? LogoData;
        public long LogoOffset;
    }

    public class XexHeaderInfo
    {
        public required XEX.ExecutionInfo ExecutionInfo;
        public long? ExecutionInfoOffset;
    }

    private XbeHeaderInfo? _XbeInfo;
    private XexHeaderInfo? _XexInfo;
    private readonly IReader Reader = reader;

    public XbeHeaderInfo XbeInfo => 
        _XbeInfo ??= GetXbeInfo() ?? 
        throw new InvalidOperationException("XBE info is not available.");
    public XexHeaderInfo XexInfo => _XexInfo ??= GetXexInfo();
    public Platform Platform => Reader.Platform;
    public uint TitleId => 
        (Platform == Platform.OriginalXbox) 
            ? XbeInfo.CertificateHeader.TitleID 
            : XexInfo.ExecutionInfo.TitleId;

    private XbeHeaderInfo? GetXbeInfo()
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
        const int MaxLogoSize = 512 * XISO.SECTOR_SIZE;

        if (header.SizeOfMicrosoftLogo > 0 && header.SizeOfMicrosoftLogo < MaxLogoSize)
            logoData = Reader.ReadBytes(
                exeOffset + logoOffset, 
                (int)header.SizeOfMicrosoftLogo);

        return new XbeHeaderInfo() 
        { 
            CertificateHeader = cert, 
            CertificateOffset = certOffset, 
            LogoData = logoData, 
            LogoOffset = logoOffset 
        };
    }

    private XexHeaderInfo GetXexInfo()
    {
        if (Reader.Platform == Platform.OriginalXbox)
            return new XexHeaderInfo() { ExecutionInfo = GetXexExeInfoFromXbe() };

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
                    ExecutionInfoOffset = kvEntry.Value
                };
            }
        }

        throw new InvalidOperationException("XEX execution info entry not found.");
    }

    private XEX.ExecutionInfo GetXexExeInfoFromXbe()
    {
        var xbe = 
            XbeInfo ?? 
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
