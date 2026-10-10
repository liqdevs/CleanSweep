namespace CleanSweep.Core.Models;

public class Category
{
    public string Key { get; set; }
    public List<ScanTarget> Targets { get; set; } = new();

    // Populated by scanner
    public long SizeBytes { get; set; }
    public int ItemCount { get; set; }
    public bool Exists { get; set; }
    public string? Error { get; set; }

    // UI state
    public bool Selected { get; set; }

    // Preview list for confirmation
    public List<PreviewItem> PreviewItems { get; set; } = new();

    public Category(string key, IEnumerable<ScanTarget>? targets = null, bool selected = true)
    {
        Key = key;
        if (targets != null)
        {
            Targets.AddRange(targets);
        }
        Selected = selected;
    }
}

