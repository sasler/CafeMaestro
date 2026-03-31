namespace CafeMaestro.Services;

public interface IPreferencesService
{
    Task SaveAppDataFilePathAsync(string filePath);
    Task<string?> GetAppDataFilePathAsync();
    Task ClearAppDataFilePathAsync();
    Task<bool> IsFirstRunAsync();
    Task SetFirstRunCompletedAsync();
    Task ResetToFirstRunAsync();
    Task SaveThemePreferenceAsync(ThemePreference theme);
    Task<ThemePreference> GetThemePreferenceAsync();
    Task SaveRoastDataFilePathAsync(string filePath);
    Task<string> GetRoastDataFilePathAsync();
    Task ClearRoastDataFilePathAsync();
}
