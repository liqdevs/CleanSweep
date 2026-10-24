using System;
using System.IO;
using CleanSweep.Core.Cleaning;
using CleanSweep.Core.Models;
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
    public void CleanFilesReportsProgressWhileDeletingManyFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepProgress_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var files = Enumerable.Range(0, 130).Select(index =>
            {
                string path = Path.Combine(tempDir, $"{index:D3}.tmp");
                File.WriteAllText(path, "x");
                return path;
            }).ToList();
            foreach (string file in files)
            {
                File.SetLastWriteTime(file, DateTime.Now.AddHours(-1));
            }
            var progress = new ImmediateProgress<CleanProgress>();

            var result = CategoryCleaner.CleanFiles(files, progress: progress);

            Assert.Equal(files.Count, result.RemovedItems);
            Assert.Contains(progress.Values, value => value.ItemsProcessed == 128);
            Assert.Equal(files.Count, progress.Values[^1].ItemsProcessed);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CleanCategoryReportsProgressForDirectoryContents()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CleanSweepCategoryProgress_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string firstFile = Path.Combine(tempDir, "first.tmp");
            string secondFile = Path.Combine(tempDir, "second.tmp");
            File.WriteAllText(firstFile, "first");
            File.WriteAllText(secondFile, "second");
            File.SetLastWriteTime(firstFile, DateTime.Now.AddHours(-1));
            File.SetLastWriteTime(secondFile, DateTime.Now.AddHours(-1));
            var category = new Category(
                "test",
                new[] { new ScanTarget(tempDir, TargetKind.DirContents) });
            var progress = new ImmediateProgress<CleanProgress>();

            var result = CategoryCleaner.CleanCategory(category, progress);

            Assert.Equal(2, result.RemovedItems);
            Assert.Contains(progress.Values, value => value.CurrentPath == firstFile || value.CurrentPath == secondFile);
            Assert.False(File.Exists(firstFile));
            Assert.False(File.Exists(secondFile));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private sealed class ImmediateProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = new();

        public void Report(T value) => Values.Add(value);
    }
}
