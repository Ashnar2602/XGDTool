using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace XGDTool.Lib.Util;

public interface IMarshalable
{
    int Size();
}

public static class MarshalableExt
{
    public static void FromBytes(this IMarshalable instance, ReadOnlySpan<byte> data)
    {
        Marshalable.ReadInto(data, instance, instance.Size());
    }

    public static T FromBytes<T>(ReadOnlySpan<byte> data) where T : IMarshalable, new()
    {
        var instance = new T();
        instance.FromBytes(data);
        return instance;
    }

    public static void ToBytes(this IMarshalable value, Span<byte> buffer)
    {
        Marshalable.WriteObject(value, buffer, value.Size());
    }

    public static byte[] ToBytes(this IMarshalable value)
    {
        var data = new byte[value.Size()];
        value.ToBytes(data);
        return data;
    }
}

internal static class Marshalable
{
    public static T Read<T>(ReadOnlySpan<byte> data) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        if (data.Length < size) throw new ArgumentException("Buffer too small.");

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return MemoryMarshal.Read<T>(data);

        byte[] temp = data.Slice(0, size).ToArray();
        var handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public static void Write<T>(in T value, Span<byte> buffer) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        if (buffer.Length < size) throw new ArgumentException("Buffer too small.");

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            MemoryMarshal.Write(buffer, in value);
            return;
        }

        byte[] temp = new byte[size];
        var handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }

        temp.AsSpan().CopyTo(buffer);
    }

    public static void ReadInto(ReadOnlySpan<byte> data, IMarshalable instance, int size)
    {
        //if (data.Length < size)
        //    throw new ArgumentException("Buffer too small.");

        byte[] temp = data.Slice(0, data.Length).ToArray();
        var handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
        try
        {
            Marshal.PtrToStructure(handle.AddrOfPinnedObject(), instance);
        }
        finally
        {
            handle.Free();
        }
    }

    public static void WriteObject(IMarshalable value, Span<byte> buffer, int size)
    {
        if (buffer.Length < size)
            throw new ArgumentException("Buffer too small.");

        byte[] temp = new byte[size];
        var handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }

        temp.AsSpan().CopyTo(buffer);
    }

    public static T FromBytes<T>(ReadOnlySpan<byte> data) where T : IMarshalable, new()
    {
        var instance = new T();
        instance.FromBytes(data);
        return instance;
    }

    public static byte[] ToBytes<T>(this T value) where T : IMarshalable
    {
        var data = new byte[value.Size()];
        value.ToBytes(data);
        return data;
    }
}
