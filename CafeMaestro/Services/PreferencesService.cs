using System;
using System.Threading.Tasks;

namespace CafeMaestro.Services
{
    public class PreferencesService
    {
        // Keys for preferences
        private const string RoastDataFilePathKey = "RoastDataFilePath";
        
        // Store the roast data file path
        public async Task SaveRoastDataFilePathAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
                
            await SecureStorage.SetAsync(RoastDataFilePathKey, filePath);
        }
        
        // Get the stored roast data file path (returns null if not set)
        public async Task<string> GetRoastDataFilePathAsync()
        {
            try
            {
                return await SecureStorage.GetAsync(RoastDataFilePathKey);
            }
            catch (Exception)
            {
                // Handle issues with secure storage (like on devices that don't support it)
                return null;
            }
        }
        
        // Clear the stored path
        public async Task ClearRoastDataFilePathAsync()
        {
            try
            {
                SecureStorage.Remove(RoastDataFilePathKey);
                await Task.CompletedTask;
            }
            catch (Exception)
            {
                // Ignore errors when clearing
            }
        }
    }
}