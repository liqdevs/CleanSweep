using System;
using System.IO;

namespace CleanSweep.Core.Utils;

public static class FileSystemUtils
{
    public static string HumanSize(double numBytes)
    {
        double value = numBytes;
        string[] units = { "B", "KB", "MB", "GB" };
        foreach (var unit in units)
        {
            if (value < 1024)
            {
                return unit == "B" ? $"{value:F0} {unit}" : $"{value:F1} {unit}";
            }
            value /= 1024.0;
        }
        return $"{value:F1} TB";
    }

    public static (long totalSize, int fileCount) DirSize(string path)
    {
        long total = 0;
        int count = 0;

        if (!Directory.Exists(path))
        {
            return (0, 0);
        }

        try
        {
            var pendingDirectories = new Stack<IEnumerator<FileSystemInfo>>();
            pendingDirectories.Push(new DirectoryInfo(path).EnumerateFileSystemInfos().GetEnumerator());
            try
            {
                while (pendingDirectories.Count > 0)
                {
                    var entries = pendingDirectories.Peek();
                    bool hasNext;
                    try
                    {
                        hasNext = entries.MoveNext();
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        entries.Dispose();
                        pendingDirectories.Pop();
                        continue;
                    }
                    if (!hasNext)
                    {
                        entries.Dispose();
                        pendingDirectories.Pop();
                        continue;
                    }

                    var entry = entries.Current;
                    try
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                        if (entry is DirectoryInfo subDirectory)
                        {
                            pendingDirectories.Push(subDirectory.EnumerateFileSystemInfos().GetEnumerator());
                        }
                        else if (entry is FileInfo file)
                        {
                            total += file.Length;
                            count++;
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                    }
                }
            }
            finally
            {
                while (pendingDirectories.Count > 0)
                {
                    pendingDirectories.Pop().Dispose();
                }
            }
        }
        catch
        {
            // Ignore access errors on the root directory
        }

        return (total, count);
    }

    public static List<string> ListAvailableDrives()
    {
        var drives = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    drives.Add(drive.RootDirectory.FullName);
                }
            }
        }
        catch
        {
            // fallback
        }

        if (drives.Count == 0)
        {
            drives.Add(SystemDrive());
        }

        return drives;
    }

    public static string SystemDrive()
    {
        string? root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrEmpty(root))
        {
            string sysDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            root = sysDrive.EndsWith('\\') ? sysDrive : sysDrive + "\\";
        }
        return root;
    }

    public static (long total, long used, long free) DiskUsage(string? drive = null)
    {
        string target = drive ?? SystemDrive();
        try
        {
            var info = new DriveInfo(target);
            if (info.IsReady)
            {
                long total = info.TotalSize;
                long free = info.AvailableFreeSpace;
                long used = total - free;
                return (total, used, free);
            }
        }
        catch
        {
            // Ignore error
        }
        return (0, 0, 0);
    }
}
