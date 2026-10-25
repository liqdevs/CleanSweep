using System.Collections.Generic;
using System.IO;
using System.Security;
using CleanSweep.Core.Models;

namespace CleanSweep.Core.Scanning;

public static class DiskStatisticsScanner
{
    private const int LargestFileLimit = 10;
    private const int VisibleFileLimit = 200;
    private const int ProgressInterval = 128;

    public static DiskFolderDeletionContents GetFolderDeletionContents(
        string directoryPath,
        string volumeRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("A directory path is required.", nameof(directoryPath));
        }
        if (string.IsNullOrWhiteSpace(volumeRoot))
        {
            throw new ArgumentException("A volume root is required.", nameof(volumeRoot));
        }

        string root = Path.GetFullPath(volumeRoot);
        string target = Path.GetFullPath(directoryPath);
        if (!IsWithinSelectedVolume(target, root))
        {
            throw new ArgumentException("The directory must be inside the selected volume.", nameof(directoryPath));
        }
        if (string.Equals(
                target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The selected volume root cannot be deleted.", nameof(directoryPath));
        }

        var rootInfo = new DirectoryInfo(target);
        if (!rootInfo.Exists)
        {
            throw new DirectoryNotFoundException($"The directory '{target}' could not be accessed.");
        }
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Symbolic links and junctions cannot be deleted.");
        }

        var files = new List<DiskLargestFile>();
        var directories = new List<string> { target };
        var pendingDirectories = new Stack<DirectoryInfo>();
        pendingDirectories.Push(rootInfo);

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pendingDirectories.Pop();

            foreach (var file in directory.EnumerateFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((file.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    files.Add(new DiskLargestFile { Path = file.FullName, SizeBytes = file.Length });
                }
            }

            foreach (var subDirectory in directory.EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((subDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                directories.Add(subDirectory.FullName);
                pendingDirectories.Push(subDirectory);
            }
        }

        return new DiskFolderDeletionContents
        {
            Files = files,
            Directories = directories
        };
    }

    public static DiskFolderPreviewContents GetFolderPreviewContents(
        string directoryPath,
        string volumeRoot,
        int largestFileLimit,
        CancellationToken cancellationToken = default,
        IProgress<DiskScanProgress>? progress = null)
    {
        if (largestFileLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(largestFileLimit));
        }
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("A directory path is required.", nameof(directoryPath));
        }
        if (string.IsNullOrWhiteSpace(volumeRoot))
        {
            throw new ArgumentException("A volume root is required.", nameof(volumeRoot));
        }

        string root = Path.GetFullPath(volumeRoot);
        string target = Path.GetFullPath(directoryPath);
        if (!IsWithinSelectedVolume(target, root) ||
            string.Equals(
                target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The selected directory must be inside the volume and cannot be the volume root.", nameof(directoryPath));
        }

        var rootInfo = new DirectoryInfo(target);
        if (!rootInfo.Exists)
        {
            throw new DirectoryNotFoundException($"The directory '{target}' could not be accessed.");
        }
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Symbolic links and junctions cannot be reviewed.");
        }

        var largestFiles = new PriorityQueue<DiskLargestFile, long>();
        var pendingDirectories = new Stack<IEnumerator<FileSystemInfo>>();
        pendingDirectories.Push(rootInfo.EnumerateFileSystemInfos().GetEnumerator());
        long totalSize = 0;
        int fileCount = 0;
        int itemsScanned = 0;

        try
        {
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entries = pendingDirectories.Peek();
                if (!entries.MoveNext())
                {
                    entries.Dispose();
                    pendingDirectories.Pop();
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                var entry = entries.Current;
                if ((entry.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    if (entry is DirectoryInfo subDirectory)
                    {
                        pendingDirectories.Push(subDirectory.EnumerateFileSystemInfos().GetEnumerator());
                    }
                    else if (entry is FileInfo file)
                    {
                        long size = file.Length;
                        totalSize += size;
                        fileCount++;
                        AddLargestFile(largestFiles, file.FullName, size, largestFileLimit);
                    }
                }
                ReportProgressIfNeeded(progress, ++itemsScanned, entry.FullName);
            }
        }
        finally
        {
            while (pendingDirectories.Count > 0)
            {
                pendingDirectories.Pop().Dispose();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new DiskScanProgress(itemsScanned, target));
        return new DiskFolderPreviewContents
        {
            LargestFiles = largestFiles.UnorderedItems
                .Select(entry => entry.Element)
                .OrderByDescending(file => file.SizeBytes)
                .ToArray(),
            TotalSizeBytes = totalSize,
            FileCount = fileCount
        };
    }

    public static DiskDirectoryContents ListDirectoryContents(
        string directoryPath,
        string volumeRoot,
        IProgress<DiskScanProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, DiskDirectoryUsage>? directoryUsage = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("A directory path is required.", nameof(directoryPath));
        }
        if (string.IsNullOrWhiteSpace(volumeRoot))
        {
            throw new ArgumentException("A volume root is required.", nameof(volumeRoot));
        }

        string root = Path.GetFullPath(volumeRoot);
        string target = Path.GetFullPath(directoryPath);
        if (!IsWithinSelectedVolume(target, root))
        {
            throw new ArgumentException("The directory must be inside the selected volume.", nameof(directoryPath));
        }

        var rootInfo = new DirectoryInfo(target);
        if (!rootInfo.Exists)
        {
            throw new DirectoryNotFoundException($"The directory '{target}' could not be accessed.");
        }
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Symbolic links and junctions cannot be browsed.");
        }

        if (directoryUsage is not null)
        {
            return ListIndexedDirectoryContents(rootInfo, directoryUsage, progress, cancellationToken);
        }

        var directFiles = new PriorityQueue<DiskLargestFile, long>();
        var folderStats = new Dictionary<string, (long size, int count)>(StringComparer.OrdinalIgnoreCase);
        var pendingDirectories = new Stack<(DirectoryInfo Directory, string? TopFolder)>();
        pendingDirectories.Push((rootInfo, null));
        int unavailablePaths = 0;
        int itemsScanned = 0;
        int directFileCount = 0;

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, topFolder) = pendingDirectories.Pop();

            try
            {
                foreach (var file in directory.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }

                        long size = file.Length;
                        if (topFolder == null)
                        {
                            directFileCount++;
                            AddLargestFile(directFiles, file.FullName, size, VisibleFileLimit);
                        }
                        else
                        {
                            var current = folderStats.GetValueOrDefault(topFolder);
                            folderStats[topFolder] = (current.size + size, current.count + 1);
                        }

                        itemsScanned++;
                        ReportProgressIfNeeded(progress, itemsScanned, file.FullName);
                    }
                    catch (Exception ex) when (IsPathAccessException(ex))
                    {
                        unavailablePaths++;
                    }
                }
            }
            catch (Exception ex) when (IsPathAccessException(ex))
            {
                unavailablePaths++;
            }

            try
            {
                foreach (var subDirectory in directory.EnumerateDirectories())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if ((subDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }

                        string folderName = topFolder ?? subDirectory.Name;
                        folderStats.TryAdd(folderName, (0, 0));
                        pendingDirectories.Push((subDirectory, folderName));
                    }
                    catch (Exception ex) when (IsPathAccessException(ex))
                    {
                        unavailablePaths++;
                    }
                }
            }
            catch (Exception ex) when (IsPathAccessException(ex))
            {
                unavailablePaths++;
            }

            itemsScanned++;
            ReportProgressIfNeeded(progress, itemsScanned, directory.FullName);
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new DiskScanProgress(itemsScanned, target));
        return new DiskDirectoryContents
        {
            Folders = folderStats.Select(entry => new DiskUsageEntry
            {
                Name = entry.Key,
                SizeBytes = entry.Value.size,
                FileCount = entry.Value.count
            })
                .OrderByDescending(entry => entry.SizeBytes)
                .ToArray(),
            Files = GetLargestFiles(directFiles),
            TotalFileCount = directFileCount,
            UnavailablePathCount = unavailablePaths
        };
    }

    public static DiskStatisticsResult Scan(
        string rootPath,
        IProgress<DiskScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("A drive path is required.", nameof(rootPath));
        }

        string root = Path.GetFullPath(rootPath);
        string volumeRoot = Path.GetPathRoot(root)
            ?? throw new ArgumentException("The drive path must have a root.", nameof(rootPath));
        var drive = new DriveInfo(volumeRoot);
        if (!drive.IsReady)
        {
            throw new IOException($"The drive '{rootPath}' is not ready.");
        }

        var rootDirectory = new DirectoryInfo(root);
        if (!rootDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"The drive '{root}' could not be accessed.");
        }

        long totalCapacity = drive.TotalSize;
        long freeBytes = drive.AvailableFreeSpace;
        var folderStats = new Dictionary<string, (long size, int count)>(StringComparer.OrdinalIgnoreCase);
        var directoryStats = new Dictionary<string, (long size, int count)>(StringComparer.OrdinalIgnoreCase)
        {
            [root] = (0, 0)
        };
        var largestFiles = new PriorityQueue<DiskLargestFile, long>();
        var rootFiles = new PriorityQueue<DiskLargestFile, long>();
        var pendingDirectories = new Stack<ScanDirectoryFrame>();
        pendingDirectories.Push(new ScanDirectoryFrame(
            rootDirectory,
            rootDirectory.EnumerateFileSystemInfos().GetEnumerator(),
            null));

        long scannedBytes = 0;
        int fileCount = 0;
        int unavailablePaths = 0;
        int itemsScanned = 0;
        int rootFileCount = 0;

        try
        {
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frame = pendingDirectories.Peek();
                bool hasNext;
                try
                {
                    hasNext = frame.Entries.MoveNext();
                }
                catch (Exception ex) when (IsPathAccessException(ex))
                {
                    unavailablePaths++;
                    hasNext = false;
                }

                if (!hasNext)
                {
                    pendingDirectories.Pop();
                    frame.Entries.Dispose();
                    directoryStats[frame.Directory.FullName] = (frame.SizeBytes, frame.FileCount);
                    if (pendingDirectories.TryPeek(out var parentFrame))
                    {
                        parentFrame.SizeBytes += frame.SizeBytes;
                        parentFrame.FileCount += frame.FileCount;
                    }
                    itemsScanned++;
                    ReportProgressIfNeeded(progress, itemsScanned, frame.Directory.FullName);
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                var entry = frame.Entries.Current;
                try
                {
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    if (entry is DirectoryInfo subDirectory)
                    {
                        string topFolder = frame.TopFolder ?? subDirectory.Name;
                        if (topFolder.Length > 0)
                        {
                            folderStats.TryAdd(topFolder, (0, 0));
                        }
                        pendingDirectories.Push(new ScanDirectoryFrame(
                            subDirectory,
                            subDirectory.EnumerateFileSystemInfos().GetEnumerator(),
                            topFolder));
                    }
                    else if (entry is FileInfo file)
                    {
                        long size = file.Length;
                        scannedBytes += size;
                        fileCount++;
                        frame.SizeBytes += size;
                        frame.FileCount++;

                        string folderName = frame.TopFolder ?? string.Empty;
                        if (folderName.Length == 0)
                        {
                            rootFileCount++;
                            AddLargestFile(rootFiles, file.FullName, size, VisibleFileLimit);
                        }
                        var current = folderStats.GetValueOrDefault(folderName);
                        folderStats[folderName] = (current.size + size, current.count + 1);

                        var largestFile = new DiskLargestFile { Path = file.FullName, SizeBytes = size };
                        if (largestFiles.Count < LargestFileLimit)
                        {
                            largestFiles.Enqueue(largestFile, size);
                        }
                        else if (largestFiles.TryPeek(out _, out long smallestSize) && size > smallestSize)
                        {
                            largestFiles.Dequeue();
                            largestFiles.Enqueue(largestFile, size);
                        }
                    }

                    itemsScanned++;
                    ReportProgressIfNeeded(progress, itemsScanned, entry.FullName);
                }
                catch (Exception ex) when (IsPathAccessException(ex))
                {
                    unavailablePaths++;
                }
            }
        }
        finally
        {
            while (pendingDirectories.Count > 0)
            {
                pendingDirectories.Pop().Entries.Dispose();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new DiskScanProgress(itemsScanned, root));
        long usedBytes = Math.Max(0, totalCapacity - freeBytes);

        return new DiskStatisticsResult
        {
            RootPath = root,
            TotalCapacityBytes = totalCapacity,
            DriveUsedBytes = usedBytes,
            FreeBytes = freeBytes,
            ScannedBytes = scannedBytes,
            FileCount = fileCount,
            RootFileCount = rootFileCount,
            UnavailablePathCount = unavailablePaths,
            Folders = folderStats
                .Select(entry => new DiskUsageEntry
                {
                    Name = entry.Key,
                    SizeBytes = entry.Value.size,
                    FileCount = entry.Value.count
                })
                .OrderByDescending(entry => entry.SizeBytes)
                .ToArray(),
            LargestFiles = largestFiles.UnorderedItems
                .Select(entry => entry.Element)
                .OrderByDescending(file => file.SizeBytes)
                .ToArray(),
            RootFiles = GetLargestFiles(rootFiles),
            DirectoryUsage = directoryStats.ToDictionary(
                entry => entry.Key,
                entry => new DiskDirectoryUsage { SizeBytes = entry.Value.size, FileCount = entry.Value.count },
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static DiskDirectoryContents ListIndexedDirectoryContents(
        DirectoryInfo directory,
        IReadOnlyDictionary<string, DiskDirectoryUsage> directoryUsage,
        IProgress<DiskScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var files = new PriorityQueue<DiskLargestFile, long>();
        var folders = new List<DiskUsageEntry>();
        int unavailablePaths = 0;
        int itemsScanned = 0;
        int fileCount = 0;

        try
        {
            foreach (var file in directory.EnumerateFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    fileCount++;
                    AddLargestFile(files, file.FullName, file.Length, VisibleFileLimit);
                    itemsScanned++;
                    ReportProgressIfNeeded(progress, itemsScanned, file.FullName);
                }
                catch (Exception ex) when (IsPathAccessException(ex))
                {
                    unavailablePaths++;
                }
            }
        }
        catch (Exception ex) when (IsPathAccessException(ex))
        {
            unavailablePaths++;
        }

        try
        {
            foreach (var subDirectory in directory.EnumerateDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if ((subDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    directoryUsage.TryGetValue(subDirectory.FullName, out var usage);
                    folders.Add(new DiskUsageEntry
                    {
                        Name = subDirectory.Name,
                        SizeBytes = usage?.SizeBytes ?? 0,
                        FileCount = usage?.FileCount ?? 0
                    });
                    itemsScanned++;
                    ReportProgressIfNeeded(progress, itemsScanned, subDirectory.FullName);
                }
                catch (Exception ex) when (IsPathAccessException(ex))
                {
                    unavailablePaths++;
                }
            }
        }
        catch (Exception ex) when (IsPathAccessException(ex))
        {
            unavailablePaths++;
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new DiskScanProgress(itemsScanned, directory.FullName));
        return new DiskDirectoryContents
        {
            Folders = folders.OrderByDescending(folder => folder.SizeBytes).ToArray(),
            Files = GetLargestFiles(files),
            TotalFileCount = fileCount,
            UnavailablePathCount = unavailablePaths
        };
    }

    public static bool IsWithinSelectedVolume(string path, string root)
    {
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void ReportProgressIfNeeded(IProgress<DiskScanProgress>? progress, int itemsScanned, string path)
    {
        if (itemsScanned % ProgressInterval == 0)
        {
            progress?.Report(new DiskScanProgress(itemsScanned, path));
        }
    }

    private static void AddLargestFile(
        PriorityQueue<DiskLargestFile, long> largestFiles,
        string path,
        long size,
        int limit)
    {
        if (limit == 0)
        {
            return;
        }
        var file = new DiskLargestFile { Path = path, SizeBytes = size };
        if (largestFiles.Count < limit)
        {
            largestFiles.Enqueue(file, size);
        }
        else if (largestFiles.TryPeek(out _, out long smallestSize) && size > smallestSize)
        {
            largestFiles.Dequeue();
            largestFiles.Enqueue(file, size);
        }
    }

    private static IReadOnlyList<DiskLargestFile> GetLargestFiles(
        PriorityQueue<DiskLargestFile, long> largestFiles) =>
        largestFiles.UnorderedItems
            .Select(entry => entry.Element)
            .OrderByDescending(file => file.SizeBytes)
            .ToArray();

    private sealed class ScanDirectoryFrame(
        DirectoryInfo directory,
        IEnumerator<FileSystemInfo> entries,
        string? topFolder)
    {
        public DirectoryInfo Directory { get; } = directory;
        public IEnumerator<FileSystemInfo> Entries { get; } = entries;
        public string? TopFolder { get; } = topFolder;
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
    }

    private static bool IsPathAccessException(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or SecurityException;
}
