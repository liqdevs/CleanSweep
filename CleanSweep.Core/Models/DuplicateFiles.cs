namespace CleanSweep.Core.Models;

public sealed record DuplicateFile(string Path, long SizeBytes, DateTime LastWriteTimeUtc);

public sealed record DuplicateFileGroup(string Hash, IReadOnlyList<DuplicateFile> Files)
{
    public long SizeBytes => Files.Count == 0 ? 0 : Files[0].SizeBytes;
    public long RecoverableBytes => SizeBytes * Math.Max(0, Files.Count - 1);
}

public sealed record DuplicateScanProgress(int FilesScanned, string CurrentPath);

public sealed record DuplicateScanResult(
    IReadOnlyList<DuplicateFileGroup> Groups,
    int FilesScanned,
    int AccessErrors);

public sealed record EmptyFolderScanResult(
    IReadOnlyList<string> EmptyDirectories,
    int DirectoriesScanned,
    int AccessErrors);
