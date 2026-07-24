using CafeMaestro.Models;

namespace CafeMaestro.Services;

public sealed record DataBackupSummary(
    string Id,
    string DisplayName,
    DateTime CreatedAt,
    DateTime LastModified,
    string AppVersion,
    int BeanCount,
    int RoastCount);

public interface IDataBackupService
{
    Task<DataBackupSummary> PreviewExternalBackupAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<AppData> RestoreExternalBackupAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<AppData> StartNewDataAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DataBackupSummary>> GetSafetyBackupsAsync(
        CancellationToken cancellationToken = default);

    Task<AppData> RestoreSafetyBackupAsync(
        string backupId,
        CancellationToken cancellationToken = default);

    Task<Stream> CreateExportStreamAsync(CancellationToken cancellationToken = default);
}
