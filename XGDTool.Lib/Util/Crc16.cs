namespace XGDTool.Lib.Util;

public class Crc16 : Crc16Modbus { }

public class Crc16Modbus
{
    private const ushort Polynomial = 0xA001;
    private readonly ushort[] Table = new ushort[256];
    private ushort Checksum = 0;

    public Crc16Modbus() 
    {
        for (ushort i = 0; i < Table.Length; ++i) 
        {
            ushort value = 0;
            ushort temp = i;

            for (byte j = 0; j < 8; ++j) 
            {
                if (((value ^ temp) & 0x0001) != 0)
                    value = (ushort)((value >> 1) ^ Polynomial);
                else
                    value >>= 1;
                
                temp >>= 1;
            }
            Table[i] = value;
        }
    }

    public ushort UpdateChecksum(ReadOnlySpan<byte> data) 
    {
        for (int i = 0; i < data.Length; ++i) 
        {
            byte index = (byte)(Checksum ^ data[i]);
            Checksum = (ushort)((Checksum >> 8) ^ Table[index]);
        }
        return Checksum;
    }

    public static ushort CalculateChecksum(ReadOnlySpan<byte> data) =>
        new Crc16Modbus().UpdateChecksum(data);
}

public class Crc16Ccit
{
    private const ushort Polynomial = 0x1021;
    private readonly ushort[] Table = new ushort[256];
    private ushort Checksum = 0xFFFF;

    public Crc16Ccit() 
    {
        ushort value;
        ushort temp;

        for (ushort i = 0; i < Table.Length; ++i) 
        {
            value = 0;
            temp = (ushort)(i << 8);

            for (byte j = 0; j < 8; ++j) 
            {
                if (((value ^ temp) & 0x8000) != 0)
                    value = (ushort)((value << 1) ^ Polynomial);
                else
                    value <<= 1;
                
                temp <<= 1;
            }
            Table[i] = value;
        }
    }

    public ushort UpdateChecksum(ReadOnlySpan<byte> data) 
    {
        for (int i = 0; i < data.Length; ++i) 
        {
            byte index = (byte)(((Checksum >> 8) ^ data[i]) & 0xFF);
            Checksum = (ushort)((Checksum << 8) ^ Table[index]);
        }
        return Checksum;
    }

    public static ushort CalculateChecksum(ReadOnlySpan<byte> data) => 
        new Crc16Ccit().UpdateChecksum(data);
}
