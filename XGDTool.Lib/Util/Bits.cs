using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace XGDTool.Lib.Util;

public static class Bits
{
    public static uint UintFromString(string str)
    {
        if (str.Length > 4)
            throw new ArgumentException("String must be <= 4 characters");

        uint result = 0;

        for (int i = 0; i < str.Length; i++)
            result |= (uint)str[i] << (8 * (3 - i));

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

        int size = value.GetByteCount();
        Span<byte> bytes = stackalloc byte[size];
        value.WriteBigEndian(bytes);
        bytes.Reverse();
        return T.ReadLittleEndian(
            bytes.ToArray(),
            isUnsigned:
                typeof(T) != typeof(long) &&
                typeof(T) != typeof(int) &&
                typeof(T) != typeof(short) &&
                typeof(T) != typeof(sbyte));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToBig<T>(T value) where T : IBinaryInteger<T>
    {
        if (!BitConverter.IsLittleEndian)
            return value;

        int size = value.GetByteCount();
        Span<byte> bytes = stackalloc byte[size];
        value.WriteLittleEndian(bytes);
        bytes.Reverse();
        return T.ReadBigEndian(
            bytes.ToArray(), 
            isUnsigned: 
                typeof(T) != typeof(long) && 
                typeof(T) != typeof(int) && 
                typeof(T) != typeof(short) && 
                typeof(T) != typeof(sbyte));
    }
}
