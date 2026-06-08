namespace XGDTool.Lib.Converter;

public struct Progress
{
    public Stage Stage;
    public double Current;
    public double Total;

    public readonly double Percent => 
        (Current >= Total) 
            ? 1.0 
            : (Total == 0) 
                ? 0.0 
                : ((double)Current / Total);
}
