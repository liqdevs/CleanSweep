using System.IO;
using System.Security;

namespace CleanSweep.Core.Cleaning;

public sealed record FileImportanceAssessment(string Risk, string DescriptionKey);

public static class FileImportanceAnalyzer
{
    private static readonly string[] NormalizedSystemRoots = NormalizeRoots(GetSystemRoots());
    private static readonly string[] NormalizedTemporaryRoots = NormalizeRoots(GetTemporaryRoots());
    private static readonly string[] NormalizedUserDataRoots = NormalizeRoots(GetUserDataRoots());

    private static readonly HashSet<string> RegenerableDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "temp", "tmp", "cache", "caches", "code cache", "gpucache", "dxcache", "d3dscache", "shadercache"
    };

    private static readonly HashSet<string> UserDataExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".odt", ".rtf", ".pdf",
        ".xls", ".xlsx", ".ods", ".csv",
        ".ppt", ".pptx", ".odp",
        ".txt", ".md", ".rtf",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".heic", ".raw",
        ".mp3", ".wav", ".flac", ".m4a", ".mp4", ".mov", ".mkv", ".avi",
        ".zip", ".7z", ".rar", ".tar", ".gz",
        ".cs", ".py", ".js", ".ts", ".html", ".css", ".json", ".xml", ".sql"
    };

    private static readonly HashSet<string> SystemManagedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "hiberfil.sys", "pagefile.sys", "swapfile.sys", "bootmgr", "bootmgr.efi",
        "bootnxt", "ntldr", "ntdetect.com"
    };

    private static readonly HashSet<string> SystemManagedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system volume information"
    };

    public static FileImportanceAssessment Analyze(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Unknown();
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or SecurityException)
        {
            return Unknown();
        }

        if (IsUnderAnyRoot(fullPath, NormalizedTemporaryRoots) || HasRegenerablePathComponent(fullPath))
        {
            return new FileImportanceAssessment("green", "preview_importance_temporary");
        }

        if (IsSystemManagedPath(fullPath) || IsUnderAnyRoot(fullPath, NormalizedSystemRoots))
        {
            return new FileImportanceAssessment("red", "preview_importance_system");
        }

        if (IsUnderAnyRoot(fullPath, NormalizedUserDataRoots) ||
            UserDataExtensions.Contains(Path.GetExtension(fullPath)))
        {
            return new FileImportanceAssessment("yellow", "preview_importance_user");
        }

        return Unknown();
    }

    private static FileImportanceAssessment Unknown() =>
        new("yellow", "preview_importance_unknown");

    private static IEnumerable<string> GetSystemRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }

    private static IEnumerable<string> GetTemporaryRoots()
    {
        yield return Path.GetTempPath();
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
    }

    private static IEnumerable<string> GetUserDataRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string[] NormalizeRoots(IEnumerable<string> roots)
    {
        var normalizedRoots = new List<string>();
        foreach (string root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            normalizedRoots.Add(Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        }

        return normalizedRoots.ToArray();
    }

    private static bool IsUnderAnyRoot(string path, IEnumerable<string> roots)
    {
        foreach (string root in roots)
        {
            if (string.Equals(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    root,
                    StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRegenerablePathComponent(string path)
    {
        string? current = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(current))
        {
            if (RegenerableDirectoryNames.Contains(Path.GetFileName(current)))
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            current = parent;
        }

        return false;
    }

    private static bool IsSystemManagedPath(string path)
    {
        if (SystemManagedFileNames.Contains(Path.GetFileName(path)))
        {
            return true;
        }

        string? current = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(current))
        {
            if (SystemManagedDirectoryNames.Contains(Path.GetFileName(current)))
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            current = parent;
        }

        try
        {
            return File.Exists(path) && (File.GetAttributes(path) & FileAttributes.System) != 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            return false;
        }
    }
}
