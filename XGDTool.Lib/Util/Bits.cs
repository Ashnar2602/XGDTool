using System.Numerics;
using System.Runtime.CompilerServices;

namespace XGDTool.Lib.Util;

public static class Bits
{
    public static uint UintFromString(string str)
    {
        if (str.Length > 4)
            throw new ArgumentException("String must be <= 4 characters");

        uint result = 0;

        for (int i = 0; i < str.Length; i++)
            result |= (uint)(byte)str[i] << (8 * i);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FromLittle<T>(T value) where T : IBinaryInteger<T> => ToLittle(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T FromBig<T>(T value) where T : IBinaryInteger<T> => ToBig(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToLittle<T>(T value) where T : IBinaryInteger<T>
    {
        if (BitConverter.IsLittleEndian)
            return value;

        int size = Unsafe.SizeOf<T>();
        Span<byte> bytes = stackalloc byte[size];
        value.WriteLittleEndian(bytes);
        return T.ReadBigEndian(bytes, isUnsigned: IsUnsigned<T>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToBig<T>(T value) where T : IBinaryInteger<T>
    {
        if (!BitConverter.IsLittleEndian)
            return value;

        int size = Unsafe.SizeOf<T>();
        Span<byte> bytes = stackalloc byte[size];
        value.WriteBigEndian(bytes);
        return T.ReadLittleEndian(bytes, isUnsigned: IsUnsigned<T>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUnsigned<T>() where T : IBinaryInteger<T>
    {
        var type = typeof(T);

        return type == typeof(byte) ||
               type == typeof(ushort) ||
               type == typeof(uint) ||
               type == typeof(ulong) ||
               type == typeof(nuint) ||
               type == typeof(UInt128);
    }
}
