using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface IAppDataService
{
    /// <summary>
    /// Raised synchronously after committed changes. Async-void subscribers are not supported.
    /// </summary>
    event EventHandler<AppData>? DataChanged;
    event EventHandler<string>? DataFilePathChanged;
    string DataFilePath { get; }
    AppData CurrentData { get; }
    /// <summary>True when the canonical file failed the validated load boundary.</summary>
    bool IsRecoveryRequired { get; }
    IDisposable SuspendNotifications();
    Task<AppData> InitializeAsync(IPreferencesService preferencesService);
    Task<AppData> LoadAppDataAsync();
    Task<bool> SaveAppDataAsync(AppData appData);
    Task<bool> SaveAppDataWithoutNotificationAsync(AppData appData);
    Task<bool> UpdateAsync(
        Action<AppData> mutation,
        CancellationToken cancellationToken = default);
    Task<bool> TryUpdateAsync(
        Func<AppData, bool> mutation,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Deliberately replaces data after a failed canonical load, preserving the raw source first.
    /// </summary>
    Task<AppData> ReplaceAppDataForRecoveryAsync(
        AppData appData,
        CancellationToken cancellationToken = default);
    bool DataFileExists();
    Task<AppData> ReloadDataAsync();
}
