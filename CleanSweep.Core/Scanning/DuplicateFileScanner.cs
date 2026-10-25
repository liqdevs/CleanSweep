using System.Security.Cryptography;
using CleanSweep.Core.Models;

namespace CleanSweep.Core.Scanning;

public static class DuplicateFileScanner
{
    private const int FingerprintBytes = 4096;

    public static DuplicateScanResult Scan(
        IEnumerable<string> roots,
        CancellationToken cancellationToken = default,
        IProgress<DuplicateScanProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(roots);

        var filesBySize = new Dictionary<long, List<DuplicateFile>>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int filesScanned = 0;
        int accessErrors = 0;
        DateTime lastProgress = DateTime.MinValue;

        foreach (string root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
            string fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot))
            {
                throw new DirectoryNotFoundException($"The duplicate scan folder does not exist: {fullRoot}");
            }

            foreach (FileInfo file in EnumerateFiles(fullRoot, cancellationToken, () => accessErrors++))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seenPaths.Add(file.FullName))
                {
                    continue;
                }
                try
                {
                    long size = file.Length;
                    if (!filesBySize.TryGetValue(size, out List<DuplicateFile>? sameSize))
                    {
                        sameSize = new List<DuplicateFile>();
                        filesBySize.Add(size, sameSize);
                    }

                    sameSize.Add(new DuplicateFile(file.FullName, size, file.LastWriteTimeUtc));
                    filesScanned++;
                    if (progress is not null && DateTime.UtcNow - lastProgress >= TimeSpan.FromMilliseconds(150))
                    {
                        lastProgress = DateTime.UtcNow;
                        progress.Report(new DuplicateScanProgress(filesScanned, file.FullName));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                }
            }
        }

        var duplicates = new List<DuplicateFileGroup>();
        foreach (List<DuplicateFile> sizeGroup in filesBySize.Values.Where(group => group.Count > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fingerprintGroups = new Dictionary<string, List<DuplicateFile>>(StringComparer.Ordinal);
            foreach (DuplicateFile file in sizeGroup)
            {
                try
                {
                    string fingerprint = CalculateFastFingerprint(file.Path, cancellationToken);
                    if (!fingerprintGroups.TryGetValue(fingerprint, out List<DuplicateFile>? matches))
                    {
                        matches = new List<DuplicateFile>();
                        fingerprintGroups.Add(fingerprint, matches);
                    }
                    matches.Add(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    accessErrors++;
                }
            }

            foreach (List<DuplicateFile> fingerprintGroup in fingerprintGroups.Values.Where(group => group.Count > 1))
            {
                var hashGroups = new Dictionary<string, List<DuplicateFile>>(StringComparer.Ordinal);
                foreach (DuplicateFile file in fingerprintGroup)
                {
                    try
                    {
                        string hash = CalculateFullHash(file.Path, cancellationToken);
                        if (!hashGroups.TryGetValue(hash, out List<DuplicateFile>? matches))
                        {
                            matches = new List<DuplicateFile>();
                            hashGroups.Add(hash, matches);
                        }
                        matches.Add(file);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        accessErrors++;
                    }
                }

                foreach ((string hash, List<DuplicateFile> matchingFiles) in hashGroups.Where(group => group.Value.Count > 1))
                {
                    var groupFiles = matchingFiles
                        .OrderBy(file => file.LastWriteTimeUtc)
                        .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    duplicates.Add(new DuplicateFileGroup(hash, groupFiles));
                }
            }
        }

        progress?.Report(new DuplicateScanProgress(filesScanned, string.Empty));
        var sortedDuplicates = duplicates
            .OrderByDescending(group => group.RecoverableBytes)
            .ThenBy(group => group.Files[0].Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new DuplicateScanResult(sortedDuplicates, filesScanned, accessErrors);
    }

    public static bool MatchesContentHash(
        string path,
        long expectedSize,
        string expectedHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);
        var file = new FileInfo(path);
        return file.Exists
            && file.Length == expectedSize
            && string.Equals(CalculateFullHash(path, cancellationToken), expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> SelectCopiesExceptOriginal(DuplicateFileGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Files
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .Skip(1)
            .Select(file => file.Path)
            .ToArray();
    }

    private static IEnumerable<FileInfo> EnumerateFiles(
        string root,
        CancellationToken cancellationToken,
        Action reportAccessError)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directoryPath = pending.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directoryPath).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                reportAccessError();
                continue;
            }

            foreach (string entryPath in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    reportAccessError();
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entryPath);
                }
                else
                {
                    yield return new FileInfo(entryPath);
                }
            }
        }
    }

    private static string CalculateFastFingerprint(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            FingerprintBytes,
            FileOptions.SequentialScan);
        byte[] buffer = new byte[FingerprintBytes * 2];
        int firstRead = stream.Read(buffer, 0, FingerprintBytes);
        int secondRead = 0;
        if (stream.Length > FingerprintBytes)
        {
            stream.Seek(Math.Max(FingerprintBytes, stream.Length - FingerprintBytes), SeekOrigin.Begin);
            secondRead = stream.Read(buffer, FingerprintBytes, FingerprintBytes);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, firstRead + secondRead)));
    }

    private static string CalculateFullHash(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, bytesRead);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
