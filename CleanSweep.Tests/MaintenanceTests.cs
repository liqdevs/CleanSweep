using System;
using System.IO;
using System.Linq;
using CleanSweep.Core.Cleaning;
using CleanSweep.Core.Scanning;

namespace CleanSweep.Tests;

public class MaintenanceTests
{
    [Fact]
    public void DuplicateScannerMatchesOnlyIdenticalFilesAndKeepsOldestAsOriginal()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepDuplicates_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] contents = new byte[12_000];
            Random.Shared.NextBytes(contents);
            string original = Path.Combine(root, "original.bin");
            string copy = Path.Combine(root, "copy.bin");
            string different = Path.Combine(root, "different.bin");
            File.WriteAllBytes(original, contents);
            File.WriteAllBytes(copy, contents);
            byte[] differentContents = contents.ToArray();
            differentContents[^1] ^= 0xFF;
            File.WriteAllBytes(different, differentContents);
            File.SetLastWriteTimeUtc(original, DateTime.UtcNow.AddDays(-2));
            File.SetLastWriteTimeUtc(copy, DateTime.UtcNow.AddDays(-1));

            var result = DuplicateFileScanner.Scan(new[] { root });

            var group = Assert.Single(result.Groups);
            Assert.Equal(3, result.FilesScanned);
            Assert.Equal(12_000, group.SizeBytes);
            Assert.Equal(12_000, group.RecoverableBytes);
            Assert.Equal(copy, Assert.Single(DuplicateFileScanner.SelectCopiesExceptOriginal(group)));
            Assert.DoesNotContain(group.Files, file => file.Path == different);
            Assert.True(DuplicateFileScanner.MatchesContentHash(copy, 12_000, group.Hash));

            File.WriteAllBytes(copy, new byte[12_000]);
            Assert.False(DuplicateFileScanner.MatchesContentHash(copy, 12_000, group.Hash));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DuplicateScannerRejectsMissingRoot()
    {
        string missing = Path.Combine(Path.GetTempPath(), "CleanSweepMissing_" + Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => DuplicateFileScanner.Scan(new[] { missing }));
    }

    [Fact]
    public void DuplicateScannerDoesNotCountOverlappingRootsTwice()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepOverlappingRoots_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "file.txt"), "same contents");

            var result = DuplicateFileScanner.Scan(new[] { root, Path.Combine(root, ".") });

            Assert.Equal(1, result.FilesScanned);
            Assert.Empty(result.Groups);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DuplicateScannerHonorsCancellation()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepCancelledDuplicates_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            Assert.Throws<OperationCanceledException>(() =>
                DuplicateFileScanner.Scan(new[] { root }, cancellation.Token));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EmptyFolderScannerFindsEmptyTreesWithoutIncludingSelectedRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepEmptyFolders_" + Guid.NewGuid().ToString("N"));
        string emptyTree = Path.Combine(root, "Empty", "Nested");
        string nonEmpty = Path.Combine(root, "HasData");
        Directory.CreateDirectory(emptyTree);
        Directory.CreateDirectory(nonEmpty);
        File.WriteAllText(Path.Combine(nonEmpty, "keep.txt"), "data");
        try
        {
            var result = EmptyFolderCleaner.FindEmptyDirectories(root);

            Assert.Equal(4, result.DirectoriesScanned);
            Assert.Equal(2, result.EmptyDirectories.Count);
            Assert.DoesNotContain(root, result.EmptyDirectories);
            Assert.Contains(emptyTree, result.EmptyDirectories);
            Assert.Contains(Path.Combine(root, "Empty"), result.EmptyDirectories);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EmptyFolderDeletionRemovesOnlyEmptyDirectoriesUnderSelectedRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepDeleteEmpty_" + Guid.NewGuid().ToString("N"));
        string empty = Path.Combine(root, "Empty", "Nested");
        string nonEmpty = Path.Combine(root, "HasData");
        Directory.CreateDirectory(empty);
        Directory.CreateDirectory(nonEmpty);
        File.WriteAllText(Path.Combine(nonEmpty, "keep.txt"), "data");
        try
        {
            var result = EmptyFolderCleaner.DeleteEmptyDirectories(
                root,
                new[] { empty, Path.Combine(root, "Empty"), nonEmpty, root });

            Assert.Equal(2, result.RemovedDirectories);
            Assert.Equal(2, result.SkippedItems);
            Assert.True(File.Exists(Path.Combine(nonEmpty, "keep.txt")));
            Assert.True(Directory.Exists(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EmptyFolderDeletionRejectsSiblingPathWithSharedRootPrefix()
    {
        string parent = Path.Combine(Path.GetTempPath(), "CleanSweepPrefix_" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(parent, "root");
        string sibling = Path.Combine(parent, "root-sibling");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(sibling);
        try
        {
            var result = EmptyFolderCleaner.DeleteEmptyDirectories(root, new[] { sibling });

            Assert.Equal(0, result.RemovedDirectories);
            Assert.Equal(1, result.SkippedItems);
            Assert.True(Directory.Exists(sibling));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void EmptyFolderScannerHonorsCancellation()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepCancelledEmpty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            Assert.Throws<OperationCanceledException>(() =>
                EmptyFolderCleaner.FindEmptyDirectories(root, cancellation.Token));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
