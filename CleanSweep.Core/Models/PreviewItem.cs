namespace CleanSweep.Core.Models;

public class PreviewItem
{
    public string Path { get; set; }
    public long SizeBytes { get; set; }
    public string Risk { get; set; } // "green", "yellow", "red"
    public bool IsRecent { get; set; }
    public string ImportanceKey { get; set; }
    public bool IsDirectory { get; set; }
    public bool AllowRecentDeletion { get; set; }

    public PreviewItem(
        string path,
        long sizeBytes = 0,
        string risk = "green",
        bool isRecent = false,
        string importanceKey = "",
        bool isDirectory = false,
        bool allowRecentDeletion = false)
    {
        Path = path;
        SizeBytes = sizeBytes;
        Risk = risk;
        IsRecent = isRecent;
        ImportanceKey = importanceKey;
        IsDirectory = isDirectory;
        AllowRecentDeletion = allowRecentDeletion;
    }
}
