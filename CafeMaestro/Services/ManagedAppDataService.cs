using System.Text.Json;
using System.Text.Json.Serialization;
using CafeMaestro.Models;

namespace CafeMaestro.Services;

public sealed class ManagedAppDataService : IAppDataService
{
    private readonly string _canonicalFilePath;
    private readonly Func<string> _appVersionProvider;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _dataAccessLock = new(1, 1);
    private AppData? _cachedData;
    private bool _isInitialized;
    private int _notificationsSuspended;

    public ManagedAppDataService()
        : this(Path.Combine(FileSystem.AppDataDirectory, "cafemaestro_data.json"))
    {
    }

    public ManagedAppDataService(
        string canonicalFilePath,
        Func<string>? appVersionProvider = null)
    {
        _canonicalFilePath = string.IsNullOrWhiteSpace(canonicalFilePath)
            ? throw new ArgumentException("Canonical data path is required.", nameof(canonicalFilePath))
            : Path.GetFullPath(canonicalFilePath);
        _appVersionProvider = appVersionProvider ?? GetAppVersion;
    }

    public event EventHandler<AppData>? DataChanged;

    public event EventHandler<string>? DataFilePathChanged;

    public string DataFilePath => _canonicalFilePath;

    public AppData CurrentData => _cachedData ?? AppDataFactory.CreateDefault();

    public IDisposable SuspendNotifications()
    {
        Interlocked.Increment(ref _notificationsSuspended);
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
                return CurrentData;
            }

            AppData data;
            if (File.Exists(_canonicalFilePath))
            {
                data = await ReadAndValidateAsync(_canonicalFilePath);
            }
            else
            {
                string? legacyPath = await preferencesService.GetAppDataFilePathAsync();
                data = await TryReadLegacyDataAsync(legacyPath)
                    ?? AppDataFactory.CreateDefault();
                await WriteAtomicAsync(data);
            }

            _cachedData = data;
            _isInitialized = true;
            await preferencesService.SaveAppDataFilePathAsync(_canonicalFilePath);
            await preferencesService.SetFirstRunCompletedAsync();
            DataFilePathChanged?.Invoke(this, _canonicalFilePath);
            RaiseDataChanged(data);
            return data;
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

        try
        {
            string fullLegacyPath = Path.GetFullPath(legacyPath);
            if (string.Equals(fullLegacyPath, _canonicalFilePath, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(fullLegacyPath))
            {
                return null;
            }

            return await ReadAndValidateAsync(fullLegacyPath);
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
        if (!File.Exists(_canonicalFilePath))
        {
            return CurrentData;
        }

        await _dataAccessLock.WaitAsync();
        try
        {
            AppData data = await ReadAndValidateAsync(_canonicalFilePath);
            _cachedData = data;
            return data;
        }
        finally
        {
            _dataAccessLock.Release();
        }
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

    public async Task<AppData> ReloadDataAsync()
    {
        AppData data = await LoadAppDataAsync();
        RaiseDataChanged(data);
        return data;
    }

    private async Task<bool> SaveInternalAsync(AppData appData, bool fireEvents)
    {
        ArgumentNullException.ThrowIfNull(appData);
        Normalize(appData);
        if (GetValidationErrors(appData).Count > 0)
        {
            return false;
        }

        await _dataAccessLock.WaitAsync();
        try
        {
            appData.LastModified = DateTime.UtcNow;
            appData.AppVersion = _appVersionProvider();
            await WriteAtomicAsync(appData);
            _cachedData = appData;

            if (fireEvents)
            {
                RaiseDataChanged(appData);
            }

            return true;
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
    }

    private async Task WriteAtomicAsync(AppData data)
    {
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
                await JsonSerializer.SerializeAsync(stream, data, _jsonOptions);
                await stream.FlushAsync();
            }

            File.Move(temporaryPath, _canonicalFilePath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private async Task<AppData> ReadAndValidateAsync(string filePath)
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
            AppData data = await AppDataJsonReader.DeserializeAsync(stream, _jsonOptions);

            Normalize(data);
            List<string> errors = GetValidationErrors(data);
            if (errors.Count > 0)
            {
                throw new InvalidDataException($"The data file is invalid. {errors[0]}");
            }

            return data;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The data file is not valid CafeMaestro JSON.", ex);
        }
    }

    private static void Normalize(AppData data)
    {
        data.Beans ??= [];
        data.RoastLogs ??= [];
        data.RoastLevels ??= [];
        if (data.RoastLevels.Count == 0)
        {
            data.RoastLevels = AppDataFactory.CreateDefault().RoastLevels;
        }
    }

    private static List<string> GetValidationErrors(AppData data)
    {
        return
        [
            .. data.Beans.SelectMany((bean, index) =>
                bean.Validate().Select(error => $"Bean {index + 1}: {error}")),
            .. data.RoastLogs.SelectMany((roast, index) =>
                roast.Validate().Select(error => $"Roast {index + 1}: {error}")),
            .. data.RoastLevels.SelectMany((level, index) =>
                level.Validate().Select(error => $"Roast level {index + 1}: {error}"))
        ];
    }

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

        Version? assemblyVersion = typeof(ManagedAppDataService).Assembly.GetName().Version;
        return assemblyVersion is null
            ? "Unknown"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }

    private bool AreNotificationsSuspended =>
        Volatile.Read(ref _notificationsSuspended) > 0;

    private void RaiseDataChanged(AppData data)
    {
        if (!AreNotificationsSuspended)
        {
            DataChanged?.Invoke(this, data);
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
                Interlocked.Decrement(ref _service._notificationsSuspended);
            }
        }
    }
}
