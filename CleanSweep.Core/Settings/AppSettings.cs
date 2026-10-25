using System.Diagnostics;
using Microsoft.Win32;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Utils;

namespace CleanSweep.Core.Settings;

public static class AppSettings
{
    private const string RegistryPath = @"Software\CleanSweep";

    public static string LoadSavedLanguage()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            string? value = key?.GetValue("Language")?.ToString();
            return string.IsNullOrWhiteSpace(value)
                ? I18n.DefaultLanguage
                : I18n.NormalizeLanguageCode(value);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Trace.TraceWarning("Could not load the saved application language: {0}", ex.Message);
            return I18n.DefaultLanguage;
        }
    }

    public static string LoadSavedDrive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            string? value = key?.GetValue("Drive")?.ToString();
            return string.IsNullOrWhiteSpace(value) ? FileSystemUtils.SystemDrive() : value;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Trace.TraceWarning("Could not load the saved application drive: {0}", ex.Message);
            return FileSystemUtils.SystemDrive();
        }
    }

    public static void SaveLanguage(string language)
    {
        SaveValue("Language", I18n.NormalizeLanguageCode(language));
    }

    public static void SaveDrive(string drive)
    {
        if (!string.IsNullOrWhiteSpace(drive))
        {
            SaveValue("Drive", drive);
        }
    }

    private static void SaveValue(string name, string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue(name, value, RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Trace.TraceWarning("Could not save application setting '{0}': {1}", name, ex.Message);
        }
    }
}
