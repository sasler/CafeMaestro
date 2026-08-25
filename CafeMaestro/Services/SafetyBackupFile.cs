using System.Text.Json;
using CafeMaestro.Models;

namespace CafeMaestro.Services;

internal static class SafetyBackupFile
{
    public const string SearchPattern = "cafemaestro_safety_*.json";

    public static string CreatePath(string backupDirectory)
    {
        Directory.CreateDirectory(backupDirectory);
        return Path.Combine(
            backupDirectory,
            $"cafemaestro_safety_{DateTime.UtcNow:yyyyMMdd_HHmmss_fffffff}_{Guid.NewGuid():N}.json");
    }

    public static async Task<string> CopyOriginalAsync(
        string sourcePath,
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        string backupPath = await WriteAtomicallyAsync(
            backupDirectory,
            async destination =>
            {
                await using FileStream source = new(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    useAsync: true);
                await source.CopyToAsync(destination, cancellationToken);
            },
            cancellationToken);

        foreach (string existingBackup in Directory.EnumerateFiles(backupDirectory, SearchPattern))
        {
            if (string.Equals(existingBackup, backupPath, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                if (!await FilesHaveSameContentAsync(
                        backupPath,
                        existingBackup,
                        cancellationToken))
                {
                    continue;
                }

                File.Delete(backupPath);
                return existingBackup;
            }
            catch (IOException)
            {
                // A concurrently removed or unreadable backup cannot prove duplication.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve the newly published recovery copy if deduplication is unavailable.
            }
        }

        Prune(backupDirectory, maximumBackups: 5, cancellationToken);
        return backupPath;
    }

    public static async Task<string> SerializeAsync(
        AppData data,
        string backupDirectory,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        return await WriteAtomicallyAsync(
            backupDirectory,
            stream => JsonSerializer.SerializeAsync(
                stream,
                data,
                options,
                cancellationToken),
            cancellationToken);
    }

    private static async Task<string> WriteAtomicallyAsync(
        string backupDirectory,
        Func<Stream, Task> writeAsync,
        CancellationToken cancellationToken)
    {
        string backupPath = CreatePath(backupDirectory);
        string temporaryPath = Path.Combine(
            backupDirectory,
            $".{Path.GetFileName(backupPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             useAsync: true))
            {
                await writeAsync(stream);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, backupPath);
            return backupPath;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
                // A hidden temporary file is not a discoverable recovery artifact.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup is best-effort after a failed or completed atomic publication.
            }
        }
    }

    private static async Task<bool> FilesHaveSameContentAsync(
        string leftPath,
        string rightPath,
        CancellationToken cancellationToken)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        await using FileStream left = new(
            leftPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
        await using FileStream right = new(
            rightPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
        byte[] leftBuffer = new byte[81920];
        byte[] rightBuffer = new byte[81920];

        while (true)
        {
            int leftRead = await left.ReadAsync(leftBuffer, cancellationToken);
            int rightRead = await right.ReadAsync(rightBuffer, cancellationToken);
            if (leftRead != rightRead)
            {
                return false;
            }

            if (leftRead == 0)
            {
                return true;
            }

            if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
            {
                return false;
            }
        }
    }

    private static void Prune(
        string backupDirectory,
        int maximumBackups,
        CancellationToken cancellationToken)
    {
        string[] backups = Directory
            .EnumerateFiles(backupDirectory, SearchPattern)
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToArray();
        foreach (string obsoleteBackup in backups.Skip(maximumBackups))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(obsoleteBackup);
        }
    }
}
