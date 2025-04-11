using System;
using System.Threading.Tasks;

namespace CafeMaestro.Services
{
    public class PreferencesService
    {
        // Keys for preferences
        private const string AppDataFilePathKey = "AppDataFilePath";
        
        // Store the app data file path
        public async Task SaveAppDataFilePathAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
                
            await SecureStorage.SetAsync(AppDataFilePathKey, filePath);
        }
        
        // Get the stored app data file path (returns null if not set)
        public async Task<string> GetAppDataFilePathAsync()
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
        
        // Legacy compatibility methods
        public async Task SaveRoastDataFilePathAsync(string filePath)
        {
            await SaveAppDataFilePathAsync(filePath);
        }
        
        public async Task<string> GetRoastDataFilePathAsync()
        {
            return await GetAppDataFilePathAsync();
        }
        
        public async Task ClearRoastDataFilePathAsync()
        {
            await ClearAppDataFilePathAsync();
        }
    }
}