namespace XGDTool.Lib.Image.Reader;

public readonly struct SectorRange(uint start, uint end)
{
    public readonly uint Start = start;
    public readonly uint End = end;

    public bool Contains(uint sector) => sector >= Start && sector <= End;
}
