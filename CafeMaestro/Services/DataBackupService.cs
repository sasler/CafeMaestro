using System.Text.Json;
using CafeMaestro.Models;

namespace CafeMaestro.Services;

public sealed class DataBackupService : IDataBackupService
{
    private const int MaximumSafetyBackups = 5;
    private readonly IAppDataService _appDataService;
    private readonly string _backupDirectory;
    private readonly Func<AppData, CancellationToken, Task>? _safetyBackupOverride;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public DataBackupService(IAppDataService appDataService)
        : this(appDataService, Path.Combine(FileSystem.AppDataDirectory, "Backups"))
    {
    }

    public DataBackupService(IAppDataService appDataService, string backupDirectory)
        : this(appDataService, backupDirectory, null)
    {
    }

    internal DataBackupService(
        IAppDataService appDataService,
        string backupDirectory,
        Func<AppData, CancellationToken, Task>? safetyBackupOverride)
    {
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _backupDirectory = string.IsNullOrWhiteSpace(backupDirectory)
            ? throw new ArgumentException("Backup directory is required.", nameof(backupDirectory))
            : backupDirectory;
        _safetyBackupOverride = safetyBackupOverride;
    }

    public async Task<DataBackupSummary> PreviewExternalBackupAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        AppData data = await ReadAndValidateAsync(filePath, cancellationToken);
        return CreateSummary(
            filePath,
            Path.GetFileName(filePath),
            File.GetCreationTimeUtc(filePath),
            data);
    }

    public async Task<AppData> RestoreExternalBackupAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        AppData data = await ReadAndValidateAsync(filePath, cancellationToken);
        return await ReplaceWithSafetyBackupAsync(data, cancellationToken);
    }

    public Task<AppData> StartNewDataAsync(CancellationToken cancellationToken = default)
    {
        return ReplaceWithSafetyBackupAsync(AppDataFactory.CreateDefault(), cancellationToken);
    }

    public async Task<IReadOnlyList<DataBackupSummary>> GetSafetyBackupsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_backupDirectory))
        {
            return [];
        }

        var summaries = new List<DataBackupSummary>();
        foreach (string filePath in Directory
                     .EnumerateFiles(_backupDirectory, SafetyBackupFile.SearchPattern)
                     .OrderByDescending(File.GetCreationTimeUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                AppData data = await ReadAndValidateAsync(filePath, cancellationToken);
                summaries.Add(CreateSummary(
                    Path.GetFileName(filePath),
                    "Automatic safety backup",
                    File.GetCreationTimeUtc(filePath),
                    data));
            }
            catch (InvalidDataException ex)
            {
                summaries.Add(new DataBackupSummary(
                    Path.GetFileName(filePath),
                    "Raw recovery copy",
                    File.GetCreationTimeUtc(filePath).ToLocalTime(),
                    File.GetLastWriteTimeUtc(filePath),
                    "Unvalidated",
                    0,
                    0,
                    IsRestorable: false,
                    RecoveryMessage: ex.Message));
            }
        }

        return summaries;
    }

    public async Task<AppData> RestoreSafetyBackupAsync(
        string backupId,
        CancellationToken cancellationToken = default)
    {
        string filePath = ResolveSafetyBackupPath(backupId);
        AppData data = await ReadAndValidateAsync(filePath, cancellationToken);
        return await ReplaceWithSafetyBackupAsync(data, cancellationToken);
    }

    public async Task<Stream> CreateSafetyBackupExportStreamAsync(
        string backupId,
        CancellationToken cancellationToken = default)
    {
        string filePath = ResolveSafetyBackupPath(backupId);
        var output = new MemoryStream();
        try
        {
            await using var source = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);
            await source.CopyToAsync(output, cancellationToken);
            output.Position = 0;
            return output;
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    public async Task<Stream> CreateExportStreamAsync(
        CancellationToken cancellationToken = default)
    {
        AppData currentData = await _appDataService.LoadAppDataAsync();
        var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(
            stream,
            currentData,
            _jsonOptions,
            cancellationToken);
        stream.Position = 0;
        return stream;
    }

    private async Task<AppData> ReplaceWithSafetyBackupAsync(
        AppData replacement,
        CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            if (_appDataService.IsRecoveryRequired)
            {
                return await _appDataService.ReplaceAppDataForRecoveryAsync(
                    replacement,
                    cancellationToken);
            }

            AppData currentData;
            try
            {
                currentData = await _appDataService.LoadAppDataAsync();
            }
            catch (Exception ex) when (
                ex is InvalidDataException or IOException or UnauthorizedAccessException &&
                _appDataService.IsRecoveryRequired)
            {
                return await _appDataService.ReplaceAppDataForRecoveryAsync(
                    replacement,
                    cancellationToken);
            }

            replacement.PersistenceRevision = currentData.PersistenceRevision;
            await CreateSafetyBackupAsync(currentData, cancellationToken);
            bool saved = await _appDataService.SaveAppDataAsync(replacement);
            if (!saved)
            {
                throw new IOException("CafeMaestro could not replace the current data safely.");
            }

            PruneSafetyBackups(cancellationToken);
            return await _appDataService.LoadAppDataAsync();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task CreateSafetyBackupAsync(
        AppData currentData,
        CancellationToken cancellationToken)
    {
        if (_safetyBackupOverride is not null)
        {
            await _safetyBackupOverride(currentData, cancellationToken);
            return;
        }

        await SafetyBackupFile.SerializeAsync(
            currentData,
            _backupDirectory,
            _jsonOptions,
            cancellationToken);
    }

    private void PruneSafetyBackups(CancellationToken cancellationToken)
    {
        string[] backups = Directory
            .EnumerateFiles(_backupDirectory, SafetyBackupFile.SearchPattern)
            .OrderByDescending(File.GetCreationTimeUtc)
            .ToArray();

        foreach (string obsoleteBackup in backups.Skip(MaximumSafetyBackups))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(obsoleteBackup);
        }
    }

    private async Task<AppData> ReadAndValidateAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);
            AppData data = await AppDataJsonReader.DeserializeAsync(
                stream,
                _jsonOptions,
                cancellationToken);
            bool migrated = new AppDataMigrationPipeline().MigrateToCurrent(data);
            if (migrated)
            {
                AppDataNormalizer.Normalize(data, allowLegacyRepairs: true);
            }
            Validate(data);
            return data;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "The selected file is not valid CafeMaestro JSON data.",
                ex);
        }
        catch (NotSupportedException ex)
        {
            throw new InvalidDataException(
                "The selected file contains unsupported CafeMaestro data.",
                ex);
        }
    }

    private static void Validate(AppData data)
    {
        List<string> errors = AppDataNormalizer.GetValidationErrors(data);

        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"The selected backup contains invalid data. {errors[0]}");
        }
    }

    private static DataBackupSummary CreateSummary(
        string id,
        string displayName,
        DateTime createdAt,
        AppData data)
    {
        return new DataBackupSummary(
            id,
            displayName,
            createdAt.ToLocalTime(),
            data.LastModified,
            data.AppVersion,
            data.Beans.Count,
            data.RoastLogs.Count);
    }

    private string ResolveSafetyBackupPath(string backupId)
    {
        string fileName = Path.GetFileName(backupId);
        string resolvedPath = Path.GetFullPath(Path.Combine(_backupDirectory, fileName));
        string resolvedDirectory = Path.GetFullPath(_backupDirectory) + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(resolvedDirectory, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("The selected safety backup no longer exists.", fileName);
        }

        return resolvedPath;
    }
}
