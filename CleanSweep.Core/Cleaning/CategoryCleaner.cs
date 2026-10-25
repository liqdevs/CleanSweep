using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using CleanSweep.Core.Models;
using CleanSweep.Core.Utils;

namespace CleanSweep.Core.Cleaning;

public static class CategoryCleaner
{
    private const int MaximumPreviewItems = 100;
    private const int MaximumStoredErrors = 100;

    public static bool ShouldSkipForCleanup(string path, int recentSeconds = 60)
    {
        try
        {
            DateTime lastWriteTime = File.GetLastWriteTimeUtc(path);
            TimeSpan age = DateTime.UtcNow - lastWriteTime;
            return age.TotalSeconds >= 0 && age.TotalSeconds < recentSeconds;
        }
        catch
        {
            // If we can't inspect the file, play it safe and skip
            return true;
        }
    }

    public static string CategoryRiskLevel(string categoryKey)
    {
        return categoryKey switch
        {
            "memory_dumps" or "crash_reports" => "red",
            "recycle_bin" or "recent_items" or "incomplete_downloads" or "app_caches" or "developer_caches" => "yellow",
            _ => "green"
        };
    }

    public static void CollectPreviewItems(
        string path,
        List<PreviewItem> previewItems,
        string categoryKey,
        int depth = 0,
        int maximumItems = MaximumPreviewItems)
    {
        if (depth > 2 || previewItems.Count >= maximumItems || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            var dirInfo = new DirectoryInfo(path);
            int count = 0;

            foreach (var file in dirInfo.EnumerateFiles())
            {
                if (count++ >= 12 || previewItems.Count >= maximumItems) break;
                try
                {
                    var assessment = FileImportanceAnalyzer.Analyze(file.FullName);
                    previewItems.Add(new PreviewItem(
                        file.FullName,
                        file.Length,
                        assessment.Risk,
                        ShouldSkipForCleanup(file.FullName),
                        assessment.DescriptionKey
                    ));
                }
                catch
                {
                    // Ignore single file error
                }
            }

            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                if (count++ >= 12 || previewItems.Count >= maximumItems) break;
                try
                {
                    CollectPreviewItems(subDir.FullName, previewItems, categoryKey, depth + 1, maximumItems);
                }
                catch
                {
                    // Ignore directory error
                }
            }
        }
        catch
        {
            // Ignore access errors
        }
    }

    public static List<PreviewItem> BuildPreviewItems(Category category, int maximumItems = MaximumPreviewItems)
    {
        var previewItems = new List<PreviewItem>();
        if (maximumItems <= 0)
        {
            return previewItems;
        }

        foreach (var target in category.Targets)
        {
            if (previewItems.Count >= maximumItems)
            {
                break;
            }
            if (target.Kind == TargetKind.DirContents)
            {
                CollectPreviewItems(target.Path, previewItems, category.Key, maximumItems: maximumItems);
            }
            else if (target.Kind == TargetKind.FileGlob && Directory.Exists(target.Path))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(target.Path);
                    int count = 0;
                    foreach (var file in dirInfo.EnumerateFiles(target.Pattern))
                    {
                        if (count++ >= 12 || previewItems.Count >= maximumItems) break;
                        try
                        {
                            var assessment = FileImportanceAnalyzer.Analyze(file.FullName);
                            previewItems.Add(new PreviewItem(
                                file.FullName,
                                file.Length,
                                assessment.Risk,
                                ShouldSkipForCleanup(file.FullName),
                                assessment.DescriptionKey
                            ));
                        }
                        catch
                        {
                            // Ignore file error
                        }
                    }
                }
                catch
                {
                    // Ignore directory error
                }
            }
        }

        return previewItems;
    }

    private static void ClearDirContents(string path, CleanResult result)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var stack = new Stack<(DirectoryInfo Directory, IEnumerator<FileSystemInfo> Entries)>();
        try
        {
            var root = new DirectoryInfo(path);
            if ((root.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                result.SkippedItems++;
                return;
            }

            stack.Push((root, root.EnumerateFileSystemInfos().GetEnumerator()));
            while (stack.Count > 0)
            {
                var frame = stack.Peek();
                bool hasNext;
                try
                {
                    hasNext = frame.Entries.MoveNext();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
                {
                    AddCleanupError(result, new CleanError("access_denied_named", Path.GetFileName(frame.Directory.FullName)));
                    hasNext = false;
                }

                if (!hasNext)
                {
                    stack.Pop();
                    frame.Entries.Dispose();
                    if (!ReferenceEquals(frame.Directory, root))
                    {
                        try
                        {
                            frame.Directory.Delete();
                            result.RemovedDirectories++;
                        }
                        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException or ArgumentException or NotSupportedException)
                        {
                            AddCleanupError(result, new CleanError("folder_cleanup_failed", frame.Directory.FullName));
                        }
                    }
                    continue;
                }

                FileSystemInfo entry = frame.Entries.Current;
                try
                {
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        result.SkippedItems++;
                    }
                    else if (entry is DirectoryInfo childDirectory)
                    {
                        stack.Push((childDirectory, childDirectory.EnumerateFileSystemInfos().GetEnumerator()));
                    }
                    else if (entry is FileInfo file)
                    {
                        if (ShouldSkipForCleanup(file.FullName))
                        {
                            result.SkippedItems++;
                            continue;
                        }

                        long size = file.Length;
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                        result.FreedBytes += size;
                        result.RemovedItems++;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException or ArgumentException or NotSupportedException)
                {
                    AddCleanupError(result, new CleanError("file_cleanup_failed", entry.FullName));
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException or ArgumentException or NotSupportedException)
        {
            AddCleanupError(result, new CleanError("access_denied_named", Path.GetFileName(path)));
        }
        finally
        {
            while (stack.Count > 0)
            {
                stack.Pop().Entries.Dispose();
            }
        }
    }

    private static void ClearFileGlob(string path, string pattern, CleanResult result)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            var dir = new DirectoryInfo(path);
            foreach (var file in dir.EnumerateFiles(pattern))
            {
                try
                {
                    if (ShouldSkipForCleanup(file.FullName))
                    {
                        result.SkippedItems++;
                        continue;
                    }

                    long size = file.Length;
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                    result.FreedBytes += size;
                    result.RemovedItems++;
                }
                catch
                {
                    result.SkippedItems++;
                }
            }
        }
        catch
        {
            // Ignore
        }
    }

    private static void ClearRecycleBin(string path, CleanResult result)
    {
        var (freedBefore, countBefore) = ShellUtils.RecycleBinSize(path);
        if (ShellUtils.EmptyRecycleBin(path))
        {
            result.FreedBytes += freedBefore;
            result.RemovedItems += countBefore;
        }
        else
        {
            AddCleanupError(result, new CleanError("recycle_bin_failed"));
        }
    }

    public static CleanResult CleanCategory(Category category)
    {
        var result = new CleanResult();

        foreach (var target in category.Targets)
        {
            if (target.Kind == TargetKind.DirContents)
            {
                ClearDirContents(target.Path, result);
            }
            else if (target.Kind == TargetKind.FileGlob)
            {
                ClearFileGlob(target.Path, target.Pattern, result);
            }
            else if (target.Kind == TargetKind.RecycleBin)
            {
                ClearRecycleBin(target.Path, result);
            }
        }

        return result;
    }

    public static CleanResult CleanFiles(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default,
        bool allowRecentFiles = false)
    {
        return CleanFilesCore(filePaths, null, cancellationToken, allowRecentFiles);
    }

    public static CleanResult CleanFilesWithinRoot(
        IEnumerable<string> filePaths,
        string allowedRoot,
        CancellationToken cancellationToken = default,
        bool allowRecentFiles = false)
    {
        if (string.IsNullOrWhiteSpace(allowedRoot))
        {
            throw new ArgumentException("A root path is required.", nameof(allowedRoot));
        }

        return CleanFilesCore(filePaths, Path.GetFullPath(allowedRoot), cancellationToken, allowRecentFiles);
    }

    public static CleanResult CleanDirectoryWithinRoot(
        string directoryPath,
        string allowedRoot,
        CancellationToken cancellationToken = default,
        IProgress<CleanProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedRoot);

        string root = Path.GetFullPath(allowedRoot);
        string target = Path.GetFullPath(directoryPath);
        if (!IsSafeDirectoryPathWithinRoot(target, root) ||
            string.Equals(
                target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The directory must be inside the allowed root and cannot be the root itself.", nameof(directoryPath));
        }

        var result = new CleanResult();
        var stack = new Stack<(DirectoryInfo Directory, IEnumerator<FileSystemInfo> Entries)>();
        var targetInfo = new DirectoryInfo(target);
        stack.Push((targetInfo, targetInfo.EnumerateFileSystemInfos().GetEnumerator()));
        int itemsProcessed = 0;

        try
        {
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frame = stack.Peek();
                bool hasNext;
                try
                {
                    hasNext = frame.Entries.MoveNext();
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
                {
                    AddCleanupError(result, new CleanError("folder_cleanup_failed", frame.Directory.FullName));
                    hasNext = false;
                }

                if (!hasNext)
                {
                    stack.Pop();
                    frame.Entries.Dispose();
                    try
                    {
                        frame.Directory.Delete();
                        result.RemovedDirectories++;
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException or ArgumentException or NotSupportedException)
                    {
                        AddCleanupError(result, new CleanError("folder_cleanup_failed", frame.Directory.FullName));
                    }
                    continue;
                }

                FileSystemInfo entry = frame.Entries.Current;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        result.SkippedItems++;
                    }
                    else if (entry is DirectoryInfo childDirectory)
                    {
                        if (IsSafeDirectoryPathWithinRoot(childDirectory.FullName, root))
                        {
                            stack.Push((childDirectory, childDirectory.EnumerateFileSystemInfos().GetEnumerator()));
                        }
                        else
                        {
                            result.SkippedItems++;
                        }
                    }
                    else if (entry is FileInfo file)
                    {
                        if (!IsSafeFilePathWithinRoot(file.FullName, root))
                        {
                            result.SkippedItems++;
                            continue;
                        }

                        long size = file.Length;
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                        result.FreedBytes += size;
                        result.RemovedItems++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException or ArgumentException or NotSupportedException)
                {
                    AddCleanupError(result, new CleanError("file_cleanup_failed", entry.FullName));
                }
                finally
                {
                    itemsProcessed++;
                    if (itemsProcessed == 1 || itemsProcessed % 128 == 0)
                    {
                        progress?.Report(new CleanProgress(itemsProcessed, entry.FullName));
                    }
                }
            }
        }
        finally
        {
            while (stack.Count > 0)
            {
                stack.Pop().Entries.Dispose();
            }
        }

        progress?.Report(new CleanProgress(itemsProcessed, string.Empty));
        return result;
    }

    private static CleanResult CleanFilesCore(
        IEnumerable<string> filePaths,
        string? allowedRoot,
        CancellationToken cancellationToken,
        bool allowRecentFiles = false)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        var result = new CleanResult();

        foreach (string path in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path))
            {
                AddCleanupError(result, new CleanError("file_cleanup_failed", path ?? string.Empty));
                continue;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (allowedRoot is not null && !IsSafeFilePathWithinRoot(fullPath, allowedRoot))
                {
                    result.SkippedItems++;
                    continue;
                }

                var file = new FileInfo(fullPath);
                if (!file.Exists)
                {
                    result.SkippedItems++;
                    continue;
                }

                if ((file.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    (!allowRecentFiles && ShouldSkipForCleanup(path)))
                {
                    result.SkippedItems++;
                    continue;
                }

                long size = file.Length;
                file.Attributes = FileAttributes.Normal;
                file.Delete();
                result.FreedBytes += size;
                result.RemovedItems++;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException or ArgumentException or NotSupportedException)
            {
                AddCleanupError(result, new CleanError("file_cleanup_failed", Path.GetFileName(path)));
            }
        }

        return result;
    }

    private static bool IsSafeFilePathWithinRoot(string filePath, string rootPath)
    {
        string normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedFile = filePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(normalizedFile, normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !normalizedFile.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? directoryPath = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directoryPath))
        {
            string normalizedDirectory = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(normalizedDirectory, normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
                !normalizedDirectory.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if ((new DirectoryInfo(directoryPath).Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
            if (string.Equals(normalizedDirectory, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            directoryPath = Path.GetDirectoryName(directoryPath);
        }

        return false;
    }

    private static bool IsSafeDirectoryPathWithinRoot(string directoryPath, string rootPath)
    {
        string normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedDirectory = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(normalizedDirectory, normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !normalizedDirectory.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? current = normalizedDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if ((new DirectoryInfo(current).Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
            if (string.Equals(
                    current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    private static void AddCleanupError(CleanResult result, CleanError error)
    {
        if (result.Errors.Count < MaximumStoredErrors)
        {
            result.Errors.Add(error);
        }
        else
        {
            result.OmittedErrorsCount++;
        }
    }
}
