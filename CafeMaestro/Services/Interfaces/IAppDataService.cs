using CafeMaestro.Models;

namespace CafeMaestro.Services;

public interface IAppDataService
{
    event EventHandler<AppData>? DataChanged;
    event EventHandler<string>? DataFilePathChanged;
    string DataFilePath { get; }
    AppData CurrentData { get; }
    IDisposable SuspendNotifications();
    Task<AppData> InitializeAsync(IPreferencesService preferencesService);
    Task<AppData> SetCustomFilePathAsync(string filePath);
    Task<AppData> ResetToDefaultPathAsync();
    Task<AppData> LoadAppDataAsync();
    Task<bool> SaveAppDataAsync(AppData appData);
    Task<bool> SaveAppDataWithoutNotificationAsync(AppData appData);
    bool DataFileExists();
    Task<AppData> CreateEmptyDataFileAsync(string filePath);
    Task<AppData> ReloadDataAsync();
}
