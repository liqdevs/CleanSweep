using System.IO;
using CleanSweep.Core.Scanning;
using CleanSweep.Core.Utils;
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

            var contents = DiskStatisticsScanner.GetFolderDeletionContents(selected, root);

            Assert.Equal(2, contents.Files.Count);
            Assert.Contains(contents.Files, file => file.Path == Path.Combine(selected, "direct.bin"));
            Assert.Contains(contents.Files, file => file.Path == Path.Combine(selected, "Nested", "nested.bin"));
            Assert.Equal(2, contents.Directories.Count);
            Assert.Contains(selected, contents.Directories);
            Assert.Contains(Path.Combine(selected, "Nested"), contents.Directories);
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

    [Fact]
    public void GetFolderPreviewContentsRetainsOnlyTheLargestRequestedFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepPreview_" + Guid.NewGuid().ToString("N"));
        string selected = Path.Combine(root, "Selected");
        Directory.CreateDirectory(Path.Combine(selected, "Nested"));
        try
        {
            File.WriteAllBytes(Path.Combine(selected, "small.bin"), new byte[2]);
            File.WriteAllBytes(Path.Combine(selected, "Nested", "large.bin"), new byte[12]);
            File.WriteAllBytes(Path.Combine(selected, "Nested", "medium.bin"), new byte[7]);

            var result = DiskStatisticsScanner.GetFolderPreviewContents(
                selected,
                Path.GetPathRoot(selected)!,
                largestFileLimit: 2);

            Assert.Equal(3, result.FileCount);
            Assert.Equal(21, result.TotalSizeBytes);
            Assert.Equal(2, result.LargestFiles.Count);
            Assert.Equal("large.bin", Path.GetFileName(result.LargestFiles[0].Path));
            Assert.Equal("medium.bin", Path.GetFileName(result.LargestFiles[1].Path));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetFolderPreviewContentsHandlesThousandsOfFilesWithBoundedSamples()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepPreviewMany_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (int index = 0; index < 2000; index++)
            {
                File.WriteAllBytes(Path.Combine(root, $"{index:D4}.bin"), new byte[index % 31]);
            }

            var result = DiskStatisticsScanner.GetFolderPreviewContents(
                root,
                Path.GetPathRoot(root)!,
                largestFileLimit: 7);

            Assert.Equal(2000, result.FileCount);
            Assert.Equal(7, result.LargestFiles.Count);
            Assert.Equal(30, result.LargestFiles[0].SizeBytes);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DirectorySizeCountsNestedFilesWithoutFollowingReparsePoints()
    {
        string root = Path.Combine(Path.GetTempPath(), "CleanSweepDirSize_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Nested"));
        try
        {
            File.WriteAllBytes(Path.Combine(root, "direct.bin"), new byte[3]);
            File.WriteAllBytes(Path.Combine(root, "Nested", "nested.bin"), new byte[8]);

            var (size, count) = FileSystemUtils.DirSize(root);

            Assert.Equal(11, size);
            Assert.Equal(2, count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
