using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        "recycle_bin",
        "app_caches",
        "developer_caches"
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
            var browserTargets = new List<ScanTarget>();
            AddChromiumBrowserCaches(browserTargets, Path.Combine(localAppData, "Google", "Chrome", "User Data"));
            AddChromiumBrowserCaches(browserTargets, Path.Combine(localAppData, "Microsoft", "Edge", "User Data"));
            AddChromiumBrowserCaches(browserTargets, Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"));
            AddChromiumBrowserCaches(browserTargets, Path.Combine(localAppData, "Vivaldi", "User Data"));
            AddChromiumBrowserCaches(browserTargets, Path.Combine(localAppData, "Yandex", "YandexBrowser", "User Data"));
            AddProfileCacheDirectories(browserTargets, Path.Combine(appData, "Opera Software", "Opera Stable"), "Cache", "Code Cache", "GPUCache");
            AddProfileCacheDirectories(browserTargets, Path.Combine(appData, "Opera Software", "Opera GX Stable"), "Cache", "Code Cache", "GPUCache");

            AddProfileCaches(browserTargets, Path.Combine(appData, "Mozilla", "Firefox", "Profiles"), "cache2");
            string firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
            AddProfileCaches(browserTargets, firefoxProfiles, "cache2");
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

            // 10. Messenger and desktop app caches. Keep account data and offline media untouched.
            var appCacheTargets = new List<ScanTarget>();
            foreach (string discordInstall in new[] { "discord", "discordptb", "discordcanary" })
            {
                string discordPath = Path.Combine(appData, discordInstall);
                AddExistingDirectoryTarget(appCacheTargets, Path.Combine(discordPath, "Cache"));
                AddExistingDirectoryTarget(appCacheTargets, Path.Combine(discordPath, "Code Cache"));
                AddExistingDirectoryTarget(appCacheTargets, Path.Combine(discordPath, "GPUCache"));
            }
            AddExistingDirectoryTarget(appCacheTargets, Path.Combine(appData, "Telegram Desktop", "tdata", "temp"));
            AddExistingDirectoryTarget(appCacheTargets, Path.Combine(appData, "Telegram Desktop", "tdata", "user_data", "cache"));
            AddExistingDirectoryTarget(appCacheTargets, Path.Combine(localAppData, "Spotify", "Browser", "Cache"));
            AddExistingDirectoryTarget(appCacheTargets, Path.Combine(localAppData, "Spotify", "Browser", "Code Cache"));

            string steamInstall = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(steamInstall))
            {
                string steamRoot = Path.Combine(steamInstall, "Steam");
                AddExistingDirectoryTarget(appCacheTargets, Path.Combine(steamRoot, "steamapps", "shadercache"));
                AddExistingDirectoryTarget(appCacheTargets, Path.Combine(steamRoot, "appcache", "httpcache"));
                AddExistingDirectoryTarget(appCacheTargets, Path.Combine(steamRoot, "config", "htmlcache"));
            }

            AddExistingDirectoryTarget(appCacheTargets, Path.Combine(localAppData, "Steam", "htmlcache"));
            categories.Add(new Category("app_caches", appCacheTargets, SelectedFor("app_caches")));

            // 11. Development tool caches. NuGet packages are included only as an optional,
            // unchecked category because removing them can require large downloads later.
            var developerCacheTargets = new List<ScanTarget>();
            AddExistingDirectoryTarget(developerCacheTargets, Path.Combine(userProfile, ".nuget", "packages"));
            AddExistingDirectoryTarget(developerCacheTargets, Path.Combine(userProfile, ".npm", "_cacache"));
            AddExistingDirectoryTarget(developerCacheTargets, Path.Combine(userProfile, ".gradle", "caches"));
            AddExistingDirectoryTarget(developerCacheTargets, Path.Combine(localAppData, "pip", "Cache"));
            categories.Add(new Category("developer_caches", developerCacheTargets, SelectedFor("developer_caches")));
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

    private static void AddChromiumBrowserCaches(List<ScanTarget> targets, string userDataPath)
    {
        AddProfileCaches(targets, userDataPath, "Cache", "Code Cache", "GPUCache");
    }

    private static void AddProfileCaches(List<ScanTarget> targets, string profilesPath, params string[] cacheDirectories)
    {
        if (!Directory.Exists(profilesPath))
        {
            return;
        }

        IEnumerable<string> profiles;
        try
        {
            profiles = Directory.EnumerateDirectories(profilesPath).ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (string profile in profiles)
        {
            AddProfileCacheDirectories(targets, profile, cacheDirectories);
        }
    }

    private static void AddProfileCacheDirectories(List<ScanTarget> targets, string profilePath, params string[] cacheDirectories)
    {
        foreach (string cacheDirectory in cacheDirectories)
        {
            AddExistingDirectoryTarget(targets, Path.Combine(profilePath, cacheDirectory));
        }
    }

    private static void AddExistingDirectoryTarget(List<ScanTarget> targets, string path)
    {
        if (Directory.Exists(path))
        {
            targets.Add(new ScanTarget(path, TargetKind.DirContents));
        }
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
