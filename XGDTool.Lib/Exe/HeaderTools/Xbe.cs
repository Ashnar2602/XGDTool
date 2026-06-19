using System.Runtime.InteropServices;
using System.Text;
using XGDTool.Lib.Image;
using XGDTool.Lib.Image.Formats;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Exe.HeaderTools;

public class Xbe : IHeaderTool
{
    private XBE.CertificateHeader? _XbeCert = null;
    private XEX.ExecutionInfo? _XexCert = null;
    private long _OffsetInFile = 0;
    private byte[]? _LogoData = null;

    public Platform Platform => Platform.Xbox;
    public XEX.ExecutionInfo ExecutionInfo => _XexCert ?? throw new InvalidOperationException("XEX execution info is not available.");
    public XBE.CertificateHeader CertificateHeader => _XbeCert ?? throw new InvalidOperationException("XBE certificate header is not available.");
    public uint TitleId => _XbeCert?.TitleID ?? throw new InvalidOperationException("XBE certificate header is not available.");
    public long OffsetInFile => _OffsetInFile;
    public byte[]? LogoData => _LogoData;
    public long LogoFileOffset;
    public ushort PublisherId => Bits.Upper16(TitleId);
    public ushort GameId => Bits.Lower16(TitleId);
    public byte DiscNumber => 1;
    public byte DiscCount => 1;

    public void Initialize(IReader Reader)
    {
        if (Reader.Platform != Platform.Xbox)
            throw new InvalidOperationException(
                $"Invalid platform {Reader.Platform} for {nameof(Xbe)}.");

        var exeEntry = Reader.ExecutableEntry;
        var exeOffset = (long)exeEntry.StartSector * XDVDFS.SECTOR_SIZE;
        var readOffset = exeOffset + Reader.ImageOffset;

        var header = ISerializable.Deserialize<XBE.FileHeader>(
            Reader.ReadBytes(readOffset, XBE.FileHeader.SIZE));

        if (!header.IsValid())
            throw new InvalidOperationException("Invalid XBE header magic.");

        var certOffset = header.Certificate - header.BaseAddress;
        var cert = ISerializable.Deserialize<XBE.CertificateHeader>(
            Reader.ReadBytes(readOffset + certOffset, XBE.CertificateHeader.SIZE));

        var logoOffset = header.MicrosoftLogo - header.BaseAddress;
        byte[]? logoData = null;

        // size limit is arbitrary, we just dont want a 4gb buffer by accident here
        const int MaxLogoSize = 512 * 2 * XDVDFS.SECTOR_SIZE;

        if (header.SizeOfMicrosoftLogo > 0 && header.SizeOfMicrosoftLogo < MaxLogoSize)
            logoData = Reader.ReadBytes(
                readOffset + logoOffset, 
                (int)header.SizeOfMicrosoftLogo);

        _XbeCert = cert;
        _XexCert = GetXexExeInfoFromXbe(cert);
        _LogoData = logoData;
        LogoFileOffset = logoOffset;
        _OffsetInFile = exeOffset;
    }

    public void PatchAllowedMedia()
    {

    }

    public string GetTitleName()
    {
        if (_XbeCert == null)
            throw new InvalidOperationException("XBE certificate header is not available.");

        ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<ushort, byte>(_XbeCert.TitleName);
        return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    public void SetTitleName(string newTitle)
    {
        if (_XbeCert == null)
            throw new InvalidOperationException("XBE certificate header is not available.");
            
        Array.Clear(_XbeCert.TitleName, 0, _XbeCert.TitleName.Length);
        var maxChars = Math.Min(newTitle.Length, _XbeCert.TitleName.Length);

        for (int i = 0; i < maxChars; i++)
            _XbeCert.TitleName[i] = newTitle[i];
    }

    public void SetTitleId(uint newTitleId)
    {
        if (_XbeCert == null)
            throw new InvalidOperationException("XBE certificate header is not available.");

        _XbeCert.TitleID = newTitleId;

        if (_XexCert != null)
            _XexCert.TitleId = newTitleId;
    }

    public void SetLogoData(byte[] logoData)
    {
        if (_LogoData == null)
            throw new InvalidOperationException("XBE logo data is not available.");

        if (logoData.Length > _LogoData.Length)
            throw new ArgumentException($"Logo data is too large. Max size is {_LogoData.Length} bytes.");

        logoData.CopyTo(_LogoData, 0);
        if (logoData.Length < _LogoData.Length)
            Array.Clear(_LogoData, logoData.Length, _LogoData.Length - logoData.Length);
    }

    private static XEX.ExecutionInfo GetXexExeInfoFromXbe(XBE.CertificateHeader cert) =>
        new() 
        {
            MediaId = 0,
            Platform = 0,
            ExecutableType = 0,
            TitleId = cert.TitleID,
            DiscNumber = 1,
            DiscCount = 1,
        };
}
