using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CafeMaestro.Models;

namespace CafeMaestro.Services
{
    public class RoastLevelService
    {
        private readonly AppDataService _appDataService;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;
        private string _currentDataFilePath = string.Empty;

        public string DataFilePath
        {
            get => _appDataService.DataFilePath;
        }

        public RoastLevelService(AppDataService appDataService)
        {
            _appDataService = appDataService;
            _currentDataFilePath = _appDataService.DataFilePath;

            // Subscribe to path changes from AppDataService
            _appDataService.DataFilePathChanged += OnDataFilePathChanged;
        }

        private void OnDataFilePathChanged(object? sender, string newPath)
        {
            Task.Run(async () =>
            {
                try
                {
                    // Update stored path
                    _currentDataFilePath = newPath;

                    // Reset initialized flag to force reload with new path
                    _isInitialized = false;

                    // Reload data with new path
                    await _appDataService.ReloadDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reloading data after path change in RoastLevelService: {ex.Message}");
                }
            });
        }

        // Initialize from preferences - ensure this is called at startup
        public async Task InitializeFromPreferencesAsync(PreferencesService preferencesService)
        {
            await _initLock.WaitAsync();

            try
            {
                if (_isInitialized)
                {
                    return;
                }

                // Force a reload of data
                await _appDataService.ReloadDataAsync();


                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing RoastLevelService from preferences: {ex.Message}");
            }
            finally
            {
                _initLock.Release();
            }
        }

        // Get roast level data using weight loss percentage
        public async Task<string> GetRoastLevelNameAsync(double weightLossPercentage)
        {
            try
            {
                // Load all roast levels
                var roastLevels = await GetRoastLevelsAsync();

                // Find the appropriate level for the given weight loss percentage
                foreach (var level in roastLevels.OrderBy(l => l.MinWeightLossPercentage))
                {
                    if (weightLossPercentage >= level.MinWeightLossPercentage &&
                        weightLossPercentage < level.MaxWeightLossPercentage)
                    {
                        return level.Name;
                    }
                }

                // If no level matches, find the highest level
                var highestLevel = roastLevels.OrderByDescending(l => l.MaxWeightLossPercentage).FirstOrDefault();
                if (highestLevel != null)
                {
                    return highestLevel.Name;
                }

                // Fallback to a generic name if no levels defined
                return "Unknown";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting roast level: {ex.Message}");
                return "Unknown";
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
    }
}