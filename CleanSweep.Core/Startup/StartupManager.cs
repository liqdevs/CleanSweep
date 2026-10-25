using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
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

    public static Dictionary<string, (string command, RegistryValueKind kind)> ReadRunKey(RegistryKey hive, string path)
    {
        var result = new Dictionary<string, (string command, RegistryValueKind kind)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = hive.OpenSubKey(path, false);
            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    var val = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result[valueName] = (val, key.GetValueKind(valueName));
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

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
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
        bool isAdministrator = IsAdministrator();

        // HKCU Run
        var enabledHkcu = ReadRunKey(Registry.CurrentUser, RunKeyPath);
        var disabledHkcu = ReadRunKey(Registry.CurrentUser, $@"{BackupKeyPath}\HKCU_Run");

        foreach (var (name, value) in enabledHkcu)
        {
            items.Add(new StartupItem
            {
                Id = $"hkcu_run:{name}",
                Name = name,
                Command = value.command,
                Source = "hkcu_run",
                Enabled = true,
                NeedsAdmin = false,
                RegistryPath = RunKeyPath
            });
        }

        foreach (var (name, value) in disabledHkcu)
        {
            items.Add(new StartupItem
            {
                Id = $"hkcu_run:{name}",
                Name = name,
                Command = value.command,
                Source = "hkcu_run",
                Enabled = false,
                NeedsAdmin = false,
                RegistryPath = RunKeyPath
            });
        }

        // Read each machine registry view explicitly to avoid WOW64 path redirection.
        IEnumerable<RegistryView> machineViews = Environment.Is64BitOperatingSystem
            ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
            : new[] { RegistryView.Registry32 };
        foreach (var view in machineViews)
        {
            using var machineHive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            var hklm = ReadRunKey(machineHive, RunKeyPath);
            foreach (var (name, value) in hklm)
            {
                items.Add(new StartupItem
                {
                    Id = $"hklm_run:{view}:{name}",
                    Name = name,
                    Command = value.command,
                    Source = "hklm_run",
                    Enabled = true,
                    NeedsAdmin = !isAdministrator,
                    RegistryPath = RunKeyPath,
                    RegistryView = view
                });
            }

            var disabledHklm = ReadRunKey(machineHive, $@"{BackupKeyPath}\HKLM_Run\{view}");
            foreach (var (name, value) in disabledHklm)
            {
                items.Add(new StartupItem
                {
                    Id = $"hklm_run:{view}:{name}",
                    Name = name,
                    Command = value.command,
                    Source = "hklm_run",
                    Enabled = false,
                    NeedsAdmin = !isAdministrator,
                    RegistryPath = RunKeyPath,
                    RegistryView = view
                });
            }
        }

        // Startup Folders
        items.AddRange(ListStartupFolder(UserStartupDir(), "user_startup", needsAdmin: false));
        items.AddRange(ListStartupFolder(CommonStartupDir(), "common_startup", needsAdmin: !isAdministrator));

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
                if (backupKey is null)
                {
                    return false;
                }

                var command = backupKey.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
                if (string.IsNullOrEmpty(command)) return false;

                using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
                if (runKey == null) return false;
                RegistryValueKind storedKind = backupKey.GetValueKind(name);
                runKey.SetValue(name, command, storedKind);
                backupKey?.DeleteValue(name, false);
            }
            else
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (runKey is null)
                {
                    return false;
                }

                var command = runKey.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
                if (string.IsNullOrEmpty(command)) return false;

                using var backupKey = Registry.CurrentUser.CreateSubKey(backupSubkey, true);
                if (backupKey == null) return false;
                backupKey.SetValue(name, command, runKey.GetValueKind(name));
                runKey.DeleteValue(name, false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ToggleMachineRegistryEntry(StartupItem item, bool enabled)
    {
        if (!IsAdministrator())
        {
            return false;
        }

        try
        {
            string backupPath = $@"{BackupKeyPath}\HKLM_Run\{item.RegistryView}";
            using var machineHive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, item.RegistryView);
            if (enabled)
            {
                using var backupKey = machineHive.OpenSubKey(backupPath, true);
                object? command = backupKey?.GetValue(item.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (command is not string commandText || backupKey is null)
                {
                    return false;
                }

                using var runKey = machineHive.CreateSubKey(item.RegistryPath, true);
                if (runKey is null)
                {
                    return false;
                }

                runKey.SetValue(item.Name, commandText, backupKey.GetValueKind(item.Name));
                backupKey.DeleteValue(item.Name, false);
            }
            else
            {
                using var runKey = machineHive.OpenSubKey(item.RegistryPath, true);
                object? command = runKey?.GetValue(item.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (command is not string commandText || runKey is null)
                {
                    return false;
                }

                using var backupKey = machineHive.CreateSubKey(backupPath, true);
                if (backupKey is null)
                {
                    return false;
                }

                backupKey.SetValue(item.Name, commandText, runKey.GetValueKind(item.Name));
                runKey.DeleteValue(item.Name, false);
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || (item.NeedsAdmin && !IsAdministrator()))
        {
            return false;
        }

        if (item.Source == "hkcu_run")
        {
            return ToggleRegistryEntry(item.Name, enabled);
        }

        if (item.Source == "hklm_run")
        {
            return ToggleMachineRegistryEntry(item, enabled);
        }

        if (item.Source == "user_startup")
        {
            return ToggleStartupFile(UserStartupDir(), item.Name, enabled);
        }

        if (item.Source == "common_startup")
        {
            return ToggleStartupFile(CommonStartupDir(), item.Name, enabled);
        }

        return false;
    }
}
