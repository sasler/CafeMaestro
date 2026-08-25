using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafeMaestro.Models;

namespace CafeMaestro.Services;

public sealed class ManagedAppDataService : IAppDataService
{
    private readonly string _canonicalFilePath;
    private readonly string _backupDirectory;
    private readonly Func<string> _appVersionProvider;
    private readonly Func<AppData, CancellationToken, Task>? _writeOverride;
    private readonly AppDataMigrationPipeline _migrationPipeline;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly JsonSerializerOptions _cloneOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _dataAccessLock = new(1, 1);
    private readonly SemaphoreSlim _notificationDispatchLock = new(1, 1);
    private readonly object _notificationQueueLock = new();
    private readonly object _dataChangedHandlerLock = new();
    private readonly Queue<PendingDataChange> _pendingDataChanges = new();
    private readonly AsyncLocal<bool> _isDispatchingDataChanged = new();
    private EventHandler<AppData>? _dataChanged;
    private AppData? _cachedData;
    private long _persistenceRevision;
    private bool _isInitialized;
    private bool _isRecoveryRequired;
    private int _notificationsSuspended;

    public ManagedAppDataService()
        : this(Path.Combine(FileSystem.AppDataDirectory, "cafemaestro_data.json"))
    {
    }

    public ManagedAppDataService(
        string canonicalFilePath,
        Func<string>? appVersionProvider = null)
        : this(canonicalFilePath, appVersionProvider, null)
    {
    }

    internal ManagedAppDataService(
        string canonicalFilePath,
        Func<string>? appVersionProvider,
        Func<AppData, CancellationToken, Task>? writeOverride,
        IEnumerable<IAppDataMigration>? migrations = null)
    {
        _canonicalFilePath = string.IsNullOrWhiteSpace(canonicalFilePath)
            ? throw new ArgumentException("Canonical data path is required.", nameof(canonicalFilePath))
            : Path.GetFullPath(canonicalFilePath);
        _backupDirectory = Path.Combine(
            Path.GetDirectoryName(_canonicalFilePath)
                ?? throw new ArgumentException("Canonical data path must have a directory.", nameof(canonicalFilePath)),
            "Backups");
        _appVersionProvider = appVersionProvider ?? GetAppVersion;
        _writeOverride = writeOverride;
        _migrationPipeline = new AppDataMigrationPipeline(migrations);
    }

    public event EventHandler<AppData>? DataChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            if (value.GetInvocationList().Any(handler =>
                    handler.Method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false)))
            {
                throw new ArgumentException(
                    "DataChanged subscribers must be synchronous. Start explicit background work instead.",
                    nameof(value));
            }

            lock (_dataChangedHandlerLock)
            {
                _dataChanged += value;
            }
        }
        remove
        {
            lock (_dataChangedHandlerLock)
            {
                _dataChanged -= value;
            }
        }
    }

    public event EventHandler<string>? DataFilePathChanged;

    public string DataFilePath => _canonicalFilePath;

    public AppData CurrentData => _cachedData ?? AppDataFactory.CreateDefault();

    public bool IsRecoveryRequired => Volatile.Read(ref _isRecoveryRequired);

    public IDisposable SuspendNotifications()
    {
        lock (_notificationQueueLock)
        {
            _notificationsSuspended++;
        }

        return new NotificationSuspension(this);
    }

    public async Task<AppData> InitializeAsync(IPreferencesService preferencesService)
    {
        ArgumentNullException.ThrowIfNull(preferencesService);
        await _initializationLock.WaitAsync();

        try
        {
            if (_isInitialized)
            {
                return Clone(CurrentData);
            }

            await _dataAccessLock.WaitAsync();
            AppData data;
            AppData result;
            PendingDataChange? pendingNotification;
            try
            {
                if (File.Exists(_canonicalFilePath))
                {
                    data = await ReadCanonicalAsync(migrateInPlace: true);
                }
                else
                {
                    string? legacyPath = await preferencesService.GetAppDataFilePathAsync();
                    data = await TryReadLegacyDataAsync(legacyPath)
                        ?? AppDataFactory.CreateDefault();
                    data.DataSchemaVersion = AppDataSchema.CurrentVersion;
                    await WriteAtomicAsync(data);
                }

                CacheLoadedData(data);
                _isInitialized = true;
                result = Clone(data);
                pendingNotification = EnqueueDataChanged(result);
            }
            finally
            {
                _dataAccessLock.Release();
            }
            await preferencesService.SaveAppDataFilePathAsync(_canonicalFilePath);
            await preferencesService.SetFirstRunCompletedAsync();
            DataFilePathChanged?.Invoke(this, _canonicalFilePath);
            if (pendingNotification is not null)
            {
                await PublishPendingDataChangesAsync(pendingNotification);
            }

            return result;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<AppData?> TryReadLegacyDataAsync(string? legacyPath)
    {
        if (string.IsNullOrWhiteSpace(legacyPath))
        {
            return null;
        }

        string? fullLegacyPath = null;
        try
        {
            fullLegacyPath = Path.GetFullPath(legacyPath);
            if (string.Equals(fullLegacyPath, _canonicalFilePath, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(fullLegacyPath))
            {
                return null;
            }

            return await ReadAndValidateAsync(fullLegacyPath, migrateInPlace: false);
        }
        catch (InvalidDataException)
        {
            if (fullLegacyPath is not null && File.Exists(fullLegacyPath))
            {
                await SafetyBackupFile.CopyOriginalAsync(
                    fullLegacyPath,
                    _backupDirectory);
            }

            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
    }
    public async Task<AppData> LoadAppDataAsync()
    {
        await _dataAccessLock.WaitAsync();
        try
        {
            return await LoadAppDataUnderLockAsync();
        }
        finally
        {
            _dataAccessLock.Release();
        }
    }

    private async Task<AppData> LoadAppDataUnderLockAsync()
    {
        if (!File.Exists(_canonicalFilePath))
        {
            if (_cachedData is null)
            {
                CacheLoadedData(AppDataFactory.CreateDefault());
            }

            return Clone(_cachedData!);
        }

        AppData data = await ReadCanonicalAsync(migrateInPlace: true);
        CacheLoadedData(data);
        return Clone(data);
    }

    public Task<bool> SaveAppDataAsync(AppData appData)
    {
        return SaveInternalAsync(appData, fireEvents: true);
    }

    public Task<bool> SaveAppDataWithoutNotificationAsync(AppData appData)
    {
        return SaveInternalAsync(appData, fireEvents: false);
    }

    public bool DataFileExists() => File.Exists(_canonicalFilePath);

    public Task<bool> UpdateAsync(
        Action<AppData> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        return UpdateCoreAsync(
            data =>
            {
                mutation(data);
                return true;
            },
            cancellationToken);
    }

    public Task<bool> TryUpdateAsync(
        Func<AppData, bool> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        return UpdateCoreAsync(mutation, cancellationToken);
    }

    private async Task<bool> UpdateCoreAsync(
        Func<AppData, bool> mutation,
        CancellationToken cancellationToken)
    {
        bool updated = false;
        AppData? committedData = null;
        PendingDataChange? pendingNotification = null;

        await _dataAccessLock.WaitAsync(cancellationToken);
        try
        {
            if (_isRecoveryRequired)
            {
                return false;
            }

            AppData source;
            bool sourceMigrated = false;
            if (_cachedData is not null)
            {
                source = _cachedData;
            }
            else if (File.Exists(_canonicalFilePath))
            {
                (source, sourceMigrated) = await ReadCanonicalResultAsync(
                    migrateInPlace: false,
                    cancellationToken);
            }
            else
            {
                source = AppDataFactory.CreateDefault();
            }

            AppData candidate = Clone(source);

            if (!mutation(candidate))
            {
                return false;
            }

            AppDataNormalizer.Normalize(candidate, allowLegacyRepairs: false);
            if (AppDataNormalizer.GetValidationErrors(candidate).Count > 0)
            {
                return false;
            }

            candidate.LastModified = DateTime.UtcNow;
            candidate.AppVersion = _appVersionProvider();
            try
            {
                if (sourceMigrated)
                {
                    await SafetyBackupFile.CopyOriginalAsync(
                        _canonicalFilePath,
                        _backupDirectory,
                        cancellationToken);
                }

                await WriteAtomicAsync(candidate, cancellationToken);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }

            CacheCommittedData(candidate);
            committedData = Clone(candidate);
            pendingNotification = EnqueueDataChanged(committedData);
            updated = true;
        }
        finally
        {
            _dataAccessLock.Release();
        }

        if (pendingNotification is not null)
        {
            await PublishPendingDataChangesAsync(pendingNotification);
        }

        return updated;
    }

    public async Task<AppData> ReloadDataAsync()
    {
        AppData data;
        PendingDataChange? pendingNotification;
        await _dataAccessLock.WaitAsync();
        try
        {
            data = await LoadAppDataUnderLockAsync();
            pendingNotification = EnqueueDataChanged(data);
        }
        finally
        {
            _dataAccessLock.Release();
        }

        if (pendingNotification is not null)
        {
            await PublishPendingDataChangesAsync(pendingNotification);
        }

        return data;
    }

    private async Task<bool> SaveInternalAsync(AppData appData, bool fireEvents)
    {
        ArgumentNullException.ThrowIfNull(appData);
        AppData? committedData = null;
        PendingDataChange? pendingNotification = null;
        bool saved = false;

        await _dataAccessLock.WaitAsync();
        try
        {
            if (_isRecoveryRequired)
            {
                return false;
            }

            if (_cachedData is null && File.Exists(_canonicalFilePath))
            {
                try
                {
                    await ReadCanonicalResultAsync(migrateInPlace: false);
                }
                catch (Exception ex) when (IsRecoverableLoadFailure(ex))
                {
                    return false;
                }

                return false;
            }

            if (_persistenceRevision > 0 &&
                appData.PersistenceRevision != _persistenceRevision)
            {
                return false;
            }

            AppData candidate = Clone(appData);
            AppDataNormalizer.Normalize(candidate, allowLegacyRepairs: false);
            if (AppDataNormalizer.GetValidationErrors(candidate).Count > 0)
            {
                return false;
            }

            candidate.LastModified = DateTime.UtcNow;
            candidate.AppVersion = _appVersionProvider();
            await WriteAtomicAsync(candidate);
            CacheCommittedData(candidate);
            committedData = Clone(candidate);
            if (fireEvents)
            {
                pendingNotification = EnqueueDataChanged(committedData);
            }
            saved = true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            _dataAccessLock.Release();
        }

        if (pendingNotification is not null)
        {
            await PublishPendingDataChangesAsync(pendingNotification);
        }

        return saved;
    }

    private async Task WriteAtomicAsync(
        AppData data,
        CancellationToken cancellationToken = default)
    {
        if (_writeOverride is not null)
        {
            await _writeOverride(data, cancellationToken);
            return;
        }

        string? directory = Path.GetDirectoryName(_canonicalFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The app data directory is unavailable.");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath =
            Path.Combine(directory, $".{Path.GetFileName(_canonicalFilePath)}.{Guid.NewGuid():N}.tmp");

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
                await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _canonicalFilePath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private async Task<AppData> ReadAndValidateAsync(
        string filePath,
        bool migrateInPlace,
        CancellationToken cancellationToken = default)
    {
        (AppData data, _) = await ReadAndValidateResultAsync(
            filePath,
            migrateInPlace,
            cancellationToken);
        return data;
    }

    private async Task<AppData> ReadCanonicalAsync(
        bool migrateInPlace,
        CancellationToken cancellationToken = default)
    {
        (AppData data, _) = await ReadCanonicalResultAsync(
            migrateInPlace,
            cancellationToken);
        return data;
    }

    private async Task<(AppData Data, bool Migrated)> ReadCanonicalResultAsync(
        bool migrateInPlace,
        CancellationToken cancellationToken = default)
    {
        try
        {
            (AppData data, bool migrated) = await ReadAndValidateResultAsync(
                _canonicalFilePath,
                migrateInPlace,
                cancellationToken);
            _isRecoveryRequired = false;
            return (data, migrated);
        }
        catch (Exception ex) when (IsRecoverableLoadFailure(ex))
        {
            _isRecoveryRequired = File.Exists(_canonicalFilePath);
            throw;
        }
    }

    private async Task<(AppData Data, bool Migrated)> ReadAndValidateResultAsync(
        string filePath,
        bool migrateInPlace,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool originalRecoveryCopied = false;
            if (migrateInPlace)
            {
                await using var schemaStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    useAsync: true);
                if (await AppDataJsonReader.IsLegacySchemaAsync(
                        schemaStream,
                        cancellationToken))
                {
                    await SafetyBackupFile.CopyOriginalAsync(
                        filePath,
                        _backupDirectory,
                        cancellationToken);
                    originalRecoveryCopied = true;
                }
            }

            AppData data;
            await using (var stream = new FileStream(
                             filePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             81920,
                             useAsync: true))
            {
                data = await AppDataJsonReader.DeserializeAsync(
                    stream,
                    _jsonOptions,
                    cancellationToken);
            }

            bool migrated = _migrationPipeline.MigrateToCurrent(data);
            if (migrated && migrateInPlace && !originalRecoveryCopied)
            {
                await SafetyBackupFile.CopyOriginalAsync(
                    filePath,
                    _backupDirectory,
                    cancellationToken);
            }

            if (migrated)
            {
                AppDataNormalizer.Normalize(data, allowLegacyRepairs: true);
            }
            List<string> errors = AppDataNormalizer.GetValidationErrors(data);
            if (errors.Count > 0)
            {
                throw new InvalidDataException($"The data file is invalid. {errors[0]}");
            }

            if (migrated && migrateInPlace)
            {
                data.LastModified = DateTime.UtcNow;
                data.AppVersion = _appVersionProvider();
                await WriteAtomicAsync(data, cancellationToken);
            }

            return (data, migrated);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The data file is not valid CafeMaestro JSON.", ex);
        }
    }

    private AppData Clone(AppData data)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(data, _cloneOptions);
        AppData clone = JsonSerializer.Deserialize<AppData>(json, _cloneOptions)
            ?? throw new InvalidDataException("The cached app data could not be copied safely.");
        clone.PersistenceRevision = data.PersistenceRevision;
        return clone;
    }

    private void CacheLoadedData(AppData data)
    {
        _persistenceRevision = checked(_persistenceRevision + 1);
        data.PersistenceRevision = _persistenceRevision;
        _cachedData = data;
    }

    private void CacheCommittedData(AppData data)
    {
        data.PersistenceRevision = checked(++_persistenceRevision);
        _cachedData = data;
    }

    private static bool IsRecoverableLoadFailure(Exception exception) =>
        exception is InvalidDataException or IOException or UnauthorizedAccessException;

    private static void TryDeleteTemporaryFile(string temporaryPath)
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
            // Cleanup is best-effort; the canonical replacement may already have succeeded.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best-effort; the canonical replacement may already have succeeded.
        }
    }

    private static string GetAppVersion()
    {
        try
        {
            string version = AppInfo.Current.VersionString;
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }
        catch (InvalidOperationException)
        {
            // AppInfo may be unavailable in a unit-test host.
        }
        catch (NotImplementedException)
        {
            // AppInfo may be unavailable in a unit-test host.
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // WinRT activation is unavailable outside a packaged Windows app process.
        }

        Version? assemblyVersion = typeof(ManagedAppDataService).Assembly.GetName().Version;
        return assemblyVersion is null
            ? "Unknown"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }

    private PendingDataChange? EnqueueDataChanged(AppData data)
    {
        lock (_notificationQueueLock)
        {
            if (_notificationsSuspended > 0)
            {
                return null;
            }

            var pending = new PendingDataChange(data);
            _pendingDataChanges.Enqueue(pending);
            return pending;
        }
    }

    private async Task PublishPendingDataChangesAsync(PendingDataChange pending)
    {
        if (_isDispatchingDataChanged.Value)
        {
            ScheduleNotificationDrain();
            return;
        }

        await DrainPendingDataChangesAsync();
        await pending.Completion.Task;
    }

    private void ScheduleNotificationDrain()
    {
        _ = Task.Run(DrainPendingDataChangesAsync);
    }

    private async Task DrainPendingDataChangesAsync()
    {
        await _notificationDispatchLock.WaitAsync();
        try
        {
            _isDispatchingDataChanged.Value = true;
            while (true)
            {
                PendingDataChange? next;
                bool suppress;
                lock (_notificationQueueLock)
                {
                    next = _pendingDataChanges.Count > 0
                        ? _pendingDataChanges.Dequeue()
                        : null;
                    suppress = next is not null && _notificationsSuspended > 0;
                }

                if (next is null)
                {
                    break;
                }

                if (suppress)
                {
                    next.Completion.TrySetResult();
                    continue;
                }

                try
                {
                    InvokeDataChangedSafely(next.Data);
                    next.Completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unexpected DataChanged dispatch failure: {ex.Message}");
                    next.Completion.TrySetResult();
                }
            }
        }
        finally
        {
            _isDispatchingDataChanged.Value = false;
            _notificationDispatchLock.Release();
        }
    }

    private void InvokeDataChangedSafely(AppData data)
    {
        EventHandler<AppData>? handlers;
        lock (_dataChangedHandlerLock)
        {
            handlers = _dataChanged;
        }
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<AppData> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DataChanged subscriber failed: {ex.Message}");
            }
        }
    }

    private sealed class NotificationSuspension(ManagedAppDataService service) : IDisposable
    {
        private readonly ManagedAppDataService _service = service;
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                lock (_service._notificationQueueLock)
                {
                    _service._notificationsSuspended--;
                }
            }
        }
    }

    private sealed class PendingDataChange(AppData data)
    {
        public AppData Data { get; } = data;

        public TaskCompletionSource Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
