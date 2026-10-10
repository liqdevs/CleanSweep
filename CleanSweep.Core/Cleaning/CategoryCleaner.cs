using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using CleanSweep.Core.Models;
using CleanSweep.Core.Utils;

namespace CleanSweep.Core.Cleaning;

public static class CategoryCleaner
{
    public static bool ShouldSkipForCleanup(string path, int recentSeconds = 60)
    {
        try
        {
            DateTime lastWriteTime = File.GetLastWriteTime(path);
            TimeSpan age = DateTime.Now - lastWriteTime;
            return age.TotalSeconds < recentSeconds;
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
            "recycle_bin" or "recent_items" or "incomplete_downloads" => "yellow",
            _ => "green"
        };
    }

    public static void CollectPreviewItems(string path, List<PreviewItem> previewItems, string categoryKey, int depth = 0)
    {
        if (depth > 2 || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            var dirInfo = new DirectoryInfo(path);
            int count = 0;

            foreach (var file in dirInfo.EnumerateFiles())
            {
                if (count++ >= 12) break;
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
                if (count++ >= 12) break;
                try
                {
                    var (subSize, _) = FileSystemUtils.DirSize(subDir.FullName);
                    var assessment = FileImportanceAnalyzer.Analyze(subDir.FullName);
                    previewItems.Add(new PreviewItem(
                        subDir.FullName,
                        subSize,
                        assessment.Risk,
                        ShouldSkipForCleanup(subDir.FullName),
                        assessment.DescriptionKey
                    ));
                    CollectPreviewItems(subDir.FullName, previewItems, categoryKey, depth + 1);
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

    public static List<PreviewItem> BuildPreviewItems(Category category)
    {
        var previewItems = new List<PreviewItem>();
        foreach (var target in category.Targets)
        {
            if (target.Kind == TargetKind.DirContents)
            {
                CollectPreviewItems(target.Path, previewItems, category.Key);
            }
            else if (target.Kind == TargetKind.FileGlob && Directory.Exists(target.Path))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(target.Path);
                    int count = 0;
                    foreach (var file in dirInfo.EnumerateFiles(target.Pattern))
                    {
                        if (count++ >= 12) break;
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

        DirectoryInfo dir;
        try
        {
            dir = new DirectoryInfo(path);
        }
        catch
        {
            result.Errors.Add(new CleanError("access_denied_named", Path.GetFileName(path)));
            return;
        }

        // Delete files
        try
        {
            foreach (var file in dir.EnumerateFiles())
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
            result.Errors.Add(new CleanError("access_denied_named", Path.GetFileName(path)));
        }

        // Delete directories
        try
        {
            foreach (var subDir in dir.EnumerateDirectories())
            {
                try
                {
                    if (ShouldSkipForCleanup(subDir.FullName))
                    {
                        result.SkippedItems++;
                        continue;
                    }

                    var (size, _) = FileSystemUtils.DirSize(subDir.FullName);
                    subDir.Delete(true);
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
            // Ignore access errors on enumerating directories
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
            result.Errors.Add(new CleanError("recycle_bin_failed"));
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

    public static CleanResult CleanFiles(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        return CleanFilesCore(filePaths, null, cancellationToken);
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
                result.Errors.Add(new CleanError("file_cleanup_failed", path ?? string.Empty));
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
                result.Errors.Add(new CleanError("file_cleanup_failed", Path.GetFileName(path)));
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
}
