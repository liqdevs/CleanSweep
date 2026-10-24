namespace CleanSweep.Core.Models;

public class CleanError
{
    public string Key { get; set; }
    public string Detail { get; set; }

    public CleanError(string key, string detail = "")
    {
        Key = key;
        Detail = detail;
    }
}

public class CleanResult
{
    public long FreedBytes { get; set; }
    public int RemovedItems { get; set; }
    public int SkippedItems { get; set; }
    public List<CleanError> Errors { get; set; } = new();
}

public sealed record CleanProgress(int ItemsProcessed, string CurrentPath);
