namespace CleanSweep.Core.Models;

public class ScanTarget
{
    public string Path { get; set; }
    public TargetKind Kind { get; set; }
    public string Pattern { get; set; }

    public ScanTarget(string path, TargetKind kind, string pattern = "*")
    {
        Path = path;
        Kind = kind;
        Pattern = pattern;
    }
}

