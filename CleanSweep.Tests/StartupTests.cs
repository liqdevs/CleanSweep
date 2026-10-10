using System;
using System.IO;
using System.Linq;
using CleanSweep.Core.Startup;
using Xunit;

namespace CleanSweep.Tests;

public class StartupTests
{
    [Fact]
    public void TestDisableMovesFileToDisabledSubfolder()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepStartupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string shortcut = Path.Combine(tempDir, "MyApp.lnk");
            File.WriteAllText(shortcut, "fake shortcut");

            bool ok = StartupManager.ToggleStartupFile(tempDir, "MyApp", enabled: false);

            Assert.True(ok);
            Assert.False(File.Exists(shortcut));
            string disabledShortcut = Path.Combine(tempDir, StartupManager.DisabledSubfolder, "MyApp.lnk");
            Assert.True(File.Exists(disabledShortcut));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestEnableMovesFileBack()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepStartupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string disabledFolder = Path.Combine(tempDir, StartupManager.DisabledSubfolder);
            Directory.CreateDirectory(disabledFolder);
            string disabledShortcut = Path.Combine(disabledFolder, "MyApp.lnk");
            File.WriteAllText(disabledShortcut, "fake shortcut");

            bool ok = StartupManager.ToggleStartupFile(tempDir, "MyApp", enabled: true);

            Assert.True(ok);
            string activeShortcut = Path.Combine(tempDir, "MyApp.lnk");
            Assert.True(File.Exists(activeShortcut));
            Assert.False(File.Exists(disabledShortcut));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestListingReflectsEnabledAndDisabledItems()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepStartupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string activeFile = Path.Combine(tempDir, "Active.lnk");
            File.WriteAllText(activeFile, "x");

            string disabledFolder = Path.Combine(tempDir, StartupManager.DisabledSubfolder);
            Directory.CreateDirectory(disabledFolder);
            string pausedFile = Path.Combine(disabledFolder, "Paused.lnk");
            File.WriteAllText(pausedFile, "x");

            var items = StartupManager.ListStartupFolder(tempDir, source: "user_startup", needsAdmin: false);
            var byName = items.ToDictionary(it => it.Name);

            Assert.True(byName["Active"].Enabled);
            Assert.False(byName["Paused"].Enabled);
            Assert.False(byName["Active"].NeedsAdmin);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestToggleFailsGracefullyWhenItemMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepStartupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            bool ok = StartupManager.ToggleStartupFile(tempDir, "DoesNotExist", enabled: false);
            Assert.False(ok);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestNonShortcutFilesAreIgnored()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepStartupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string txtFile = Path.Combine(tempDir, "readme.txt");
            File.WriteAllText(txtFile, "not a startup item");

            var items = StartupManager.ListStartupFolder(tempDir, source: "user_startup", needsAdmin: false);
            Assert.Empty(items);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestToggleStartupFileRejectsBlankInput()
    {
        Assert.False(StartupManager.ToggleStartupFile(string.Empty, "MyApp", enabled: false));
        Assert.False(StartupManager.ToggleStartupFile(Path.GetTempPath(), string.Empty, enabled: false));
    }
}

