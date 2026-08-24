using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace CafeMaestro.Services
{
    // Custom theme enum to avoid conflicts with Microsoft.Maui.ApplicationModel.AppTheme
    public enum ThemePreference
    {
        System,
        Light,
        Dark
    }

    public class PreferencesService : IPreferencesService
    {        // Keys for preferences
        private const string AppDataFilePathKey = "AppDataFilePath";
        private const string FirstRunKey = "IsFirstRun";
        private const string ThemePreferenceKey = "AppTheme"; // Storage key unchanged for backward compatibility
        
        // Store the app data file path
        public async Task SaveAppDataFilePathAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
                
            await SecureStorage.SetAsync(AppDataFilePathKey, filePath);
        }
        
        // Get the stored app data file path (returns null if not set)
        public async Task<string?> GetAppDataFilePathAsync()
        {
            try
            {
                return await SecureStorage.GetAsync(AppDataFilePathKey);
            }
            catch (Exception)
            {
                // Handle issues with secure storage (like on devices that don't support it)
                return null;
            }
        }
        
        // Clear the stored path
        public async Task ClearAppDataFilePathAsync()
        {
            try
            {
                SecureStorage.Remove(AppDataFilePathKey);
                await Task.CompletedTask;
            }
            catch (Exception)
            {
                // Ignore errors when clearing
            }
        }
        
        // Check if this is the first run of the app
        public async Task<bool> IsFirstRunAsync()
        {
            try
            {
                string? value = await SecureStorage.GetAsync(FirstRunKey);
                // If the key doesn't exist or is explicitly set to "true", then it's a first run
                return string.IsNullOrEmpty(value) || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                // If there's an error reading, assume it's a first run
                return true;
            }
        }
        
        // Mark the first run as completed
        public async Task SetFirstRunCompletedAsync()
        {
            try
            {
                await SecureStorage.SetAsync(FirstRunKey, "false");
            }
            catch (Exception)
            {
                // Ignore any errors when setting the value
            }
        }
        
        // Reset to first run state (for testing purposes)
        public async Task ResetToFirstRunAsync()
        {
            try
            {
                await SecureStorage.SetAsync(FirstRunKey, "true");
            }
            catch (Exception)
            {
                // Ignore any errors
            }
        }

    // Theme management
          // Save the user's theme preference
        public async Task SaveThemePreferenceAsync(ThemePreference theme)
        {
            try
            {
                await SecureStorage.SetAsync(ThemePreferenceKey, theme.ToString());
            }
            catch (Exception)
            {
                // Ignore any errors when setting the value
            }
        }
          // Get the user's theme preference (dark unless an explicit choice was stored)
        public async Task<ThemePreference> GetThemePreferenceAsync()
        {
            try
            {
                string? value = await SecureStorage.GetAsync(ThemePreferenceKey);
                return ThemePreferencePolicy.FromStoredValue(value);
            }
            catch (Exception)
            {
                // Secure storage is unavailable - treat it as "no preference stored".
                return ThemePreferencePolicy.Fallback;
            }
        }
        
        // Legacy compatibility methods
        public async Task SaveRoastDataFilePathAsync(string filePath)
        {
            await SaveAppDataFilePathAsync(filePath);
        }
        
        public async Task<string> GetRoastDataFilePathAsync()
        {
            return await GetAppDataFilePathAsync() ?? string.Empty;
        }
        
        public async Task ClearRoastDataFilePathAsync()
        {
            await ClearAppDataFilePathAsync();
        }
    }
}