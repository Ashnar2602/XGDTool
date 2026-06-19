// using System.Runtime.InteropServices;

// namespace XGDTool.Lib.Util;

// public interface IMarshalable<TSelf> where TSelf : IMarshalable<TSelf>
// {
//     static abstract int SIZE { get; }
// }

// public static class Marshalable
// {
//     public static void FromBytes<TSelf>(this TSelf instance, ReadOnlySpan<byte> data)
//         where TSelf : class, IMarshalable<TSelf>
//     {
//         if (data.Length < TSelf.SIZE)
//             throw new ArgumentException("Buffer too small.");

//         byte[] temp = data.Slice(0, TSelf.SIZE).ToArray();
//         var handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
//         try
//         {
//             Marshal.PtrToStructure(handle.AddrOfPinnedObject(), instance);
//         }
//         finally
//         {
//             handle.Free();
//         }
//     }

//     public static TSelf FromBytes<TSelf>(ReadOnlySpan<byte> data)
//         where TSelf : class, IMarshalable<TSelf>, new()
//     {
//         var instance = new TSelf();
//         instance.FromBytes(data);
//         return instance;
//     }

//     public static void ToBytes<TSelf>(this TSelf value, Span<byte> buffer)
//         where TSelf : class, IMarshalable<TSelf>
//     {
//         if (buffer.Length < TSelf.SIZE)
//             throw new ArgumentException("Buffer too small.");

//         byte[] temp = new byte[TSelf.SIZE];
//         var handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
//         try
//         {
//             Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
//         }
//         finally
//         {
//             handle.Free();
//         }

//         temp.AsSpan().CopyTo(buffer);
//     }

//     public static byte[] ToBytes<TSelf>(this TSelf value)
//         where TSelf : class, IMarshalable<TSelf>
//     {
//         var data = new byte[TSelf.SIZE];
//         value.ToBytes(data);
//         return data;
//     }
// }
