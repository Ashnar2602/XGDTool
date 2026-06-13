namespace XGDTool.Lib.Converter;

public class Progress
{
    public Stage Stage { get; set; }
    public double Current { get; set; }
    public double Total { get; set; }

    public double Percent => 
        (Current >= Total) 
            ? 1.0 
            : (Total == 0) 
                ? 0.0 
                : ((double)Current / Total);
}
