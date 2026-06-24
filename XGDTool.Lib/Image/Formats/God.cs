using System.Runtime.CompilerServices;

namespace XGDTool.Lib.Image.Formats;

public static class GOD
{
    public enum Type : uint
    {
        GamesOnDemand = 0x7000,
        OriginalXbox = 0x5000
    }

    public const int BLOCK_SIZE = 0x1000;
    public const int BLOCK_SHIFT = 12;
    public const int BLOCKS_PER_PART = 41616;
    public const int DATA_BLOCKS_PER_SHT = 204;
    public const int SHT_PER_MHT = 203;
    public const int DATA_BLOCKS_PER_PART = DATA_BLOCKS_PER_SHT * SHT_PER_MHT;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SubHashTableCount(long size)
    {
        var blockCount = BlockCount(size);
        var count =
            (blockCount - 1) /
            (DATA_BLOCKS_PER_SHT + 1) +
            ((blockCount - 1) % (DATA_BLOCKS_PER_SHT + 1) > 0 ? 1 : 0);
        return (int)count; 
    }

    public static long AlignUpToBlock(long value) => (value + BLOCK_SIZE - 1) & ~(BLOCK_SIZE - 1);

    public static long AlignDownToBlock(long value) => value & ~(BLOCK_SIZE - 1);

    public static uint BlockCount(long size) => (uint)(AlignUpToBlock(size) >> BLOCK_SHIFT);

    public static uint BlockIndex(long offset) => (uint)(offset >> BLOCK_SHIFT);

    public static bool IsBlockAligned(long value) => (value & (BLOCK_SIZE - 1)) == 0;

    public static byte[] GetLiveHeaderTemplate()
    {
        var buffer = new byte[45056];

        WriteSegment(buffer, 0, new byte[]
        {
                0x4C, 0x49, 0x56, 0x45
        });
        WriteSegment(buffer, 556, new byte[]
        {
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
        });
        WriteSegment(buffer, 812, new byte[]
        {
                0xC0, 0x65, 0x30, 0xD6, 0x87, 0xEB, 0x08, 0x3F, 0x30, 0x55, 0xAD, 0xF6, 0xF1, 0x7F, 0x43, 0x4A,
                0xFF, 0xFC, 0x48, 0x95
        });
        WriteSegment(buffer, 834, new byte[]
        {
                0xAD, 0x0E
        });
        WriteSegment(buffer, 838, new byte[]
        {
                0x70
        });
        WriteSegment(buffer, 843, new byte[]
        {
                0x02
        });
        WriteSegment(buffer, 859, new byte[]
        {
                0x0A
        });
        WriteSegment(buffer, 863, new byte[]
        {
                0x0A
        });
        WriteSegment(buffer, 870, new byte[]
        {
                0x01, 0x01
        });
        WriteSegment(buffer, 889, new byte[]
        {
                0x24, 0x05, 0x05, 0x11, 0xAA, 0x36, 0x9F, 0x3A, 0xD5, 0x2A, 0xA7, 0xA2, 0x8E, 0xC4, 0x85, 0x39,
                0x90, 0xB5, 0x89, 0x5B, 0x65, 0xB5, 0x2F, 0x85, 0x40, 0x54, 0x42
        });
        WriteSegment(buffer, 917, new byte[]
        {
                0x4E, 0x41
        });
        WriteSegment(buffer, 928, new byte[]
        {
                0x44, 0x4E
        });
        WriteSegment(buffer, 940, new byte[]
        {
                0x01
        });
        WriteSegment(buffer, 3346, new byte[]
        {
                0x54
        });
        WriteSegment(buffer, 3348, new byte[]
        {
                0x68
        });
        WriteSegment(buffer, 3350, new byte[]
        {
                0x69
        });
        WriteSegment(buffer, 3352, new byte[]
        {
                0x73
        });
        WriteSegment(buffer, 3354, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3356, new byte[]
        {
                0x69
        });
        WriteSegment(buffer, 3358, new byte[]
        {
                0x73
        });
        WriteSegment(buffer, 3360, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3362, new byte[]
        {
                0x61
        });
        WriteSegment(buffer, 3364, new byte[]
        {
                0x6E
        });
        WriteSegment(buffer, 3366, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3368, new byte[]
        {
                0x69
        });
        WriteSegment(buffer, 3370, new byte[]
        {
                0x6E
        });
        WriteSegment(buffer, 3372, new byte[]
        {
                0x73
        });
        WriteSegment(buffer, 3374, new byte[]
        {
                0x74
        });
        WriteSegment(buffer, 3376, new byte[]
        {
                0x61
        });
        WriteSegment(buffer, 3378, new byte[]
        {
                0x6C
        });
        WriteSegment(buffer, 3380, new byte[]
        {
                0x6C
        });
        WriteSegment(buffer, 3382, new byte[]
        {
                0x65
        });
        WriteSegment(buffer, 3384, new byte[]
        {
                0x64
        });
        WriteSegment(buffer, 3386, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3388, new byte[]
        {
                0x67
        });
        WriteSegment(buffer, 3390, new byte[]
        {
                0x61
        });
        WriteSegment(buffer, 3392, new byte[]
        {
                0x6D
        });
        WriteSegment(buffer, 3394, new byte[]
        {
                0x65
        });
        WriteSegment(buffer, 3396, new byte[]
        {
                0x2E
        });
        WriteSegment(buffer, 3398, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3400, new byte[]
        {
                0x54
        });
        WriteSegment(buffer, 3402, new byte[]
        {
                0x6F
        });
        WriteSegment(buffer, 3404, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3406, new byte[]
        {
                0x70
        });
        WriteSegment(buffer, 3408, new byte[]
        {
                0x6C
        });
        WriteSegment(buffer, 3410, new byte[]
        {
                0x61
        });
        WriteSegment(buffer, 3412, new byte[]
        {
                0x79
        });
        WriteSegment(buffer, 3414, new byte[]
        {
                0x2C
        });
        WriteSegment(buffer, 3416, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3418, new byte[]
        {
                0x69
        });
        WriteSegment(buffer, 3420, new byte[]
        {
                0x6E
        });
        WriteSegment(buffer, 3422, new byte[]
        {
                0x73
        });
        WriteSegment(buffer, 3424, new byte[]
        {
                0x65
        });
        WriteSegment(buffer, 3426, new byte[]
        {
                0x72
        });
        WriteSegment(buffer, 3428, new byte[]
        {
                0x74
        });
        WriteSegment(buffer, 3430, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3432, new byte[]
        {
                0x74
        });
        WriteSegment(buffer, 3434, new byte[]
        {
                0x68
        });
        WriteSegment(buffer, 3436, new byte[]
        {
                0x65
        });
        WriteSegment(buffer, 3438, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3440, new byte[]
        {
                0x6F
        });
        WriteSegment(buffer, 3442, new byte[]
        {
                0x72
        });
        WriteSegment(buffer, 3444, new byte[]
        {
                0x69
        });
        WriteSegment(buffer, 3446, new byte[]
        {
                0x67
        });
        WriteSegment(buffer, 3448, new byte[]
        {
                0x69
        });
        WriteSegment(buffer, 3450, new byte[]
        {
                0x6E
        });
        WriteSegment(buffer, 3452, new byte[]
        {
                0x61
        });
        WriteSegment(buffer, 3454, new byte[]
        {
                0x6C
        });
        WriteSegment(buffer, 3456, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3458, new byte[]
        {
                0x67
        });
        WriteSegment(buffer, 3460, new byte[]
        {
                0x61
        });
        WriteSegment(buffer, 3462, new byte[]
        {
                0x6D
        });
        WriteSegment(buffer, 3464, new byte[]
        {
                0x65
        });
        WriteSegment(buffer, 3466, new byte[]
        {
                0x20
        });
        WriteSegment(buffer, 3468, new byte[]
        {
                0x64
        });
        WriteSegment(buffer, 3470, new byte[]
        {
                0x69
        });
        WriteSegment(buffer, 3472, new byte[]
        {
                0x73
        });
        WriteSegment(buffer, 3474, new byte[]
        {
                0x63
        });
        WriteSegment(buffer, 3476, new byte[]
        {
                0x2E
        });
        WriteSegment(buffer, 5908, new byte[]
        {
                0x38, 0x41
        });
        WriteSegment(buffer, 5912, new byte[]
        {
                0x38, 0x41
        });

        return buffer;
    }

    private static void WriteSegment(byte[] destination, int offset, byte[] segment)
    {
        Buffer.BlockCopy(segment, 0, destination, offset, segment.Length);
    }
}
