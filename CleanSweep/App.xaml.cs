using System.Diagnostics;
using System.IO;
using System.Windows;
using CleanSweep.Core.Cleaning;
using CleanSweep.Core.Scanning;

namespace CleanSweep;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 0)
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            return;
        }

        int exitCode;
        try
        {
            exitCode = await Task.Run(() => RunSilentCleanup(e.Args));
        }
        catch (Exception ex)
        {
            Trace.TraceError("Silent cleanup failed unexpectedly: {0}", ex);
            exitCode = 5;
        }

        Environment.ExitCode = exitCode;
        Shutdown(exitCode);
    }

    private static int RunSilentCleanup(string[] args)
    {
        if (args.Length == 0 || args[0] != "--silent")
        {
            Trace.TraceError("Invalid command line. Silent mode must begin with --silent.");
            return 2;
        }

        string? drive = null;
        string? categoriesValue = null;
        for (int index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length)
            {
                Trace.TraceError("Missing value for command-line option '{0}'.", args[index]);
                return 2;
            }

            string value = args[index + 1];
            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                Trace.TraceError("Missing value for command-line option '{0}'.", args[index]);
                return 2;
            }

            switch (args[index])
            {
                case "--drive" when drive is null:
                    drive = value;
                    break;
                case "--categories" when categoriesValue is null:
                    categoriesValue = value;
                    break;
                default:
                    Trace.TraceError("Unknown or repeated command-line option '{0}'.", args[index]);
                    return 2;
            }
        }

        if (string.IsNullOrWhiteSpace(drive) || string.IsNullOrWhiteSpace(categoriesValue))
        {
            Trace.TraceError("Silent cleanup requires --drive and --categories.");
            return 2;
        }

        string fullDrive;
        try
        {
            fullDrive = Path.GetFullPath(drive);
            string? root = Path.GetPathRoot(fullDrive);
            if (string.IsNullOrEmpty(root)
                || !string.Equals(
                    Path.TrimEndingDirectorySeparator(fullDrive),
                    Path.TrimEndingDirectorySeparator(root),
                    StringComparison.OrdinalIgnoreCase)
                || !new DriveInfo(root).IsReady)
            {
                Trace.TraceError("The drive argument must identify an available drive root.");
                return 2;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Trace.TraceError("Invalid drive argument: {0}", ex.Message);
            return 2;
        }

        string[] requestedKeys = categoriesValue.Split(',', StringSplitOptions.TrimEntries);
        if (requestedKeys.Length == 0
            || requestedKeys.Any(string.IsNullOrWhiteSpace)
            || requestedKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != requestedKeys.Length)
        {
            Trace.TraceError("The categories list must contain unique category keys.");
            return 2;
        }

        List<CleanSweep.Core.Models.Category> availableCategories;
        try
        {
            availableCategories = CategoryScanner.BuildCategories(fullDrive);
        }
        catch (Exception ex)
        {
            Trace.TraceError("Could not initialize cleanup categories: {0}", ex);
            return 3;
        }

        var categoriesByKey = availableCategories.ToDictionary(category => category.Key, StringComparer.OrdinalIgnoreCase);
        var selectedCategories = new List<CleanSweep.Core.Models.Category>();
        foreach (string key in requestedKeys)
        {
            if (!categoriesByKey.TryGetValue(key, out var category))
            {
                Trace.TraceError("Unknown cleanup category '{0}'.", key);
                return 2;
            }
            selectedCategories.Add(category);
        }

        foreach (var category in selectedCategories)
        {
            CategoryScanner.ScanCategory(category);
            if (!string.IsNullOrEmpty(category.Error))
            {
                Trace.TraceError("Could not scan category '{0}': {1}", category.Key, category.Error);
                return 3;
            }
        }

        bool hadErrors = false;
        foreach (var category in selectedCategories)
        {
            var result = CategoryCleaner.CleanCategory(category);
            Trace.TraceInformation(
                "Silent cleanup category {0}: removed {1} files and {2} folders; freed {3} bytes; skipped {4}.",
                category.Key,
                result.RemovedItems,
                result.RemovedDirectories,
                result.FreedBytes,
                result.SkippedItems);
            hadErrors |= result.SkippedItems > 0 || result.Errors.Count > 0 || result.OmittedErrorsCount > 0;
        }

        return hadErrors ? 4 : 0;
    }
}
