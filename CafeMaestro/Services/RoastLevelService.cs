using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CafeMaestro.Models;

namespace CafeMaestro.Services
{
    public class RoastLevelService : IRoastLevelService, IDisposable
    {
        private readonly IAppDataService _appDataService;
        private bool _isDisposed;

        public string DataFilePath
        {
            get => _appDataService.DataFilePath;
        }

        public RoastLevelService(IAppDataService appDataService)
        {
            _appDataService = appDataService;

            // Subscribe to path changes from AppDataService
            _appDataService.DataFilePathChanged += OnDataFilePathChanged;
        }

        private void OnDataFilePathChanged(object? sender, string newPath)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Reload data with new path
                    await _appDataService.ReloadDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reloading data after path change in RoastLevelService: {ex.Message}");
                }
            });
        }

        // Get roast level data using weight loss percentage
        public async Task<string> GetRoastLevelNameAsync(double weightLossPercentage)
        {
            try
            {
                // Load all roast levels
                var roastLevels = await GetRoastLevelsAsync();

                return RoastLevelResolver.Resolve(roastLevels, weightLossPercentage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting roast level: {ex.Message}");
                return RoastLevelResolver.UnknownLevelName;
            }
        }

        // Get all roast levels
        public async Task<List<RoastLevelData>> GetRoastLevelsAsync()
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Check if roast levels are defined
                if (appData.RoastLevels == null || appData.RoastLevels.Count == 0)
                {
                    // Return default levels (this should not happen since we initialize them in AppDataService)
                    return new List<RoastLevelData>
                    {
                        new RoastLevelData("Light", 0.0, 12.0),
                        new RoastLevelData("Medium-Light", 12.0, 14.0),
                        new RoastLevelData("Medium", 14.0, 16.0),
                        new RoastLevelData("Medium-Dark", 16.0, 18.0),
                        new RoastLevelData("Dark", 18.0, 100.0)
                    };
                }

                // Return roast levels sorted by min weight loss percentage
                return appData.RoastLevels.OrderBy(l => l.MinWeightLossPercentage).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading roast levels: {ex.Message}");
                return new List<RoastLevelData>();
            }
        }

        // Save updated roast levels
        public async Task<bool> SaveRoastLevelsAsync(List<RoastLevelData> levels)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Update roast levels
                appData.RoastLevels = levels;

                // Save updated app data
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving roast levels: {ex.Message}");
                return false;
            }
        }

        // Add a new roast level
        public async Task<bool> AddRoastLevelAsync(RoastLevelData level)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Initialize if needed
                if (appData.RoastLevels == null)
                {
                    appData.RoastLevels = new List<RoastLevelData>();
                }

                // Add the level
                appData.RoastLevels.Add(level);

                // Save updated app data
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding roast level: {ex.Message}");
                return false;
            }
        }

        // Delete a roast level
        public async Task<bool> DeleteRoastLevelAsync(Guid id)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Find and remove the level
                var levelToRemove = appData.RoastLevels?.FirstOrDefault(l => l.Id == id);
                if (levelToRemove != null && appData.RoastLevels != null)
                {
                    appData.RoastLevels.Remove(levelToRemove);

                    // Save updated app data
                    return await _appDataService.SaveAppDataAsync(appData);
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting roast level: {ex.Message}");
                return false;
            }
        }

        // Update a roast level
        public async Task<bool> UpdateRoastLevelAsync(RoastLevelData updatedLevel)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Find the level to update
                var existingLevel = appData.RoastLevels?.FirstOrDefault(l => l.Id == updatedLevel.Id);
                if (existingLevel != null)
                {
                    // Update properties
                    existingLevel.Name = updatedLevel.Name;
                    existingLevel.MinWeightLossPercentage = updatedLevel.MinWeightLossPercentage;
                    existingLevel.MaxWeightLossPercentage = updatedLevel.MaxWeightLossPercentage;

                    // Save updated app data
                    return await _appDataService.SaveAppDataAsync(appData);
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating roast level: {ex.Message}");
                return false;
            }
        }

        // Get a specific roast level by ID
        public async Task<RoastLevelData?> GetRoastLevelByIdAsync(Guid id)
        {
            try
            {
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();

                // Find the roast level with the matching ID
                return appData.RoastLevels?.FirstOrDefault(l => l.Id == id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting roast level by ID: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _appDataService.DataFilePathChanged -= OnDataFilePathChanged;
        }
    }
}
