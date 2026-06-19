using System.Text;

namespace XGDTool.Lib.Util;

public static class StringExt
{
    public static uint GetUint(string str)
    {
        uint result = 0;
        var asciiBytes = Encoding.ASCII.GetBytes(str);

        for (int i = 0; i < str.Length; i++)
            result |= (uint)asciiBytes[i] << (8 * i);

        return result;
    }

    public static ushort GetUshort(string str)
    {
        ushort result = 0;
        var asciiBytes = Encoding.ASCII.GetBytes(str);

        for (int i = 0; i < str.Length; i++)
            result |= (ushort)(asciiBytes[i] << (8 * i));

        return result;
    }

    public static byte GetByte(string str)
    {
        if (str.Length < 1)
            throw new ArgumentException("String must be 1 character");

        return Encoding.ASCII.GetBytes(str)[0];
    }
}
