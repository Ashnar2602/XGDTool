namespace XGDTool.Lib.Converter;

public struct Progress
{
    public Stage Stage;
    public long Current;
    public long Total;

    public readonly double Percent => 
        (Total == 0) ? 0 : 
            (Current> Total) ? 1.0 : ((double)Current / Total);
}
