namespace XGDTool.Lib.Util;

public static class Bits
{
    public static uint Upper32(ulong value) => (uint)(value >> 32);
    public static ushort Upper16(uint value) => (ushort)(value >> 16);
    public static byte Upper8(ushort value) => (byte)(value >> 8);
    public static uint Lower32(ulong value) => (uint)(value & 0xFFFFFFFF);
    public static ushort Lower16(uint value) => (ushort)(value & 0xFFFF);
    public static byte Lower8(ushort value) => (byte)(value & 0xFF);
    public static ulong Combine64(uint upper, uint lower) => ((ulong)upper << 32) | lower;
    public static uint Combine32(ushort upper, ushort lower) => ((uint)upper << 16) | lower;
    public static ushort Combine16(byte upper, byte lower) => (ushort)(((uint)upper << 8) | lower);
    public static uint Set16At(uint original, ushort value, int msbIndex) => (original & ~(0xFFFFu << (32 - 16 - msbIndex))) | (((uint)value & 0xFFFFu) << (32 - 16 - msbIndex));
    public static uint Set8At(uint original, byte value, int msbIndex) => (original & ~(0xFFu << (32 - 8 - msbIndex))) | (((uint)value & 0xFFu) << (32 - 8 - msbIndex));
    public static uint Set4At(uint original, byte value, int msbIndex) => (original & ~(0xFu << (32 - 4 - msbIndex))) | (((uint)value & 0xFu) << (32 - 4 - msbIndex));
    public static ushort Get16At(uint value, int msbIndex) => (ushort)((value >> (32 - 16 - msbIndex)) & 0xFFFFu);
    public static byte Get8At(uint value, int msbIndex) => (byte)((value >> (32 - 8 - msbIndex)) & 0xFFu);
    public static byte Get4At(uint value, int msbIndex) => (byte)((value >> (32 - 4 - msbIndex)) & 0xFu);
}
