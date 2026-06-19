namespace XGDTool.Lib.Image;

public readonly struct SectorRange
{
    public readonly uint Start;
    public readonly uint End;

    public SectorRange(uint start, uint end)
    {
        Start = start;
        End = end;
    }

    public SectorRange(long start, long end)
    {
        Start = checked((uint)start);
        End = checked((uint)end);
    }

    public bool Contains(uint sector) => sector >= Start && sector <= End;
}
