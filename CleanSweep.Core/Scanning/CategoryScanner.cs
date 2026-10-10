using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using CleanSweep.Core.Models;
using CleanSweep.Core.Utils;

namespace CleanSweep.Core.Scanning;

public static class CategoryScanner
{
    private static readonly HashSet<string> CautiousByDefault = new(StringComparer.OrdinalIgnoreCase)
    {
        "memory_dumps",
        "recent_items",
        "incomplete_downloads",
        "recycle_bin"
    };

    public static List<Category> BuildCategories(string? drive = null)
    {
        var categories = new List<Category>();

        bool SelectedFor(string key) => !CautiousByDefault.Contains(key);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            drive = string.IsNullOrWhiteSpace(drive) ? FileSystemUtils.SystemDrive() : drive.Trim();
            if (string.IsNullOrWhiteSpace(drive))
            {
                drive = FileSystemUtils.SystemDrive();
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(systemRoot))
            {
                systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";
            }

            string userTemp = Path.GetTempPath();
            string windowsTemp = Path.Combine(systemRoot, "Temp");
            string downloads = Path.Combine(userProfile, "Downloads");

            // 1. Temporary files
            categories.Add(new Category("temp", new[]
            {
                new ScanTarget(userTemp, TargetKind.DirContents),
                new ScanTarget(windowsTemp, TargetKind.DirContents)
            }, SelectedFor("temp")));

            // 2. Browser cache
            var browserTargets = new List<ScanTarget>
            {
                new(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache"), TargetKind.DirContents),
                new(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache"), TargetKind.DirContents)
            };

            string firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefoxProfiles))
            {
                try
                {
                    foreach (var profile in Directory.EnumerateDirectories(firefoxProfiles))
                    {
                        string cacheDir = Path.Combine(profile, "cache2");
                        if (Directory.Exists(cacheDir))
                        {
                            browserTargets.Add(new ScanTarget(cacheDir, TargetKind.DirContents));
                        }
                    }
                }
                catch
                {
                    // Ignore profile read errors
                }
            }
            categories.Add(new Category("browser_cache", browserTargets, SelectedFor("browser_cache")));

            // 3. Thumbnails
            categories.Add(new Category("thumbnails", new[]
            {
                new ScanTarget(Path.Combine(localAppData, "Microsoft", "Windows", "Explorer"), TargetKind.FileGlob, "thumbcache_*.db")
            }, SelectedFor("thumbnails")));

            // 4. Crash reports
            categories.Add(new Category("crash_reports", new[]
            {
                new ScanTarget(Path.Combine(localAppData, "CrashDumps"), TargetKind.DirContents),
                new ScanTarget(Path.Combine(programData, "Microsoft", "Windows", "WER"), TargetKind.DirContents)
            }, SelectedFor("crash_reports")));

            // 5. Memory dumps
            categories.Add(new Category("memory_dumps", new[]
            {
                new ScanTarget(Path.Combine(systemRoot, "Minidump"), TargetKind.DirContents),
                new ScanTarget(Path.Combine(systemRoot, "LiveKernelReports"), TargetKind.DirContents),
                new ScanTarget(systemRoot, TargetKind.FileGlob, "MEMORY.DMP")
            }, SelectedFor("memory_dumps")));

            // 6. Recent items
            categories.Add(new Category("recent_items", new[]
            {
                new ScanTarget(Path.Combine(appData, "Microsoft", "Windows", "Recent"), TargetKind.DirContents)
            }, SelectedFor("recent_items")));

            // 7. Incomplete downloads
            categories.Add(new Category("incomplete_downloads", new[]
            {
                new ScanTarget(downloads, TargetKind.FileGlob, "*.crdownload"),
                new ScanTarget(downloads, TargetKind.FileGlob, "*.part"),
                new ScanTarget(downloads, TargetKind.FileGlob, "*.download")
            }, SelectedFor("incomplete_downloads")));

            // 8. Shader cache
            categories.Add(new Category("shader_cache", new[]
            {
                new ScanTarget(Path.Combine(localAppData, "D3DSCache"), TargetKind.DirContents),
                new ScanTarget(Path.Combine(localAppData, "NVIDIA", "DXCache"), TargetKind.DirContents),
                new ScanTarget(Path.Combine(localAppData, "AMD", "DxCache"), TargetKind.DirContents)
            }, SelectedFor("shader_cache")));

            // 9. Recycle bin
            categories.Add(new Category("recycle_bin", new[]
            {
                new ScanTarget(drive, TargetKind.RecycleBin)
            }, SelectedFor("recycle_bin")));
        }
        else
        {
            // Non-Windows fallback
            categories.Add(new Category("temp", new[]
            {
                new ScanTarget(Path.GetTempPath(), TargetKind.DirContents)
            }));
            string cacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
            categories.Add(new Category("cache", new[]
            {
                new ScanTarget(cacheHome, TargetKind.DirContents)
            }));
        }

        return categories;
    }

    public static void ScanCategory(Category category)
    {
        long totalSize = 0;
        int totalCount = 0;
        bool foundAnything = false;
        category.Error = null;

        foreach (var target in category.Targets)
        {
            try
            {
                if (target.Kind == TargetKind.DirContents)
                {
                    if (Directory.Exists(target.Path))
                    {
                        var (size, count) = FileSystemUtils.DirSize(target.Path);
                        totalSize += size;
                        totalCount += count;
                        if (count > 0)
                        {
                            foundAnything = true;
                        }
                    }
                }
                else if (target.Kind == TargetKind.FileGlob)
                {
                    if (Directory.Exists(target.Path))
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(target.Path);
                            foreach (var file in dirInfo.EnumerateFiles(target.Pattern))
                            {
                                try
                                {
                                    totalSize += file.Length;
                                    totalCount++;
                                    foundAnything = true;
                                }
                                catch
                                {
                                    // Ignore single file error
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            category.Error = "access_denied";
                        }
                        catch
                        {
                            // Ignore directory iteration error
                        }
                    }
                }
                else if (target.Kind == TargetKind.RecycleBin)
                {
                    var (size, count) = ShellUtils.RecycleBinSize(target.Path);
                    totalSize += size;
                    totalCount += count;
                    if (count > 0)
                    {
                        foundAnything = true;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                category.Error = "access_denied";
            }
            catch (Exception ex)
            {
                category.Error = ex.Message;
            }
        }

        category.SizeBytes = totalSize;
        category.ItemCount = totalCount;
        category.Exists = foundAnything;
    }
}

