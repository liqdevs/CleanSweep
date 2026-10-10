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
            var dirInfo = new DirectoryInfo(path);
            WalkDirectory(dirInfo, ref total, ref count);
        }
        catch
        {
            // Ignore access errors on the root directory
        }

        return (total, count);
    }

    private static void WalkDirectory(DirectoryInfo dir, ref long total, ref int count)
    {
        // Skip reparse points (symlinks / junctions) to prevent cycles and accidental deletion
        if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return;
        }

        FileInfo[] files;
        try
        {
            files = dir.GetFiles();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return;
        }

        foreach (var file in files)
        {
            try
            {
                if ((file.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    total += file.Length;
                    count++;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // In-use or locked file
            }
        }

        DirectoryInfo[] subDirs;
        try
        {
            subDirs = dir.GetDirectories();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return;
        }

        foreach (var subDir in subDirs)
        {
            WalkDirectory(subDir, ref total, ref count);
        }
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

