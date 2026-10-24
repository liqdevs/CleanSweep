using System.IO;
using CleanSweep.Core.Models;
using CleanSweep.Core.Scanning;
using Xunit;

namespace CleanSweep.Tests;

public class DiskStatisticsTests
{
    [Fact]
    public void ScanGroupsFilesByTopLevelFolderAndFindsLargestFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepDiskStats_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "FolderA", "Nested"));
        Directory.CreateDirectory(Path.Combine(root, "FolderB"));
        Directory.CreateDirectory(Path.Combine(root, "EmptyFolder"));
        try
        {
            File.WriteAllBytes(Path.Combine(root, "root.bin"), new byte[2]);
            File.WriteAllBytes(Path.Combine(root, "FolderA", "Nested", "large.bin"), new byte[12]);
            File.WriteAllBytes(Path.Combine(root, "FolderB", "small.bin"), new byte[5]);

            var result = DiskStatisticsScanner.Scan(root);

            Assert.Equal(3, result.FileCount);
            Assert.Equal(19, result.ScannedBytes);
            Assert.Equal(4, result.Folders.Count);
            Assert.Equal("FolderA", result.Folders[0].Name);
            Assert.Equal(12, result.Folders[0].SizeBytes);
            Assert.Equal(2, result.Folders.Single(folder => folder.Name.Length == 0).SizeBytes);
            Assert.Equal(0, result.Folders.Single(folder => folder.Name == "EmptyFolder").SizeBytes);
            Assert.Equal(Path.Combine(root, "FolderA", "Nested", "large.bin"), result.LargestFiles[0].Path);
            Assert.Equal(12, result.LargestFiles[0].SizeBytes);
            Assert.Equal(result.TotalCapacityBytes, result.DriveUsedBytes + result.FreeBytes);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ScanHonorsCancellationBeforeWalkingFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepDiskStats_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            Assert.Throws<OperationCanceledException>(() =>
                DiskStatisticsScanner.Scan(root, cancellationToken: cancellation.Token));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ScanRejectsBlankPath()
    {
        Assert.Throws<ArgumentException>(() => DiskStatisticsScanner.Scan(" "));
    }

    [Fact]
    public void ListDirectoryContentsReturnsDirectFilesAndRecursiveChildFolderSizes()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepBrowse_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "FolderA", "Nested"));
        Directory.CreateDirectory(Path.Combine(root, "FolderB"));
        try
        {
            File.WriteAllBytes(Path.Combine(root, "root.bin"), new byte[3]);
            File.WriteAllBytes(Path.Combine(root, "FolderA", "direct.bin"), new byte[5]);
            File.WriteAllBytes(Path.Combine(root, "FolderA", "Nested", "nested.bin"), new byte[7]);
            File.WriteAllBytes(Path.Combine(root, "FolderB", "other.bin"), new byte[11]);

            var scan = DiskStatisticsScanner.Scan(root);
            var listing = DiskStatisticsScanner.ListDirectoryContents(
                root,
                root,
                directoryUsage: scan.DirectoryUsage);

            Assert.Single(listing.Files);
            Assert.Equal(Path.Combine(root, "root.bin"), listing.Files[0].Path);
            Assert.Equal(3, listing.Files[0].SizeBytes);
            Assert.Equal(2, listing.Folders.Count);
            Assert.Equal(12, listing.Folders.Single(folder => folder.Name == "FolderA").SizeBytes);
            Assert.Equal(2, listing.Folders.Single(folder => folder.Name == "FolderA").FileCount);

            var nestedListing = DiskStatisticsScanner.ListDirectoryContents(
                Path.Combine(root, "FolderA"),
                root,
                directoryUsage: scan.DirectoryUsage);
            Assert.Single(nestedListing.Files);
            Assert.Equal("direct.bin", Path.GetFileName(nestedListing.Files[0].Path));
            Assert.Equal(7, nestedListing.Folders.Single(folder => folder.Name == "Nested").SizeBytes);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ListDirectoryContentsRejectsPathsOutsideTheSelectedRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepBrowse_" + Guid.NewGuid().ToString("N"));
        string outside = Path.Combine(Path.GetTempPath(), "CleanSweepOutside_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);

        try
        {
            Assert.Throws<ArgumentException>(() => DiskStatisticsScanner.ListDirectoryContents(outside, root));
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(outside, true);
        }
    }

    [Fact]
    public void GetFolderDeletionContentsReturnsFilesAndDirectoriesWithoutIncludingTheSelectedVolumeRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepDeleteFolder_" + Guid.NewGuid().ToString("N"));
        string selected = Path.Combine(root, "Selected");
        Directory.CreateDirectory(Path.Combine(selected, "Nested"));
        try
        {
            File.WriteAllBytes(Path.Combine(selected, "direct.bin"), new byte[3]);
            File.WriteAllBytes(Path.Combine(selected, "Nested", "nested.bin"), new byte[7]);

            var progress = new ImmediateProgress<DiskScanProgress>();
            var contents = DiskStatisticsScanner.GetFolderDeletionContents(
                selected,
                root,
                progress: progress);

            Assert.Equal(2, contents.Files.Count);
            Assert.Contains(contents.Files, file => file.Path == Path.Combine(selected, "direct.bin"));
            Assert.Contains(contents.Files, file => file.Path == Path.Combine(selected, "Nested", "nested.bin"));
            Assert.Equal(2, contents.Directories.Count);
            Assert.Contains(selected, contents.Directories);
            Assert.Contains(Path.Combine(selected, "Nested"), contents.Directories);
            Assert.Equal(3, progress.Values[^1].ItemsScanned);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetFolderDeletionContentsRejectsDeletingTheSelectedVolumeRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepDeleteRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<ArgumentException>(() => DiskStatisticsScanner.GetFolderDeletionContents(root, root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class ImmediateProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = new();

        public void Report(T value) => Values.Add(value);
    }
}
