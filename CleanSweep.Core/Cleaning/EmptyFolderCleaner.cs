using System.Security;
using CleanSweep.Core.Models;

namespace CleanSweep.Core.Cleaning;

public static class EmptyFolderCleaner
{
    public static EmptyFolderScanResult FindEmptyDirectories(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"The selected folder does not exist: {root}");
        }

        var emptyDirectories = new List<string>();
        var stack = new Stack<(DirectoryInfo Directory, IEnumerator<FileSystemInfo> Entries, bool HasFiles)>();
        var rootInfo = new DirectoryInfo(root);
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArgumentException("The selected folder cannot be a symbolic link or junction.", nameof(rootPath));
        }

        stack.Push((rootInfo, rootInfo.EnumerateFileSystemInfos().GetEnumerator(), false));
        int directoriesScanned = 1;
        int accessErrors = 0;
        try
        {
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frame = stack.Pop();
                bool hasNext;
                try
                {
                    hasNext = frame.Entries.MoveNext();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
                {
                    accessErrors++;
                    frame.Entries.Dispose();
                    MarkHasFiles(stack);
                    continue;
                }

                if (hasNext)
                {
                    FileSystemInfo entry = frame.Entries.Current;
                    stack.Push((frame.Directory, frame.Entries, frame.HasFiles));
                    try
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            MarkHasFiles(stack);
                            continue;
                        }

                        if (entry is DirectoryInfo child)
                        {
                            stack.Push((child, child.EnumerateFileSystemInfos().GetEnumerator(), false));
                            directoriesScanned++;
                        }
                        else if (entry is FileInfo)
                        {
                            MarkHasFiles(stack);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
                    {
                        accessErrors++;
                        MarkHasFiles(stack);
                    }
                    continue;
                }

                frame.Entries.Dispose();
                if (!frame.HasFiles)
                {
                    emptyDirectories.Add(frame.Directory.FullName);
                }
                else
                {
                    MarkHasFiles(stack);
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

        emptyDirectories.RemoveAll(path => string.Equals(
            Path.TrimEndingDirectorySeparator(path),
            Path.TrimEndingDirectorySeparator(root),
            StringComparison.OrdinalIgnoreCase));
        emptyDirectories.Sort((left, right) => right.Length.CompareTo(left.Length));
        return new EmptyFolderScanResult(emptyDirectories, directoriesScanned, accessErrors);
    }

    public static CleanResult DeleteEmptyDirectories(
        string rootPath,
        IEnumerable<string> selectedDirectories,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(selectedDirectories);
        string root = Path.GetFullPath(rootPath);
        var rootInfo = new DirectoryInfo(root);
        if (!rootInfo.Exists)
        {
            throw new DirectoryNotFoundException($"The selected folder does not exist: {root}");
        }
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArgumentException("The selected folder cannot be a symbolic link or junction.", nameof(rootPath));
        }

        var result = new CleanResult();
        var directories = selectedDirectories
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => path.Length);

        foreach (string path in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsWithinRoot(path, root) ||
                string.Equals(
                    Path.TrimEndingDirectorySeparator(path),
                    Path.TrimEndingDirectorySeparator(root),
                    StringComparison.OrdinalIgnoreCase))
            {
                result.SkippedItems++;
                continue;
            }

            try
            {
                if (!HasSafePathComponents(path, root))
                {
                    result.SkippedItems++;
                    continue;
                }

                var directory = new DirectoryInfo(path);
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    result.SkippedItems++;
                    continue;
                }

                directory.Delete();
                result.RemovedDirectories++;
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                result.SkippedItems++;
                if (result.Errors.Count < 100)
                {
                    result.Errors.Add(new CleanError("folder_cleanup_failed", path));
                }
                else
                {
                    result.OmittedErrorsCount++;
                }
            }
        }

        return result;
    }

    private static void MarkHasFiles(
        Stack<(DirectoryInfo Directory, IEnumerator<FileSystemInfo> Entries, bool HasFiles)> stack)
    {
        if (stack.Count == 0)
        {
            return;
        }

        var parent = stack.Pop();
        stack.Push((parent.Directory, parent.Entries, true));
    }

    private static bool IsWithinRoot(string path, string root)
    {
        string relativePath = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relativePath)
            && relativePath != "."
            && !relativePath.Equals("..", StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool HasSafePathComponents(string path, string root)
    {
        string relativePath = Path.GetRelativePath(root, path);
        string currentPath = root;
        foreach (string segment in relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            var directory = new DirectoryInfo(currentPath);
            if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }
        return true;
    }
}
