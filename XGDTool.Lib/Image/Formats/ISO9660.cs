using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Buffers.Binary;
using System.Reflection;
using XGDTool.Lib.Util;

namespace XGDTool.Lib.Image.Formats;

public static class ISO9660
{
    public const string STANDARD_IDENTIFIER = "CD001";
    public const string SEPARATOR1 = ".";
    public const string SEPARATOR2 = ";";

    internal class PrimaryVolumeDescriptor : ISerializable
    {
        public byte VolumeDescriptorType;
        public readonly byte[] StandardIdentifier = new byte[5];
        public byte VolumeDescriptorVersion;
        public byte VolumesFlags;
        public readonly byte[] SystemIdentifier = new byte[32];
        public readonly byte[] VolumeIdentifier = new byte[32];

        // private readonly byte[] Unused1 = new byte[8];
        // public uint VolumeSpaceSizeLittle;
        // public uint VolumeSpaceSizeBig;
        // public byte[] Unused2 = new byte[32];
        // public ushort VolumeSetSizeLittle;
        // public ushort VolumeSetSizeBig;
        // public ushort VolumeSequenceNumberLittle;
        // public ushort VolumeSequenceNumberBig;
        // public ushort LogicalBlockSizeLittle;
        // public ushort LogicalBlockSizeBig;
        // public uint PathTableSizeLittle;
        // public uint PathTableSizeBig;
        // public uint LbaOfLPathTableLittle;
        // public uint LbaOfOptionalLPathTableLittle;
        // public uint LbaOfMPathTableBig;
        // public uint LbaOfOptionalMPathTableBig;

        public uint VolumeSpaceSize;
        public ushort VolumeSetSize;
        public ushort VolumeSequenceNumber;
        public ushort LogicalBlockSize;
        public uint PathTableSize;
        public uint LbaOfLPathTable;
        public uint LbaOfOptionalLPathTable;
        public uint LbaOfMPathTable;
        public uint LbaOfOptionalMPathTable;

        public readonly byte[] RootEntryDirectoryRecord = new byte[34];
        public readonly byte[] VolumeSetIdentifier = new byte[128];
        public readonly byte[] PublisherIdentifier = new byte[128];
        public readonly byte[] DataPreparerIdentifier = new byte[128];
        public readonly byte[] ApplicationIdentifier = new byte[128];
        public readonly byte[] CopyrightFileIdentifier = new byte[37];
        public readonly byte[] AbstractFileIdentifier = new byte[37];
        public readonly byte[] BibliographicFileIdentifier = new byte[37];
        public readonly byte[] VolumeCreationDateTime = new byte[17];
        public readonly byte[] VolumeModificationDateTime = new byte[17];
        public readonly byte[] VolumeExpirationDateTime = new byte[17];
        public readonly byte[] VolumeEffectiveDateTime = new byte[17];
        public byte FileStructureVersion;
        // private readonly byte Reserved1;
        public readonly byte[] ApplicationUse = new byte[512];
        // private readonly byte[] Reserved2 = new byte[563];

        // End of packed data

        public static int SIZE => 2048;
        public int Size() => SIZE;

        public PrimaryVolumeDescriptor(uint totalSectors, Title.Info titleInfo, DateTime? dateTime = null)
        {
            var idChars = Encoding.ASCII.GetBytes(STANDARD_IDENTIFIER);
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            var xgdIdChars = Encoding.ASCII.GetBytes($"XGDTOOL V{appVersion.ToUpperInvariant()}");
            var titleIdChars = Encoding.ASCII.GetBytes(titleInfo.TitleId.ToString("X8"));
            var publisherIdChars = Encoding.ASCII.GetBytes(titleInfo.PublisherId.ToString("X4"));
            var gameIdChars = Encoding.ASCII.GetBytes(titleInfo.GameId.ToString("X4"));
            var systemNameChars = Encoding.ASCII.GetBytes(
                "MICROSOFT XBOX" +
                (titleInfo.Platform == Exe.Platform.Xbox ? string.Empty : " 360"));
            var spaceChar = Encoding.ASCII.GetBytes(" ")[0];
            var zeroChar = Encoding.ASCII.GetBytes("0")[0];
            var dateTimeChars = Encoding.ASCII.GetBytes((dateTime ?? DateTime.UtcNow).ToString("yyyyMMddHHmmssff"));

            VolumeDescriptorType = 0x01;
            idChars.CopyTo(StandardIdentifier, 0);
            VolumeDescriptorVersion = 0x01;

            systemNameChars.CopyTo(SystemIdentifier, 0);
            SystemIdentifier.AsSpan(systemNameChars.Length).Fill(spaceChar);

            titleIdChars.CopyTo(VolumeIdentifier, 0);
            VolumeIdentifier.AsSpan(titleIdChars.Length).Fill(spaceChar);

            VolumeSpaceSize =  totalSectors;

            VolumeSetSize = (ushort)titleInfo.DiscCount;

            VolumeSequenceNumber = (ushort)titleInfo.DiscNumber;

            LogicalBlockSize = (ushort)XDVDFS.SECTOR_SIZE;

            titleIdChars.CopyTo(VolumeSetIdentifier, 0);
            VolumeSetIdentifier.AsSpan(titleIdChars.Length).Fill(spaceChar);

            publisherIdChars.CopyTo(PublisherIdentifier, 0);
            PublisherIdentifier.AsSpan(publisherIdChars.Length).Fill(spaceChar);

            xgdIdChars.CopyTo(DataPreparerIdentifier, 0);
            DataPreparerIdentifier.AsSpan(xgdIdChars.Length).Fill(spaceChar);

            gameIdChars.CopyTo(ApplicationIdentifier, 0);
            ApplicationIdentifier.AsSpan(gameIdChars.Length).Fill(spaceChar);

            CopyrightFileIdentifier.AsSpan().Fill(spaceChar);
            AbstractFileIdentifier.AsSpan().Fill(spaceChar);
            BibliographicFileIdentifier.AsSpan().Fill(spaceChar);

            dateTimeChars.AsSpan(0, 16).CopyTo(VolumeCreationDateTime);
            dateTimeChars.AsSpan(0, 16).CopyTo(VolumeModificationDateTime);
            VolumeExpirationDateTime.AsSpan(0, 16).Fill(zeroChar);
            VolumeEffectiveDateTime.AsSpan(0, 16).Fill(zeroChar);

            FileStructureVersion = 0x01;
        }

        public void Serialize(Span<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer must be at least {SIZE} bytes long", nameof(buffer));

            var offset = 0;
            buffer[offset++] = VolumeDescriptorType;
            StandardIdentifier.CopyTo(buffer.Slice(1, 5)); offset += 5;
            buffer[offset++] = VolumeDescriptorVersion;
            buffer[offset++] = VolumesFlags;

            SystemIdentifier.CopyTo(buffer.Slice(offset, 32)); offset += 32;
            VolumeIdentifier.CopyTo(buffer.Slice(offset, 32)); offset += 32;
            offset += 8; // 8 bytes unused

            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), VolumeSpaceSize); offset += 4;
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset, 4), VolumeSpaceSize); offset += 4;
            offset += 32; // 32 bytes unused

            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset, 2), VolumeSetSize); offset += 2;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(offset, 2), VolumeSetSize); offset += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset, 2), VolumeSequenceNumber); offset += 2;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(offset, 2), VolumeSequenceNumber); offset += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset, 2), LogicalBlockSize); offset += 2;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(offset, 2), LogicalBlockSize); offset += 2;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), PathTableSize); offset += 4;
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset, 4), PathTableSize); offset += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), PathTableSize); offset += 4;

            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), LbaOfLPathTable); offset += 4;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), LbaOfOptionalLPathTable); offset += 4;
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset, 4), LbaOfMPathTable); offset += 4;
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset, 4), LbaOfOptionalMPathTable); offset += 4;

            RootEntryDirectoryRecord.CopyTo(buffer.Slice(offset, 34)); offset += 34;

            VolumeSetIdentifier.CopyTo(buffer.Slice(offset, 128)); offset += 128;
            PublisherIdentifier.CopyTo(buffer.Slice(offset, 128)); offset += 128;
            DataPreparerIdentifier.CopyTo(buffer.Slice(offset, 128)); offset += 128;
            ApplicationIdentifier.CopyTo(buffer.Slice(offset, 128)); offset += 128;

            CopyrightFileIdentifier.CopyTo(buffer.Slice(offset, 37)); offset += 37;
            AbstractFileIdentifier.CopyTo(buffer.Slice(offset, 37)); offset += 37;
            BibliographicFileIdentifier.CopyTo(buffer.Slice(offset, 37)); offset += 37;

            VolumeCreationDateTime.CopyTo(buffer.Slice(offset, 17)); offset += 17;
            VolumeModificationDateTime.CopyTo(buffer.Slice(offset, 17)); offset += 17;
            VolumeExpirationDateTime.CopyTo(buffer.Slice(offset, 17)); offset += 17;
            VolumeEffectiveDateTime.CopyTo(buffer.Slice(offset, 17)); offset += 17;

            buffer[offset++] = FileStructureVersion;
            offset++; // reserved
            ApplicationUse.CopyTo(buffer.Slice(offset, 512));
        }

        public void Deserialize(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer must be at least {SIZE} bytes long", nameof(buffer));

            var offset = 0;
            VolumeDescriptorType = buffer[offset++];
            buffer.Slice(offset, 5).CopyTo(StandardIdentifier); offset += 5;
            VolumeDescriptorVersion = buffer[offset++];
            VolumesFlags = buffer[offset++];

            buffer.Slice(offset, 32).CopyTo(SystemIdentifier); offset += 32;
            buffer.Slice(offset, 32).CopyTo(VolumeIdentifier); offset += 32;
            offset += 8; // 8 bytes unused

            VolumeSpaceSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, 4)); offset += 4;
            offset += 4; // big endian copy
            offset += 32; // 32 bytes unused

            VolumeSetSize = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, 2)); offset += 2;
            offset += 2; // big endian copy
            VolumeSequenceNumber = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, 2)); offset += 2;
            offset += 2; // big endian copy
            LogicalBlockSize = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, 2)); offset += 2;
            offset += 2; // big endian copy
            PathTableSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, 4)); offset += 4;
            offset += 4; // big endian copy

            LbaOfLPathTable = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, 4)); offset += 4;
            LbaOfOptionalLPathTable = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, 4)); offset += 4;
            LbaOfMPathTable = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(offset, 4)); offset += 4;
            LbaOfOptionalMPathTable = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(offset, 4)); offset += 4;

            buffer.Slice(offset, 34).CopyTo(RootEntryDirectoryRecord); offset += 34;

            buffer.Slice(offset, 128).CopyTo(VolumeSetIdentifier); offset += 128;
            buffer.Slice(offset, 128).CopyTo(PublisherIdentifier); offset += 128;
            buffer.Slice(offset, 128).CopyTo(DataPreparerIdentifier); offset += 128;
            buffer.Slice(offset, 128).CopyTo(ApplicationIdentifier); offset += 128;

            buffer.Slice(offset, 37).CopyTo(CopyrightFileIdentifier); offset += 37;
            buffer.Slice(offset, 37).CopyTo(AbstractFileIdentifier); offset += 37;
            buffer.Slice(offset, 37).CopyTo(BibliographicFileIdentifier); offset += 37;

            buffer.Slice(offset, 17).CopyTo(VolumeCreationDateTime); offset += 17;
            buffer.Slice(offset, 17).CopyTo(VolumeModificationDateTime); offset += 17;
            buffer.Slice(offset, 17).CopyTo(VolumeExpirationDateTime); offset += 17;
            buffer.Slice(offset, 17).CopyTo(VolumeEffectiveDateTime); offset += 17;

            FileStructureVersion = buffer[offset++];
            offset++; // reserved
            buffer.Slice(offset, 512).CopyTo(ApplicationUse);
        }
    }

    internal class TerminatorVolumeDescriptor : ISerializable
    {
        public byte VolumeDescriptorType;
        public readonly byte[] StandardIdentifier = new byte[5];
        public byte VolumeDescriptorVersion;

        public TerminatorVolumeDescriptor()
        {
            VolumeDescriptorType = 0xFF;
            Encoding.ASCII.GetBytes(STANDARD_IDENTIFIER).CopyTo(StandardIdentifier, 0);
            VolumeDescriptorVersion = 0x01;
        }

        public const int SIZE = 7;
        public int Size() => SIZE;
        public void Serialize(Span<byte> buffer)
        {
            buffer[0] = VolumeDescriptorType;
            StandardIdentifier.CopyTo(buffer.Slice(1, 5));
            buffer[6] = VolumeDescriptorVersion;
        }
        public void Deserialize(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < SIZE)
                throw new ArgumentException($"Buffer must be at least {SIZE} bytes long", nameof(buffer));

            VolumeDescriptorType = buffer[0];
            buffer.Slice(1, 5).CopyTo(StandardIdentifier);
            VolumeDescriptorVersion = buffer[6];
        }
    }

    public class PathTableRecord
    {
        public byte DirectoryIdentifierLength;
        public byte ExtendedAttributeRecordLength;
        public uint LocationOfExtent;
        public ushort ParentDirectoryNumber;
        public string DirectoryIdentifier = string.Empty;

        public byte[] Serialize()
        {
            var dirIdBytes = Encoding.ASCII.GetBytes(DirectoryIdentifier);
            var buffer = new byte[Size()];
            buffer[0] = DirectoryIdentifierLength;
            buffer[1] = ExtendedAttributeRecordLength;
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(2, 4), (int)LocationOfExtent);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(6, 2), ParentDirectoryNumber);
            dirIdBytes.CopyTo(buffer, 8);
            return buffer;
        }

        public int Size() => 
            8 + 
            Encoding.ASCII.GetByteCount(DirectoryIdentifier) + 
            (DirectoryIdentifierLength % 2 == 0 ? 0 : 1);
    }

    public class DirectoryRecord
    {
        public byte Length;
        public byte ExtendedAttributeRecordLength;
        public uint LocationOfExtentLittle;
        public uint LocationOfExtentBig;
        public uint DataLengthLittle;
        public uint DataLengthBig;
        public byte[] RecordingDateTime = new byte[7];
        public byte FileFlags;
        public byte FileUnitSize;
        public byte InterleaveGapSize;
        public ushort VolumeSequenceNumberLittle;
        public ushort VolumeSequenceNumberBig;
        public byte FileIdentifierLength;
        public string FileIdentifier = string.Empty;
    }
}
