namespace XGDTool.Lib.Converter;

public class ProgressContext
{
    public Progress Progress { get; set; } = new();
    public IProgress<Progress>? Reporter { get; set; }
    public CancellationToken Ct { get; set; }

    public void Report(double current)
    {
        Progress.Current = current;
        Reporter?.Report(Progress);
    }

    public void ReportIncrement(double increment)
    {
        Progress.Current += increment;
        Reporter?.Report(Progress);
    }
}
