using System;
using System.IO;
using CleanSweep.Core.Cleaning;
using Xunit;

namespace CleanSweep.Tests;

public class CleanupSafetyTests
{
    [Fact]
    public void TestRecentFilesAreSkippedAutomatically()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string target = Path.Combine(tempDir, "active.tmp");
            File.WriteAllText(target, "temporary content");

            bool shouldSkip = CategoryCleaner.ShouldSkipForCleanup(target, recentSeconds: 60);
            Assert.True(shouldSkip);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TestOldFilesAreAllowedForCleanup()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string target = Path.Combine(tempDir, "old.tmp");
            File.WriteAllText(target, "temporary content");
            File.SetLastWriteTime(target, DateTime.Now.AddHours(-1));

            bool shouldSkip = CategoryCleaner.ShouldSkipForCleanup(target, recentSeconds: 60);
            Assert.False(shouldSkip);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CleanFilesDeletesOnlySelectedOldFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string selected = Path.Combine(tempDir, "selected.tmp");
            string unselected = Path.Combine(tempDir, "unselected.tmp");
            File.WriteAllText(selected, "selected");
            File.WriteAllText(unselected, "unselected");
            File.SetLastWriteTime(selected, DateTime.Now.AddHours(-1));
            File.SetLastWriteTime(unselected, DateTime.Now.AddHours(-1));

            var result = CategoryCleaner.CleanFiles(new[] { selected });

            Assert.Equal(1, result.RemovedItems);
            Assert.Equal(8, result.FreedBytes);
            Assert.False(File.Exists(selected));
            Assert.True(File.Exists(unselected));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CleanFilesSkipsRecentlyModifiedFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string target = Path.Combine(tempDir, "recent.tmp");
            File.WriteAllText(target, "recent file");

            var result = CategoryCleaner.CleanFiles(new[] { target });

            Assert.Equal(0, result.RemovedItems);
            Assert.Equal(1, result.SkippedItems);
            Assert.True(File.Exists(target));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CleanFilesWithinRootDoesNotDeleteFilesOutsideTheAllowedFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepAllowed_" + Guid.NewGuid().ToString("N"));
        string outside = Path.Combine(Path.GetTempPath(), "CleanSweepOutside_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        try
        {
            string target = Path.Combine(outside, "outside.tmp");
            File.WriteAllText(target, "outside");
            File.SetLastWriteTime(target, DateTime.Now.AddHours(-1));

            var result = CategoryCleaner.CleanFilesWithinRoot(new[] { target }, root);

            Assert.Equal(0, result.RemovedItems);
            Assert.Equal(1, result.SkippedItems);
            Assert.True(File.Exists(target));
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(outside, true);
        }
    }

    [Fact]
    public void CleanFilesWithinRootCanDeleteRecentFilesWhenExplicitlyAllowed()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepRecentAllowed_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string target = Path.Combine(root, "recent.tmp");
            File.WriteAllText(target, "recent file");

            var result = CategoryCleaner.CleanFilesWithinRoot(
                new[] { target },
                root,
                allowRecentFiles: true);

            Assert.Equal(1, result.RemovedItems);
            Assert.False(File.Exists(target));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CleanDirectoryWithinRootDeletesNestedContentsWithoutMaterializingFileList()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepFolderCleanup_" + Guid.NewGuid().ToString("N"));
        string selected = Path.Combine(root, "Selected");
        Directory.CreateDirectory(Path.Combine(selected, "Nested"));
        try
        {
            File.WriteAllText(Path.Combine(selected, "first.tmp"), "first");
            File.WriteAllText(Path.Combine(selected, "Nested", "second.tmp"), "second");

            var result = CategoryCleaner.CleanDirectoryWithinRoot(selected, root);

            Assert.Equal(2, result.RemovedItems);
            Assert.Equal(2, result.RemovedDirectories);
            Assert.Equal(11, result.FreedBytes);
            Assert.False(Directory.Exists(selected));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CleanDirectoryWithinRootCannotDeleteAllowedRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepFolderRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<ArgumentException>(() => CategoryCleaner.CleanDirectoryWithinRoot(root, root));
            Assert.True(Directory.Exists(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
