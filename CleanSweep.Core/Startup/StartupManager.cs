using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using CleanSweep.Core.Models;
using Microsoft.Win32;

namespace CleanSweep.Core.Startup;

public static class StartupManager
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string Wow6432RunKeyPath = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run";
    public const string BackupKeyPath = @"Software\CleanSweep\DisabledStartup";
    public const string DisabledSubfolder = "Disabled (CleanSweep)";

    private static readonly HashSet<string> StartupFileSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lnk", ".url", ".exe", ".bat", ".cmd", ".vbs"
    };

    public static string UserStartupDir()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "Startup");
    }

    public static string CommonStartupDir()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "Microsoft", "Windows", "Start Menu", "Programs", "Startup");
    }

    public static Dictionary<string, string> ReadRunKey(RegistryKey hive, string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = hive.OpenSubKey(path, false);
            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    var val = key.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result[valueName] = val;
                    }
                }
            }
        }
        catch
        {
            // Ignore access or non-existent keys
        }
        return result;
    }

    public static List<StartupItem> ListStartupFolder(string folderPath, string source, bool needsAdmin)
    {
        var items = new List<StartupItem>();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return items;
        }

        try
        {
            var dir = new DirectoryInfo(folderPath);
            foreach (var file in dir.EnumerateFiles())
            {
                if (StartupFileSuffixes.Contains(file.Extension))
                {
                    string stem = Path.GetFileNameWithoutExtension(file.Name);
                    items.Add(new StartupItem
                    {
                        Id = $"{source}:{stem}",
                        Name = stem,
                        Command = file.FullName,
                        Source = source,
                        Enabled = true,
                        NeedsAdmin = needsAdmin
                    });
                }
            }
        }
        catch
        {
            // Ignore access errors
        }

        string disabledFolder = Path.Combine(folderPath, DisabledSubfolder);
        if (Directory.Exists(disabledFolder))
        {
            try
            {
                var dir = new DirectoryInfo(disabledFolder);
                foreach (var file in dir.EnumerateFiles())
                {
                    if (StartupFileSuffixes.Contains(file.Extension))
                    {
                        string stem = Path.GetFileNameWithoutExtension(file.Name);
                        items.Add(new StartupItem
                        {
                            Id = $"{source}:{stem}",
                            Name = stem,
                            Command = file.FullName,
                            Source = source,
                            Enabled = false,
                            NeedsAdmin = needsAdmin
                        });
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        return items;
    }

    public static List<StartupItem> ListStartupItems()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new List<StartupItem>();
        }

        var items = new List<StartupItem>();

        // HKCU Run
        var enabledHkcu = ReadRunKey(Registry.CurrentUser, RunKeyPath);
        var disabledHkcu = ReadRunKey(Registry.CurrentUser, $@"{BackupKeyPath}\HKCU_Run");

        foreach (var (name, command) in enabledHkcu)
        {
            items.Add(new StartupItem
            {
                Id = $"hkcu_run:{name}",
                Name = name,
                Command = command,
                Source = "hkcu_run",
                Enabled = true,
                NeedsAdmin = false
            });
        }

        foreach (var (name, command) in disabledHkcu)
        {
            items.Add(new StartupItem
            {
                Id = $"hkcu_run:{name}",
                Name = name,
                Command = command,
                Source = "hkcu_run",
                Enabled = false,
                NeedsAdmin = false
            });
        }

        // HKLM Run (64-bit and 32-bit Wow64)
        foreach (var path in new[] { RunKeyPath, Wow6432RunKeyPath })
        {
            var hklm = ReadRunKey(Registry.LocalMachine, path);
            foreach (var (name, command) in hklm)
            {
                items.Add(new StartupItem
                {
                    Id = $"hklm_run:{path}:{name}",
                    Name = name,
                    Command = command,
                    Source = "hklm_run",
                    Enabled = true,
                    NeedsAdmin = true
                });
            }
        }

        // Startup Folders
        items.AddRange(ListStartupFolder(UserStartupDir(), "user_startup", needsAdmin: false));
        items.AddRange(ListStartupFolder(CommonStartupDir(), "common_startup", needsAdmin: true));

        items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return items;
    }

    public static bool ToggleRegistryEntry(string name, bool enabled)
    {
        try
        {
            string backupSubkey = $@"{BackupKeyPath}\HKCU_Run";
            if (enabled)
            {
                using var backupKey = Registry.CurrentUser.OpenSubKey(backupSubkey, true);
                var command = backupKey?.GetValue(name)?.ToString();
                if (string.IsNullOrEmpty(command))
                {
                    return false;
                }

                using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
                if (runKey == null) return false;
                runKey.SetValue(name, command, RegistryValueKind.String);
                backupKey?.DeleteValue(name, false);
            }
            else
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                var command = runKey?.GetValue(name)?.ToString();
                if (string.IsNullOrEmpty(command))
                {
                    return false;
                }

                using var backupKey = Registry.CurrentUser.CreateSubKey(backupSubkey, true);
                if (backupKey == null) return false;
                backupKey.SetValue(name, command, RegistryValueKind.String);
                runKey?.DeleteValue(name, false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool ToggleStartupFile(string folderPath, string stemName, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(stemName))
        {
            return false;
        }

        string disabledFolder = Path.Combine(folderPath, DisabledSubfolder);
        try
        {
            string sourceFolder = enabled ? disabledFolder : folderPath;
            string targetFolder = enabled ? folderPath : disabledFolder;

            if (!Directory.Exists(sourceFolder))
            {
                return false;
            }

            Directory.CreateDirectory(targetFolder);

            var dir = new DirectoryInfo(sourceFolder);
            foreach (var file in dir.EnumerateFiles())
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(file.Name), stemName, StringComparison.OrdinalIgnoreCase))
                {
                    string destPath = Path.Combine(targetFolder, file.Name);
                    if (string.Equals(file.FullName, destPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }

                    file.MoveTo(destPath, true);
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetStartupEnabled(StartupItem item, bool enabled)
    {
        if (item.NeedsAdmin || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        if (item.Source == "hkcu_run")
        {
            return ToggleRegistryEntry(item.Name, enabled);
        }

        if (item.Source == "user_startup")
        {
            return ToggleStartupFile(UserStartupDir(), item.Name, enabled);
        }

        return false;
    }
}
