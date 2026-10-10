namespace CleanSweep.Core.Models;

public sealed class DiskUsageEntry
{
    public string Name { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public int FileCount { get; init; }
}

public sealed class DiskDirectoryUsage
{
    public long SizeBytes { get; init; }
    public int FileCount { get; init; }
}

public sealed class DiskLargestFile
{
    public string Path { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

public sealed class DiskStatisticsResult
{
    public string RootPath { get; init; } = string.Empty;
    public long TotalCapacityBytes { get; init; }
    public long DriveUsedBytes { get; init; }
    public long FreeBytes { get; init; }
    public long ScannedBytes { get; init; }
    public int FileCount { get; init; }
    public int UnavailablePathCount { get; init; }
    public IReadOnlyList<DiskUsageEntry> Folders { get; init; } = Array.Empty<DiskUsageEntry>();
    public IReadOnlyList<DiskLargestFile> LargestFiles { get; init; } = Array.Empty<DiskLargestFile>();
    public IReadOnlyList<DiskLargestFile> RootFiles { get; init; } = Array.Empty<DiskLargestFile>();
    public IReadOnlyDictionary<string, DiskDirectoryUsage> DirectoryUsage { get; init; } =
        new Dictionary<string, DiskDirectoryUsage>(StringComparer.OrdinalIgnoreCase);
}

public sealed record DiskScanProgress(int ItemsScanned, string CurrentPath);

public sealed class DiskDirectoryContents
{
    public IReadOnlyList<DiskUsageEntry> Folders { get; init; } = Array.Empty<DiskUsageEntry>();
    public IReadOnlyList<DiskLargestFile> Files { get; init; } = Array.Empty<DiskLargestFile>();
    public int UnavailablePathCount { get; init; }
}

public sealed class DiskFolderDeletionContents
{
    public IReadOnlyList<DiskLargestFile> Files { get; init; } = Array.Empty<DiskLargestFile>();
    public IReadOnlyList<string> Directories { get; init; } = Array.Empty<string>();
}
