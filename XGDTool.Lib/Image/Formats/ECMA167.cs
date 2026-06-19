// using System.Runtime.CompilerServices;
// using System.Runtime.InteropServices;
// using System.Text;
// using System.Buffers.Binary;
// using System.Reflection;
// using XGDTool.Lib.Util;

// namespace XGDTool.Lib.Image.Formats;

// public static class ECMA167
// {
//     public class DString(int length)
//     {
//         private readonly byte[] _Data = new byte[length];
//         private readonly int Length = length;
//         public byte[] Data => _Data;

//         public override string ToString()
//         {
//             var str = Encoding.ASCII.GetString(_Data);
//             var nullIndex = str.IndexOf('\0');
//             if (nullIndex >= 0)
//                 str = str.Substring(0, nullIndex);
//             return str;
//         }

//         public void SetString(string value)
//         {
//             var bytes = Encoding.ASCII.GetBytes(value);

//             if (bytes.Length > Length)
//                 throw new ArgumentException($"String is too long. Max length is {Length} bytes.");

//             Array.Clear(_Data, 0, _Data.Length);
//             Array.Copy(bytes, _Data, bytes.Length);
//         }
//     }

//     public enum CharSetType : byte
//     {
//         CS0 = 0,
//         CS1 = 1,
//         CS2 = 2,
//         CS3 = 3,
//         CS4 = 4,
//         CS5 = 5,
//         CS6 = 6,
//         CS7 = 7,
//         CS8 = 8
//     }

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class CharSpec : IMarshalable<CharSpec>
//     {
//         public CharSetType Type;

//         [MarshalAs(UnmanagedType.ByValArray, SizeConst = 63)]
//         public readonly byte[] Information = new byte[63];

//         public static int SIZE => 64;
//     }

//     public enum TimeStampType : ushort
//     {
//         CoordinateUniversal = 0,
//         Local = 0x1000,
//         Agreement = 0x2000
//     }

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class TimeStamp : IMarshalable<TimeStamp>
//     {
//         private ushort TypeAndTimezone;
//         public short Year;
//         public byte Month;
//         public byte Day;
//         public byte Hour;
//         public byte Minute;
//         public byte Second;
//         public byte Centiseconds;
//         public byte HundredsOfMicroseconds;
//         public byte Microseconds;

//         public static int SIZE => 12;

//         public TimeStampType Type
//         {
//             get => (TimeStampType)(TypeAndTimezone & 0xF000);
//             set => TypeAndTimezone = (ushort)((TypeAndTimezone & 0x0FFF) | ((ushort)value & 0xF000));
//         }
//         public sbyte TimezoneOffset
//         {
//             get => (sbyte)(TypeAndTimezone & 0x0FFF);
//             set => TypeAndTimezone = (ushort)((TypeAndTimezone & 0xF000) | ((ushort)value & 0x0FFF));
//         }
//     }

//     [Flags]
//     public enum EntityFlag : byte
//     {
//         Dirty = 1 << 0,
//         Protected = 1 << 1,
//     }

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class RegId : IMarshalable<RegId>
//     {
//         public EntityFlag Flags;

//         [MarshalAs(UnmanagedType.ByValArray, SizeConst = 23)]
//         public readonly byte[] Identifier = new byte[23];

//         [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
//         public readonly byte[] IdentifierSuffix = new byte[8];

//         public static int SIZE => 32;
//     }

//     public const int VSD_STD_ID_LENGTH = 5;

//     // Standard Identifier (EMCA 167r2 2/9.1.2)
//     public const string VSD_STD_ID_NSR02 = "NSR02";	// (3/9.1)

//     // Standard Identifier (ECMA 167r3 2/9.1.2)
//     public const string VSD_STD_ID_BEA01 = "BEA01";	// (2/9.2)
//     public const string VSD_STD_ID_BOOT2 = "BOOT2";	// (2/9.4)
//     public const string VSD_STD_ID_CD001 = "CD001";	// (ECMA-119)
//     public const string VSD_STD_ID_CDW02 = "CDW02";	// (ECMA-168)
//     public const string VSD_STD_ID_NSR03 = "NSR03";	// (3/9.1)
//     public const string VSD_STD_ID_TEA01 = "TEA01";	// (2/9.3)

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class VolumeStructDescriptor : IMarshalable<VolumeStructDescriptor>
//     {
//         public byte Type;

//         [MarshalAs(UnmanagedType.ByValArray, SizeConst = VSD_STD_ID_LENGTH)]
//         public byte[] StdIndentifier = new byte[VSD_STD_ID_LENGTH];
//         public byte Version;

//         [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2041)] 
//         public byte[] Data = new byte[2041];

//         public static int SIZE => 2048;
//     }

//     public class BeginningExtendedAreaDescriptor : VolumeStructDescriptor {}
//     public class TerminatingExtendedAreaDescriptor : VolumeStructDescriptor {}

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class ExtentAd : IMarshalable<ExtentAd>
//     {
//         public uint Length;
//         public uint Location;
        
//         public static int SIZE => 8;
//     }

//     public enum TagIdentifier : ushort
//     {
//         PrimaryVolumeDescriptor = 1,
//         AnchorVolumeDescriptorPointer = 2,
//         VolumeDescriptorPointer = 3,
//         ImplementationUseVolumeDescriptor = 4,
//         PartitionDescriptor = 5,
//         LogicalVolumeDescriptor = 6,
//         UnallocatedSpaceDescriptor = 7,
//         TerminatingDescriptor = 8,
//         LogicalVolumeIntegrityDescriptor = 9,

//         FileSetDescriptor = 0x0100,
//         FileIdentDescriptor	= 0x0101,
//         AllocationExtDescriptor	= 0x0102,
//         IndirectEntry = 0x0103,
//         TerminalEntry = 0x0104,
//         Fileentry = 0x0105,
//         ExtendedAttributeHeaderDescriptor = 0x0106,
//         UnallocSpaceEntry = 0x0107,
//         SpaceBitmapDescriptor	= 0x0108,
//         PartitionIntegrityEntry = 0x0109,
//         ExtendedFileEntry = 0x010A,
//     }

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class Tag(TagIdentifier identifier, ushort version = 3) : IMarshalable<Tag>
//     {
//         public readonly TagIdentifier Identifier = identifier;
//         public readonly ushort DescriptorVersion = version;
//         private byte TagChecksum;
//         private readonly byte Reserved;
//         public ushort TagSerialNumber;
//         public ushort DescriptorCRC;
//         public ushort DescriptorCRCLength;
//         public uint TagLocation;

//         public static int SIZE => 16;

//         public byte[] Finalize(ReadOnlySpan<byte> descriptorData)
//         {
//             DescriptorCRC = Crc16Ccit.CalculateChecksum(descriptorData);
//             DescriptorCRCLength = (ushort)descriptorData.Length;
//             TagChecksum = 0;
            
//             var bytes = this.ToBytes();
//             var checksum = 0;

//             for (int i = 0; i < bytes.Length; i++) 
//                 checksum += bytes[i];

//             TagChecksum = (byte)(checksum % 256);
//             bytes[4] = TagChecksum;
//             return bytes;
//         }
//     }

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class NrsDescriptor : IMarshalable<NrsDescriptor>
//     {
//         public byte Type;

//         [MarshalAs(UnmanagedType.ByValArray, SizeConst = VSD_STD_ID_LENGTH)]
//         public byte[] StdIndentifier = new byte[VSD_STD_ID_LENGTH];
//         public byte Version;
//         public byte Reserved;

//         [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2040)]
//         public byte[] Data = new byte[2040];

//         public static int SIZE => 2048;
//     }

//     [Flags]
//     public enum PrimaryVolumeFlags : ushort
//     {
//         VsiNeedNotBeCommon = 1 << 0,
//         VsiIsCommon = 1 << 1,
//     }

//     public class PrimaryVolumeDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.PrimaryVolumeDescriptor); // 16 bytes
//         public uint VolumeDescriptorSequenceNumber;
//         public uint PrimaryVolumeDescriptorNumber;
//         public readonly DString VolumeIdentifier = new(32); // 32 bytes
//         public ushort VolumeSequenceNumber;
//         public ushort MaximumVolumeSequenceNumber;
//         public ushort InterchangeLevel;
//         public ushort MaximumInterchangeLevel;
//         public uint CharacterSetList;
//         public uint MaximumCharacterSetList;
//         public readonly DString VolumeSetIdentifier = new(128); // 128 bytes
//         public CharSpec DescriptorCharacterSet = new(); // 64 bytes
//         public CharSpec ExplanatoryCharacterSet = new(); // 64 bytes
//         public ExtentAd VolumeAbstract = new(); // 8 bytes
//         public ExtentAd VolumeCopyrightNotice = new(); // 8 bytes
//         public RegId ApplicationIdentifier = new(); // 32 bytes
//         public TimeStamp RecordingDateAndTime = new(); // 12 bytes
//         public RegId ImplementationIdentifier = new(); // 8 bytes
//         public readonly byte[] ImplementationUse = new byte[64];
//         public uint PredecessorVolumeDescriptorSequenceLocation;
//         public PrimaryVolumeFlags Flags;

//         // public readonly byte[] Reserved = new byte[22];

//         public const int SIZE = 512;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(VolumeDescriptorSequenceNumber)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(PrimaryVolumeDescriptorNumber)));
//             s.Write(VolumeIdentifier.Data);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(VolumeSequenceNumber)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(MaximumVolumeSequenceNumber)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(InterchangeLevel)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(MaximumInterchangeLevel)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(CharacterSetList)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(MaximumCharacterSetList)));
//             s.Write(VolumeSetIdentifier.Data);
//             s.Write(DescriptorCharacterSet.ToBytes());
//             s.Write(ExplanatoryCharacterSet.ToBytes());
//             s.Write(VolumeAbstract.ToBytes());
//             s.Write(VolumeCopyrightNotice.ToBytes());
//             s.Write(ApplicationIdentifier.ToBytes());
//             s.Write(RecordingDateAndTime.ToBytes());
//             s.Write(ImplementationIdentifier.ToBytes());
//             s.Write(ImplementationUse);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(PredecessorVolumeDescriptorSequenceLocation)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((ushort)Flags)));

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));
//             return data;
//         }
//     }

//     public class AnchorVolumeDescriptorPointer
//     {
//         public Tag Tag = new(TagIdentifier.AnchorVolumeDescriptorPointer); // 16 bytes
//         public ExtentAd MainVolumeDescriptorSequenceExtent = new(); // 8 bytes
//         public ExtentAd ReserveVolumeDescriptorSequenceExtent = new(); // 8 bytes

//         // public readonly byte[] Reserved = new byte[480];

//         public const int SIZE = 512;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(MainVolumeDescriptorSequenceExtent.ToBytes());
//             s.Write(ReserveVolumeDescriptorSequenceExtent.ToBytes());

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class VolumeDescriptorPointer
//     {
//         public Tag Tag = new(TagIdentifier.VolumeDescriptorPointer);
//         public uint VolumeDescriptorSequenceNumber;
//         public ExtentAd NextVolumeDescriptorSequenceExtent = new();

//         // public readonly byte[] Reserved = new byte[484];

//         public const int SIZE = 512;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(VolumeDescriptorSequenceNumber)));
//             s.Write(NextVolumeDescriptorSequenceExtent.ToBytes());

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class ImplementationUseVolumeDescriptor
//     {
//         public class ImpUse
//         {
//             public CharSpec LviCharset = new();
//             public readonly DString LogicVolumeId = new(128);
//             public readonly DString LvInfo1 = new(36);
//             public readonly DString LvInfo2 = new(36);
//             public readonly DString LvInfo3 = new(36);
//             public RegId ImplementationId = new();
//             public readonly byte[] ImplementationUse = new byte[128];

//             public static readonly int SIZE = CharSpec.SIZE + 128 + (36 * 3) + RegId.SIZE + 128;

//             public byte[] Serialize()
//             {
//                 if (SIZE != 480)
//                     throw new InvalidOperationException("ImpUse size must be 480 bytes.");

//                 var data = new byte[SIZE];
//                 var s = new MemoryStream(data);

//                 s.Write(LviCharset.ToBytes());
//                 s.Write(LogicVolumeId.Data);
//                 s.Write(LvInfo1.Data);
//                 s.Write(LvInfo2.Data);
//                 s.Write(LvInfo3.Data);
//                 s.Write(ImplementationId.ToBytes());
//                 s.Write(ImplementationUse);

//                 return data;
//             }
//         }

//         public Tag Tag = new(TagIdentifier.ImplementationUseVolumeDescriptor);
//         public uint VolumeDescriptorSequenceNumber;
//         public RegId ImplementationIdentifier = new();
//         public ImpUse ImplementationUse = new();

//         public static readonly int SIZE = Tag.SIZE + sizeof(uint) + RegId.SIZE + ImpUse.SIZE;

//         public byte[] Serialize()
//         {
//             if (SIZE != 512)
//                 throw new InvalidOperationException("ImplementationUseVolumeDescriptor size must be 512 bytes.");

//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);
//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(VolumeDescriptorSequenceNumber)));
//             s.Write(ImplementationIdentifier.ToBytes());
//             s.Write(ImplementationUse.Serialize());
//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));
//             return data;
//         }
//     }

//     [Flags]
//     public enum PartitionFlags : ushort
//     {
//         NotAllocated = 1 << 0,
//         Allocated = 1 << 1,
//     }

//     public enum AccessType : uint
//     {
//         NotSpecified = 0,
//         ReadOnly = 1,
//         WriteOnce = 2,
//         Rewritable = 3,
//         Overwritable = 4
//     }

//     public const string PARTCON_FDC01 = "+FDC01";
//     public const string PARTCON_CD001 = "+CD001";
//     public const string PARTCON_CDW02 = "+CDW02";
//     public const string PARTCON_NSR03 = "+NSR03";

//     public class PartitionDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.PartitionDescriptor);
//         public uint VolumeDescriptorSequenceNumber;
//         public PartitionFlags PartitionFlags;
//         public ushort PartitionNumber;
//         public RegId PartitionContents = new();
//         public readonly byte[] PartitionContentsUse = new byte[128];
//         public AccessType AccessType;
//         public uint PartitionStartingLocation;
//         public uint PartitionLength;
//         public RegId ImplementationIdentifier = new();
//         public readonly byte[] ImplementationUse = new byte[128];

//         // public readonly byte[] Reserved = new byte[156];

//         public const int SIZE = 512;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(VolumeDescriptorSequenceNumber)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((ushort)PartitionFlags)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(PartitionNumber)));
//             s.Write(PartitionContents.ToBytes());
//             s.Write(PartitionContentsUse);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((uint)AccessType)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(PartitionStartingLocation)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(PartitionLength)));
//             s.Write(ImplementationIdentifier.ToBytes());
//             s.Write(ImplementationUse);

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class LogicalVolumeDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.LogicalVolumeDescriptor);
//         public uint VolumeDescriptorSequenceNumber;
//         public CharSpec DescriptorCharacterSet = new();
//         public readonly DString LogicalVolumeIdentifier = new(128);
//         public uint LogicalBlockSize;
//         public RegId DomainIdentifier = new();
//         public readonly byte[] LogicalVolumeContentsUse = new byte[16];
//         public uint MapTableLength => (uint)PartitionMaps.Sum(m => m.Length);
//         public uint NumberOfPartitionMaps => (uint)PartitionMaps.Count;
//         public RegId ImplementationIdentifier = new();
//         public readonly byte[] ImplementationUse = new byte[32];
//         public ExtentAd IntegritySequenceExtent = new();
//         public List<PartitionMapBase> PartitionMaps = new();

//         public int SIZE => 440 + PartitionMaps.Sum(m => m.Length);

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(VolumeDescriptorSequenceNumber)));
//             s.Write(DescriptorCharacterSet.ToBytes());
//             s.Write(LogicalVolumeIdentifier.Data);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LogicalBlockSize)));
//             s.Write(DomainIdentifier.ToBytes());
//             s.Write(LogicalVolumeContentsUse);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(MapTableLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(NumberOfPartitionMaps)));
//             s.Write(ImplementationIdentifier.ToBytes());
//             s.Write(ImplementationUse);
//             s.Write(IntegritySequenceExtent.ToBytes());

//             foreach (var map in PartitionMaps)
//                 s.Write(map.Serialize());

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public enum PartitionMapType : byte
//     {
//         NotSpecified = 0,
//         Type1 = 1,
//         Type2 = 2,
//     }

//     public abstract class PartitionMapBase
//     {
//         public abstract PartitionMapType Type { get; }
//         public abstract byte Length { get; }

//         public abstract byte[] Serialize();
//     }

//     public class PartitionMapType1 : PartitionMapBase
//     {
//         public override PartitionMapType Type => PartitionMapType.Type1;
//         public override byte Length => 6;
//         public ushort VolumeSequenceNumber;
//         public ushort PartitionNumber;

//         public override byte[] Serialize()
//         {
//             var data = new byte[Length];
//             data[0] = (byte)Type;
//             data[1] = Length;
//             Array.Copy(BitConverter.GetBytes(Bits.ToLittle(VolumeSequenceNumber)), 0, data, 2, 2);
//             Array.Copy(BitConverter.GetBytes(Bits.ToLittle(PartitionNumber)), 0, data, 4, 2);
//             return data;
//         }
//     }

//     public class PartitionMapType2 : PartitionMapBase
//     {
//         public override PartitionMapType Type => PartitionMapType.Type2;
//         public override byte Length => 64;
//         public readonly byte[] PartitionIdentifier = new byte[62];

//         public override byte[] Serialize()
//         {
//             var data = new byte[Length];
//             data[0] = (byte)Type;
//             data[1] = Length;
//             Array.Copy(PartitionIdentifier, 0, data, 2, 62);
//             return data;
//         }
//     }

//     public class UnallocatedSpaceDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.UnallocatedSpaceDescriptor);
//         public uint VolumeDescriptorSequenceNumber;
//         public uint NumberOfAllocationDescriptors => (uint)AllocationDescriptors.Count;
//         public readonly List<ExtentAd> AllocationDescriptors = new();

//         public int Size() => Tag.SIZE + 4 + 4 + (AllocationDescriptors.Count * ExtentAd.SIZE);

//         public byte[] Serialize()
//         {
//             var data = new byte[Size()];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(VolumeDescriptorSequenceNumber)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(NumberOfAllocationDescriptors)));

//             foreach (var descriptor in AllocationDescriptors)
//                 s.Write(descriptor.ToBytes());

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class TerminatingDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.TerminatingDescriptor);

//         public const int SIZE = 512;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));
//             return data;
//         }
//     }

//     public enum LogicalVolumeIntegrity : uint
//     {
//         Open = 0,
//         Closed = 1,
//     }

//     public class LogicalVolumeIntegrityDescriptor
//     {
//         public class ImpUse
//         {
//             public RegId ImplementationId = new();
//             public uint NumberOfFiles;
//             public uint NumberOfDirectories;
//             public ushort MinimumUdfReadRevision;
//             public ushort MinimumUdfWriteRevision;
//             public ushort MaximumUdfWriteRevision;
//             public byte[] ImplementationUse = [];

//             public int Size() =>
//                 RegId.SIZE +
//                 sizeof(uint) + // NumberOfFiles
//                 sizeof(uint) + // NumberOfDirectories
//                 sizeof(ushort) + // MinimumUdfReadRevision
//                 sizeof(ushort) + // MinimumUdfWriteRevision
//                 sizeof(ushort) + // MaximumUdfWriteRevision
//                 ImplementationUse.Length;

//             public void Serialize(Span<byte> buffer)
//             {
//                 var s = new MemoryStream(buffer.ToArray());
//                 s.Write(ImplementationId.ToBytes());
//                 s.Write(BitConverter.GetBytes(Bits.ToLittle(NumberOfFiles)));
//                 s.Write(BitConverter.GetBytes(Bits.ToLittle(NumberOfDirectories)));
//                 s.Write(BitConverter.GetBytes(Bits.ToLittle(MinimumUdfReadRevision)));
//                 s.Write(BitConverter.GetBytes(Bits.ToLittle(MinimumUdfWriteRevision)));
//                 s.Write(BitConverter.GetBytes(Bits.ToLittle(MaximumUdfWriteRevision)));
//                 s.Write(ImplementationUse);
//             }

//             public byte[] Serialize()
//             {
//                 var buffer = new byte[Size()];
//                 Serialize(buffer);
//                 return buffer;
//             }
//         }

//         public Tag Tag = new(TagIdentifier.LogicalVolumeIntegrityDescriptor);
//         public TimeStamp RecordingDateAndTime = new();
//         public LogicalVolumeIntegrity IntegrityType;
//         public ExtentAd NextIntegrityExtent = new();
//         public readonly byte[] LogicalVolumeContentUse = new byte[32];
//         public uint NumberOfPartitions => (uint)FreeSpaceAndSizeTable.Count;
//         public uint LengthOfImplementationUse => (uint)ImplementationUse.Size();
//         private readonly List<(uint FreeSpace, uint Size)> FreeSpaceAndSizeTable = new();
//         public ImpUse ImplementationUse = new();

//         // BP Length Name Contents
//         // 0 16 Descriptor Tag tag (3/7.2) (Tag=9)
//         // 16 12 Recording Date and Time timestamp (1/7.3)
//         // 28 4 Integrity Type Uint32 (1/7.1.5)
//         // 32 8 Next Integrity Extent extent_ad (3/7.1)
//         // 40 32 Logical Volume Contents Use bytes
//         // 72 4 Number of Partitions (=N_P) Uint32 (1/7.1.5)
//         // 76 4 Length of Implementation Use (=L_IU) Uint32 (1/7.1.5)
//         // 80 N_P×4 Free Space Table Uint32 (1/7.1.5)
//         // N_P×4+80 N_P×4 Size Table Uint32 (1/7.1.5)
//         // N_P×8+80 L_IU Implementation Use bytes

//         public int Size() => 
//             Tag.SIZE + 
//             TimeStamp.SIZE + 
//             4 + 
//             ExtentAd.SIZE + 
//             32 + 
//             4 + 
//             4 + 
//             (FreeSpaceAndSizeTable.Count * 4) + 
//             (FreeSpaceAndSizeTable.Count * 4) + 
//             ImplementationUse.Size();

//         public byte[] Serialize()
//         {
//             var data = new byte[Size()];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(RecordingDateAndTime.ToBytes());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((uint)IntegrityType)));
//             s.Write(NextIntegrityExtent.ToBytes());
//             s.Write(LogicalVolumeContentUse);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(NumberOfPartitions)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LengthOfImplementationUse)));

//             foreach (var (FreeSpace, _) in FreeSpaceAndSizeTable)
//                 s.Write(BitConverter.GetBytes(Bits.ToLittle(FreeSpace)));

//             foreach (var (_, Size) in FreeSpaceAndSizeTable)
//                 s.Write(BitConverter.GetBytes(Bits.ToLittle(Size)));

//             s.Write(ImplementationUse.Serialize());

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class LbAddress : IMarshalable<LbAddress>
//     {
//         public uint LogicalBlockNumber;
//         public ushort PartitionReferenceNumber;

//         public static int SIZE => 6;
//     }

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class ShortAd : IMarshalable<ShortAd>
//     {
//         public uint Length;
//         public uint Position;

//         public static int SIZE => 8;
//     }

//     public class LongAd
//     {
//         public uint Length;
//         public LbAddress Location = new();
//         public readonly byte[] ImpUse = new byte[6];

//         public static readonly int SIZE = sizeof(uint) + LbAddress.SIZE + 6;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(Length)));
//             s.Write(Location.ToBytes());
//             s.Write(ImpUse);
//             return data;
//         }
//     }

//     public class ExtAd
//     {
//         public uint ExtLength;
//         public uint RecordedLength;
//         public uint InformationLength;
//         public LbAddress Location = new();

//         public static readonly int SIZE = sizeof(uint) * 3 + LbAddress.SIZE;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(ExtLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(RecordedLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(InformationLength)));
//             s.Write(Location.ToBytes());
//             return data;
//         }
//     }

//     public class FileSetDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.FileSetDescriptor);
//         public TimeStamp RecordingDateAndTime = new();
//         public ushort InterchangeLevel;
//         public ushort MaximumInterchangeLevel;
//         public uint CharacterSetList;
//         public uint MaximumCharacterSetList;
//         public uint FileSetNumber;
//         public uint FileSetDescriptorNumber;
//         public CharSpec LogicalVolumeIdentifierCharacterSet = new();
//         public readonly DString LogicalVolumeIdentifier = new(128);
//         public CharSpec FileSetCharacterSet = new();
//         public readonly DString FileSetIdentifier = new(32);
//         public readonly DString CopyrightFileIdentifier = new(32);
//         public readonly DString AbstractFileIdentifier = new(32);
//         public LongAd RootDirectoryICB = new();
//         public RegId DomainIdentifier = new();
//         public LongAd NextExtent = new();
//         public LongAd StreamDirectoryICB = new();

//         // public readonly byte[] Reserved = new byte[32];

//         public static readonly int SIZE = 
//             Tag.SIZE + 
//             TimeStamp.SIZE + 
//             (sizeof(ushort) * 2) + 
//             (sizeof(uint) * 4) + 
//             CharSpec.SIZE +
//             128 +
//             CharSpec.SIZE +
//             (32 * 3) +
//             LongAd.SIZE +
//             RegId.SIZE +
//             LongAd.SIZE +
//             LongAd.SIZE +
//             32;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(RecordingDateAndTime.ToBytes());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(InterchangeLevel)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(MaximumInterchangeLevel)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(CharacterSetList)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(MaximumCharacterSetList)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(FileSetNumber)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(FileSetDescriptorNumber)));
//             s.Write(LogicalVolumeIdentifierCharacterSet.ToBytes());
//             s.Write(LogicalVolumeIdentifier.Data);
//             s.Write(FileSetCharacterSet.ToBytes());
//             s.Write(FileSetIdentifier.Data);
//             s.Write(CopyrightFileIdentifier.Data);
//             s.Write(AbstractFileIdentifier.Data);
//             s.Write(RootDirectoryICB.Serialize());
//             s.Write(DomainIdentifier.ToBytes());
//             s.Write(NextExtent.Serialize());

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class PartitionHeaderDescriptor
//     {
//         public ShortAd UnallocatedSpaceTable = new();
//         public ShortAd UnallocatedSpaceBitmap = new();
//         public ShortAd PatitionIntegrityTable = new();
//         public ShortAd FreedSpacetable = new();
//         public ShortAd FreedSpaceBitmap = new();

//         // public byte[] Reserved = new byte[88];

//         public static readonly int SIZE = ShortAd.SIZE * 5 + 88;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);
//             s.Write(UnallocatedSpaceTable.ToBytes());
//             s.Write(UnallocatedSpaceBitmap.ToBytes());
//             s.Write(PatitionIntegrityTable.ToBytes());
//             s.Write(FreedSpacetable.ToBytes());
//             s.Write(FreedSpaceBitmap.ToBytes());
//             return data;
//         }
//     }

//     [Flags]
//     public enum FileIdentFileCharacteristic : byte
//     {
//         Hidden = 1 << 0,
//         Directory = 1 << 1,
//         Deleted = 1 << 2,
//         Parent = 1 << 3,
//         MetaData = 1 << 4
//     }

//     public class FileIdentDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.FileIdentDescriptor);
//         public ushort FileVersionNumber;
//         public FileIdentFileCharacteristic FileCharacteristics;
//         public byte LengthFileIdent;
//         public LongAd Icb = new();
//         public ushort LengthOfImpUse => (ushort)ImpUseAndFileIdent.Length;
//         public byte[] ImpUseAndFileIdent = [];

//         public int SIZE => 
//             Tag.SIZE + 
//             sizeof(ushort) + 
//             sizeof(byte) +
//             sizeof(byte) + 
//             LongAd.SIZE + 
//             sizeof(ushort) + 
//             ImpUseAndFileIdent.Length;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(FileVersionNumber)));
//             s.WriteByte((byte)FileCharacteristics);
//             s.WriteByte(LengthFileIdent);
//             s.Write(Icb.Serialize());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LengthOfImpUse)));
//             s.Write(ImpUseAndFileIdent);

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class AllocationExtDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.AllocationExtDescriptor);
//         public uint PreviousAllocationExtLocation;
//         public uint LengthOfAllocationDescriptors;

//         public static int SIZE => Tag.SIZE + sizeof(uint) + sizeof(uint);

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(PreviousAllocationExtLocation)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LengthOfAllocationDescriptors)));

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public enum IcbTagStrategy : ushort
//     {
//         NotSpecified = 0,
//         Type1 = 1,
//         Type2 = 2,
//         Type3 = 3,
//         Type4 = 4
//     }

//     public enum IcbTagFileType : byte
//     {
//         NotSpecified = 0,
//         USE = 1,
//         PIE = 2,
//         IE = 3,
//         DIRECTORY = 4,
//         REGULAR = 5,
//         BLOCK = 6,
//         CHAR = 7,
//         EA = 8,
//         FIFO = 9,
//         SOCKET = 0xa,
//         TE = 0xb,
//         SYMLINK = 0xc,
//         STREAMDIR = 0xd
//     }

//     [Flags]
//     public enum IcbTagFlags : ushort
//     {
//         AD_MASK = 0x0007,
//         AD_SHORT = 0x0000,
//         AD_LONG = 0x0001,
//         AD_EXTENDED = 0x0002,
//         AD_IN_ICB = 0x0003,
//         SORTED = 0x0008,
//         NONRELOCATABLE = 0x0010,
//         ARCHIVE = 0x0020,
//         SETUID = 0x0040,
//         SETGID = 0x0080,
//         STICKY = 0x0100,
//         CONTIGUOUS = 0x0200,
//         SYSTEM = 0x0400,
//         TRANSFORMED = 0x0800,
//         MULTIVERSIONS = 0x1000,
//         STREAM = 0x2000
//     }

//     public class IcbTag
//     {
//         public uint PriorRecordNumDirectEntries;
//         public IcbTagStrategy StrategyType;
//         public ushort StrategyParameter;
//         public ushort NumEntries;
//         // private readonly byte Reserved;
//         public IcbTagFileType FileType;
//         public LbAddress ParentIcbLocation = new();
//         public IcbTagFlags Flags;

//         public static int SIZE =>
//             sizeof(uint) + // PriorRecordNumDirectEntries
//             sizeof(ushort) + // StrategyType
//             sizeof(ushort) + // StrategyParameter
//             sizeof(ushort) + // NumEntries
//             sizeof(byte) + // Reserved
//             sizeof(byte) + // FileType
//             LbAddress.SIZE + // ParentIcbLocation
//             sizeof(ushort); // Flags

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle(PriorRecordNumDirectEntries)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((ushort)StrategyType)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(StrategyParameter)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(NumEntries)));
//             // s.WriteByte(Reserved);
//             s.Seek(s.Position + 1, SeekOrigin.Begin); // Reserved byte
//             s.WriteByte((byte)FileType);
//             s.Write(ParentIcbLocation.ToBytes());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((ushort)Flags)));

//             return data;
//         }
//     }

//     public class IndirectEntry
//     {
//         public Tag Tag = new(TagIdentifier.IndirectEntry);
//         public IcbTag IcbTag = new();
//         public LongAd IndirectIcb = new();

//         public static int SIZE => Tag.SIZE + IcbTag.SIZE + LongAd.SIZE;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(IcbTag.Serialize());
//             s.Write(IndirectIcb.Serialize());

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class TerminalEntry
//     {
//         public Tag Tag = new(TagIdentifier.TerminalEntry);
//         public IcbTag IcbTag = new();

//         public static int SIZE => Tag.SIZE + IcbTag.SIZE;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(IcbTag.Serialize());
//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     [Flags]
//     public enum FileEntryPermissions : uint
//     {
//         OwnerExecute = 0x1,
//         OwnerWrite = 0x2,
//         OwnerRead = 0x4,
//         OwnerChattr = 0x8,
//         OwnerDelete = 0x10,
//         GroupExecute = 0x20,
//         GroupWrite = 0x40,
//         GroupRead = 0x80,
//         GroupChattr = 0x100,
//         GroupDelete = 0x200,
//         UserExecute = 0x400,
//         UserWrite = 0x800,
//         UserRead = 0x1000,
//         UserChattr = 0x2000,
//         UserDelete = 0x4000,
//     }

//     public enum FileEntryRecordFormat : byte
//     {
//         Undefined = 0,
//         FixedPad = 1,
//         Fixed = 2,
//         Variable8 = 3,
//         Variable16 = 4,
//         Variable16Msb = 5,
//         Variable32 = 6,
//         Print = 7,
//         LF = 8,
//         CR = 9,
//         CRLF = 10,
//         LFCR = 11,
//     }

//     public enum FileEntryDisplayAttributes : byte
//     {
//         Undef = 0,
//         Attr1 = 1,
//         Attr2 = 2,
//         Attr3 = 3,
//     }

//     public class FileEntry
//     {
//         public Tag Tag = new(TagIdentifier.Fileentry);
//         public IcbTag IcbTag = new();
//         public uint Uid;
//         public uint Gid;
//         public FileEntryPermissions Permissions;
//         public ushort FileLinkCount;
//         public FileEntryRecordFormat RecordFormat;
//         public FileEntryDisplayAttributes RecordDisplayAttributes;
//         public uint RecordLength;
//         public ulong InformationLength;
//         public ulong LogicalBlocksRecorded;
//         public TimeStamp AccessTime = new();
//         public TimeStamp ModificationTime = new();
//         public TimeStamp AttributeTime = new();
//         public uint Checkpoint;
//         public LongAd ExtendedAttributeIcb = new();
//         public RegId ImplementationIdentifier = new();
//         public ulong UniqueId;
//         public uint LengthExtendedAttributes;
//         public uint LengthAllocationDescriptors;
//         public byte[] ExtendedAttributesAndAllocationDescriptors = [];

//         public int SIZE =>
//             Tag.SIZE +
//             IcbTag.SIZE +
//             sizeof(uint) + // Uid
//             sizeof(uint) + // Gid
//             sizeof(uint) + // Permissions
//             sizeof(ushort) + // FileLinkCount
//             sizeof(byte) + // RecordFormat
//             sizeof(byte) + // RecordDisplayAttributes
//             sizeof(uint) + // RecordLength
//             sizeof(ulong) + // InformationLength
//             sizeof(ulong) + // LogicalBlocksRecorded
//             TimeStamp.SIZE * 3 +
//             sizeof(uint) + // Checkpoint
//             LongAd.SIZE +
//             RegId.SIZE +
//             sizeof(ulong) + // UniqueId
//             sizeof(uint) + // LengthExtendedAttributes
//             sizeof(uint) + // LengthAllocationDescriptors
//             ExtendedAttributesAndAllocationDescriptors.Length;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(IcbTag.Serialize());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(Uid)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(Gid)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((uint)Permissions)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(FileLinkCount)));
//             s.WriteByte((byte)RecordFormat);
//             s.WriteByte((byte)RecordDisplayAttributes);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(RecordLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(InformationLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LogicalBlocksRecorded)));
//             s.Write(AccessTime.ToBytes());
//             s.Write(ModificationTime.ToBytes());
//             s.Write(AttributeTime.ToBytes());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(Checkpoint)));
//             s.Write(ExtendedAttributeIcb.Serialize());
//             s.Write(ImplementationIdentifier.ToBytes());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(UniqueId)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LengthExtendedAttributes)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LengthAllocationDescriptors)));
//             s.Write(ExtendedAttributesAndAllocationDescriptors);

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));
//             return data;
//         }
//     }

//     public class ExtendedAttributeHeaderDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.ExtendedAttributeHeaderDescriptor);
//         public uint ImplementationAttributeLocation;
//         public uint ApplicationAttributeLocation;

//         public static int SIZE => Tag.SIZE + sizeof(uint) + sizeof(uint);

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(ImplementationAttributeLocation)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(ApplicationAttributeLocation)));

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     // public class GenericFormat
//     // {
//     //     public uint AttributeType;
//     //     public byte AttributeSubType;

//     //     // public byte[] Reserved = new byte[3];
//     //     public uint AttributeLength => (uint)AttributeData.Length;
//     //     public byte[] AttributeData = [];

//     //     public int SIZE => sizeof(uint) + sizeof(byte) + 3 + sizeof(uint) + AttributeData.Length;

//     //     public byte[] Serialize()
//     //     {
//     //         var data = new byte[SIZE];
//     //         var s = new MemoryStream(data);

//     //         s.Write(BitConverter.GetBytes(Bits.ToLittle(AttributeType)));
//     //         s.WriteByte(AttributeSubType);
//     //         s.Seek(s.Position + 3, SeekOrigin.Begin); // reserved
//     //         s.Write(BitConverter.GetBytes(Bits.ToLittle(AttributeLength)));
//     //         s.Write(AttributeData);

//     //         return data;
//     //     }
//     // }

//     public enum ExtAttributeType : uint
//     {
//         CharSetInfo = 1,
//         AltPermissions = 3,
//         FileTimes = 5,
//         InfoTimes = 6,
//         DeviceSpec = 12,
//         ImpUse = 2048,
//         AppUse = 65536
//     }

//     public abstract class ExtAttribute
//     {
//         public abstract ExtAttributeType AttributeType { get; }
//         public abstract byte AttributeSubType { get; set; }

//         // public byte[] Reserved = new byte[3];
//         public abstract uint AttributeLength { get; }

//         // public byte[] AttributeData;

//         public int SIZE => sizeof(uint) + sizeof(byte) + 3 + sizeof(uint) + (int)AttributeLength;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle((uint)AttributeType)));
//             s.WriteByte(AttributeSubType);
//             s.Seek(s.Position + 3, SeekOrigin.Begin); // reserved
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(AttributeLength)));
//             s.Write(GetAttributeData());

//             return data;
//         }

//         protected abstract byte[] GetAttributeData(); 
//     }

//     public class CharSetInfo : ExtAttribute
//     {
//         public override ExtAttributeType AttributeType => ExtAttributeType.CharSetInfo;
//         public override byte AttributeSubType { get; set; } = 0;
//         public override uint AttributeLength => EscapeSequenceLength + sizeof(uint) + sizeof(byte);

//         public uint EscapeSequenceLength => (uint)EscapeSequence.Length;
//         public byte CharSetType;
//         public byte[] EscapeSequence = [];

//         protected override byte[] GetAttributeData()
//         {
//             var data = new byte[AttributeLength];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle(EscapeSequenceLength)));
//             s.WriteByte(CharSetType);
//             s.Write(EscapeSequence);

//             return data;   
//         }
//     }

//     public class AltPermissions : ExtAttribute
//     {
//         public override ExtAttributeType AttributeType => ExtAttributeType.AltPermissions;
//         public override byte AttributeSubType { get; set; } = 0;
//         public override uint AttributeLength => sizeof(ushort) * 3;
//         public ushort OwnerIdentifier;
//         public ushort GroupIdentifier;
//         public ushort Permission;

//         protected override byte[] GetAttributeData()
//         {
//             var data = new byte[AttributeLength];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle(OwnerIdentifier)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(GroupIdentifier)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(Permission)));

//             return data;
//         }
//     }

//     [Flags]
//     public enum FileTimeExistence : uint
//     {
//         None = 0,
//         Creation = 1 << 0,
//         Backup = 1 << 1,
//         Deletion = 1 << 2,
//         Effective = 1 << 3,
//     }

//     public class FileTimesExtAttribute : ExtAttribute
//     {
//         public override ExtAttributeType AttributeType => ExtAttributeType.FileTimes;
//         public override byte AttributeSubType { get; set; } = 0;
//         public override uint AttributeLength => sizeof(uint) + sizeof(uint) + sizeof(byte);// (FileTimes * TimeStamp.SIZE);
//         public uint DataLength;
//         public FileTimeExistence FileTimeExistence;
//         public byte FileTimes;

//         protected override byte[] GetAttributeData()
//         {
//             var data = new byte[AttributeLength];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle(DataLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((uint)FileTimeExistence)));
//             s.WriteByte(FileTimes);

//             return data;
//         }
//     }

//     public class InfoTimes : ExtAttribute
//     {
//         public override ExtAttributeType AttributeType => ExtAttributeType.InfoTimes;
//         public override byte AttributeSubType { get; set; } = 0;
//         public override uint AttributeLength => sizeof(uint) + sizeof(uint) + (uint)InfoTimesData.Length;

//         public uint DataLength;
//         public uint InfoTimesExistence;
//         public byte[] InfoTimesData = [];

//         protected override byte[] GetAttributeData()
//         {
//             var data = new byte[AttributeLength];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle(DataLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(InfoTimesExistence)));
//             s.Write(InfoTimesData);

//             return data;
//         }
//     }

//     public class DeviceSpec : ExtAttribute
//     {
//         public override ExtAttributeType AttributeType => ExtAttributeType.DeviceSpec;
//         public override byte AttributeSubType { get; set; } = 0;
//         public override uint AttributeLength => (sizeof(uint) * 3) + ImplementationUseLength;

//         public uint ImplementationUseLength => (uint)ImplementationUse.Length;
//         public uint MajorDeviceIdentifier;
//         public uint MinorDeviceIdentifier;
//         public byte[] ImplementationUse = [];

//         protected override byte[] GetAttributeData()
//         {
//             var data = new byte[AttributeLength];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle(ImplementationUseLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(MajorDeviceIdentifier)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(MinorDeviceIdentifier)));
//             s.Write(ImplementationUse);

//             return data;
//         }
//     }

//     public class ImpUse : ExtAttribute
//     {
//         public override ExtAttributeType AttributeType => ExtAttributeType.ImpUse;
//         public override byte AttributeSubType { get; set; } = 0;
//         public override uint AttributeLength => sizeof(uint) + (uint)RegId.SIZE + ImplementationUseLength;

//         public uint ImplementationUseLength => (uint)ImplementationUse.Length;
//         public RegId ImplementationIdentifier = new();
//         public byte[] ImplementationUse = [];

//         protected override byte[] GetAttributeData()
//         {
//             var data = new byte[AttributeLength];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle(ImplementationUseLength)));
//             s.Write(ImplementationIdentifier.ToBytes());
//             s.Write(ImplementationUse);

//             return data;
//         }
//     }

//     public class AppUse : ExtAttribute
//     {
//         public override ExtAttributeType AttributeType => ExtAttributeType.AppUse;
//         public override byte AttributeSubType { get; set; } = 0;
//         public override uint AttributeLength => sizeof(uint) + (uint)RegId.SIZE + AppUseLength;

//         public uint AppUseLength => (uint)ApplicationUse.Length;
//         public RegId AppIdentifier = new();
//         public byte[] ApplicationUse = [];

//         protected override byte[] GetAttributeData()
//         {
//             var data = new byte[AttributeLength];
//             var s = new MemoryStream(data);

//             s.Write(BitConverter.GetBytes(Bits.ToLittle(AppUseLength)));
//             s.Write(AppIdentifier.ToBytes());
//             s.Write(ApplicationUse);

//             return data;
//         }
//     }

//     public class UnallocSpaceEntry
//     {
//         public Tag Tag = new(TagIdentifier.UnallocSpaceEntry);
//         public IcbTag IcbTag = new();
//         public uint LengthAllocDescriptors => (uint)AllocationDescriptors.Length;
//         public byte[] AllocationDescriptors = [];

//         public int SIZE => Tag.SIZE + IcbTag.SIZE + sizeof(uint) + AllocationDescriptors.Length;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(IcbTag.Serialize());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LengthAllocDescriptors)));
//             s.Write(AllocationDescriptors);

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class SpaceBitmapDescriptor
//     {
//         public Tag Tag = new(TagIdentifier.SpaceBitmapDescriptor);
//         public uint NumberOfBits;
//         public uint NumberOfBytes => (uint)Bitmap.Length;
//         public byte[] Bitmap = [];

//         public int SIZE => Tag.SIZE + sizeof(uint) + sizeof(uint) + Bitmap.Length;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(NumberOfBits)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(NumberOfBytes)));
//             s.Write(Bitmap);

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public class PartitionIntegrityEntry
//     {
//         public Tag Tag = new(TagIdentifier.PartitionIntegrityEntry);
//         public IcbTag IcbTag = new();
//         public TimeStamp RecordingDateAndTime = new();
//         public byte IntegrityType;

//         // public byte[] Reserved = new byte[175];
//         public RegId ImplementationIdentifier = new();
//         public byte[] ImpUse = [];

//         public int SIZE => Tag.SIZE + IcbTag.SIZE + TimeStamp.SIZE + sizeof(byte) + 175 + RegId.SIZE + ImpUse.Length;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(IcbTag.Serialize());
//             s.Write(RecordingDateAndTime.ToBytes());
//             s.WriteByte(IntegrityType);
//             s.Seek(s.Position + 175, SeekOrigin.Begin); // reserved
//             s.Write(ImplementationIdentifier.ToBytes());
//             s.Write(ImpUse);

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }

//     public enum ExtentLength : uint
//     {
//         LengthMask = 0x3fffffff,
//         TypeMask = 0xc0000000,
//         RecordedAllocated = 0x00000000,
//         NotRecordedAllocated = 0x40000000,
//         NotRecordedNotAllocated = 0x80000000,
//         NextExtentAllocDescs = 0xc0000000
//     }

//     [StructLayout(LayoutKind.Sequential, Pack = 1)]
//     public class LogicVolumeHeaderDescriptor : IMarshalable<LogicVolumeHeaderDescriptor>
//     {
//         public ulong UniqueueId;

//         [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
//         public byte[] Reserved = new byte[24];

//         public static int SIZE => sizeof(ulong) + 24;
//     }

//     public class PathComponent
//     {
//         public byte ComponentType;
//         public byte LengthComponentIdentifier => (byte)ComponentIdentifier.Length;
//         public ushort ComponentFileVersionNum;
//         public byte[] ComponentIdentifier = [];

//         public int SIZE => sizeof(byte) + sizeof(byte) + sizeof(ushort) + ComponentIdentifier.Length;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.WriteByte(ComponentType);
//             s.WriteByte(LengthComponentIdentifier);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(ComponentFileVersionNum)));
//             s.Write(ComponentIdentifier);

//             return data;
//         }
//     }

//     public class ExtendedFileEntry
//     {
//         public Tag Tag = new(TagIdentifier.ExtendedFileEntry);
//         public IcbTag IcbTag = new();
//         public uint Uid;
//         public uint Gid;
//         public FileEntryPermissions Permissions;
//         public ushort FileLinkCount;
//         public FileEntryRecordFormat RecordFormat;
//         public FileEntryDisplayAttributes RecordDisplayAttributes;
//         public uint RecordLength;
//         public ulong InformationLength;
//         public ulong LogicalBlocksRecorded;
//         public TimeStamp AccessTime = new();
//         public TimeStamp ModificationTime = new();
//         public TimeStamp CreationTime = new();
//         public TimeStamp AttributeTime = new();
//         public uint Checkpoint;

//         // private readonly uint Reserved;
//         public LongAd ExtendedAttributeIcb = new();
//         public LongAd StreamDirectoryIcb = new();
//         public RegId ImplementationIdentifier = new();
//         public ulong UniqueId;
//         public uint LengthExtendedAttributes;
//         public uint LengthAllocationDescriptors;
//         public byte[] ExtendedAttributesAndAllocationDescriptors = [];

//         public int SIZE =>
//             Tag.SIZE +
//             IcbTag.SIZE +
//             sizeof(uint) + // Uid
//             sizeof(uint) + // Gid
//             sizeof(uint) + // Permissions
//             sizeof(ushort) + // FileLinkCount
//             sizeof(byte) + // RecordFormat
//             sizeof(byte) + // RecordDisplayAttributes
//             sizeof(uint) + // RecordLength
//             sizeof(ulong) + // InformationLength
//             sizeof(ulong) + // LogicalBlocksRecorded
//             TimeStamp.SIZE * 4 +
//             sizeof(uint) + // Checkpoint
//             sizeof(uint) + // Reserved
//             LongAd.SIZE +
//             LongAd.SIZE +
//             RegId.SIZE +
//             sizeof(ulong) + // UniqueId
//             sizeof(uint) + // LengthExtendedAttributes
//             sizeof(uint) + // LengthAllocationDescriptors
//             ExtendedAttributesAndAllocationDescriptors.Length;

//         public byte[] Serialize()
//         {
//             var data = new byte[SIZE];
//             var s = new MemoryStream(data);

//             s.Seek(Tag.SIZE, SeekOrigin.Begin);
//             s.Write(IcbTag.Serialize());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(Uid)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(Gid)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle((uint)Permissions)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(FileLinkCount)));
//             s.WriteByte((byte)RecordFormat);
//             s.WriteByte((byte)RecordDisplayAttributes);
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(RecordLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(InformationLength)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LogicalBlocksRecorded)));
//             s.Write(AccessTime.ToBytes());
//             s.Write(ModificationTime.ToBytes());
//             s.Write(CreationTime.ToBytes());
//             s.Write(AttributeTime.ToBytes());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(Checkpoint)));
//             // s.Write(BitConverter.GetBytes(Bits.ToLittle(Reserved)));
//             s.Seek(s.Position + sizeof(uint), SeekOrigin.Begin); // reserved
//             s.Write(ExtendedAttributeIcb.Serialize());
//             s.Write(StreamDirectoryIcb.Serialize());
//             s.Write(ImplementationIdentifier.ToBytes());
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(UniqueId)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LengthExtendedAttributes)));
//             s.Write(BitConverter.GetBytes(Bits.ToLittle(LengthAllocationDescriptors)));
//             s.Write(ExtendedAttributesAndAllocationDescriptors);

//             s.Seek(0, SeekOrigin.Begin);
//             s.Write(Tag.Finalize(data.AsSpan(Tag.SIZE)));

//             return data;
//         }
//     }
// }
