using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CafeMaestro.Models;

namespace CafeMaestro.Services
{
    public class AppDataService
    {
        private string _folderPath = string.Empty;
        private string _filePath = string.Empty;
        private readonly JsonSerializerOptions _jsonOptions;
        private AppData? _cachedData;
        private bool _isDirty = false;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _dataAccessLock = new SemaphoreSlim(1, 1);
        private bool _pathInitializedFromPreferences = false;

        // Event that fires when the data file path changes or data is reloaded
        public event EventHandler<AppData>? DataChanged;
        public event EventHandler<string>? DataFilePathChanged;

        // Property to get the current data file path
        public string DataFilePath
        {
            get => _filePath;
            private set
            {
                if (_filePath != value)
                {
                    string oldPath = _filePath;
                    _filePath = value;
                    string? directory = Path.GetDirectoryName(_filePath);
                    _folderPath = directory ?? string.Empty;

                    // Clear cache when path changes
                    _cachedData = null;
                    _isDirty = true;

                    // Raise event to notify other services of the path change
                    DataFilePathChanged?.Invoke(this, _filePath);
                }
            }
        }

        // Get the current cached data
        public AppData CurrentData => _cachedData ?? CreateEmptyAppData();

        public AppDataService()
        {
            // Initialize with empty string path - no default path
            _folderPath = string.Empty;
            _filePath = string.Empty;

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        // Initialize the service with the saved path - call this only once during startup
        public async Task<AppData> InitializeAsync(PreferencesService preferencesService)
        {
            // Use a lock to prevent multiple simultaneous initializations
            await _initializationLock.WaitAsync();

            try
            {
                if (_isInitialized)
                {
                    return _cachedData ?? CreateEmptyAppData();
                }

                // Get saved file path from preferences
                string? savedFilePath = await preferencesService.GetAppDataFilePathAsync();

                if (!string.IsNullOrEmpty(savedFilePath))
                {
                    // If the saved file exists, use it
                    if (File.Exists(savedFilePath))
                    {
                        // Set the path directly to avoid duplicate event firing
                        _filePath = savedFilePath;
                        string? directory = Path.GetDirectoryName(_filePath);
                        _folderPath = directory ?? string.Empty;
                        _cachedData = null;
                        _isDirty = true;
                        _pathInitializedFromPreferences = true;

                        // Load data from the saved path
                        var data = await LoadAppDataInternalAsync(_filePath);

                        _isInitialized = true;

                        // Now fire the event to notify other services about the path
                        DataFilePathChanged?.Invoke(this, _filePath);
                        // Also notify about the loaded data
                        DataChanged?.Invoke(this, data);

                        return data;
                    }
                    else
                    {
                        // File doesn't exist, do not fall back to a default
                        await preferencesService.ClearAppDataFilePathAsync();
                    }
                }

                // No valid path, return empty data but don't set a path
                _isInitialized = true;

                // Create empty data for UI initialization
                var emptyData = CreateEmptyAppData();
                _cachedData = emptyData;

                return emptyData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing AppDataService: {ex.Message}");

                // Create empty data
                var emptyData = CreateEmptyAppData();
                _cachedData = emptyData;
                _isInitialized = true;

                return emptyData;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        // Set a custom file path for data storage - Call this when a user selects a new file
        public async Task<AppData> SetCustomFilePathAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            _pathInitializedFromPreferences = true;

            // Set path through property to trigger events
            DataFilePath = filePath;

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Load data from the new path
            var data = await LoadAppDataInternalAsync(filePath);

            // Notify about the loaded data
            DataChanged?.Invoke(this, data);

            return data;
        }

        // Reset to the default file path in app data directory - Kept for backward compatibility
        // Now creates a file in the user's Documents folder instead of AppData
        public async Task<AppData> ResetToDefaultPathAsync()
        {
            // Instead of using app data directory, use Documents folder for better visibility
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultFolder = Path.Combine(documentsPath, "CafeMaestro");
            string defaultFilePath = Path.Combine(defaultFolder, "cafemaestro_data.json");

            // Create the directory if it doesn't exist
            if (!Directory.Exists(defaultFolder))
            {
                Directory.CreateDirectory(defaultFolder);
            }

            _pathInitializedFromPreferences = true;

            // Use property setter to fire path change event
            DataFilePath = defaultFilePath;

            // Check if file exists
            bool fileExists = File.Exists(defaultFilePath);

            // If file doesn't exist, create it
            if (!fileExists)
            {
                var emptyData = CreateEmptyAppData();
                string jsonString = JsonSerializer.Serialize(emptyData, _jsonOptions);
                await File.WriteAllTextAsync(defaultFilePath, jsonString);
            }

            // Load data from default path
            var data = await LoadAppDataInternalAsync(defaultFilePath);

            // Notify about the loaded data
            DataChanged?.Invoke(this, data);

            return data;
        }

        // Loads all app data - should only be called by the service or during page navigation
        public async Task<AppData> LoadAppDataAsync()
        {
            // Check if we have a valid path
            if (string.IsNullOrEmpty(_filePath))
            {
                return CreateEmptyAppData();
            }

            // If we have a path, load from it
            return await LoadAppDataInternalAsync(_filePath);
        }

        // Internal method that actually loads the data from a specific path
        private async Task<AppData> LoadAppDataInternalAsync(string filePath)
        {
            // If no path is provided, return empty data
            if (string.IsNullOrEmpty(filePath))
            {
                return CreateEmptyAppData();
            }

            // Use a lock to prevent concurrent reads/writes
            await _dataAccessLock.WaitAsync();

            try
            {
                // Return cached data if available and not dirty
                if (_cachedData != null && !_isDirty && filePath == _filePath)
                {
                    return _cachedData;
                }

                if (!File.Exists(filePath))
                {
                    // Create and return empty data structure
                    _cachedData = CreateEmptyAppData();
                    _isDirty = false;
                    return _cachedData;
                }

                // CRITICAL FIX FOR ANDROID: Use File.OpenRead instead of File.ReadAllTextAsync
                string jsonString;
                try
                {
                    // Use streams instead of direct file reads for better Android compatibility
                    using (var fileStream = File.OpenRead(filePath))
                    using (var reader = new StreamReader(fileStream))
                    {
                        jsonString = await reader.ReadToEndAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading file with stream, falling back to direct read: {ex.Message}");
                    // Fall back to direct read if stream fails
                    jsonString = await File.ReadAllTextAsync(filePath);
                }

                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    _cachedData = CreateEmptyAppData();
                    return _cachedData;
                }

                AppData? appData;
                try
                {
                    appData = JsonSerializer.Deserialize<AppData>(jsonString, _jsonOptions);
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON deserialization failed: {ex.Message}, creating empty data");
                    _cachedData = CreateEmptyAppData();
                    return _cachedData;
                }

                if (appData == null)
                {
                    appData = CreateEmptyAppData();
                }
                else
                {
                    // Initialize collections if they're null
                    if (appData.Beans == null)
                    {
                        appData.Beans = new List<BeanData>();
                    }

                    if (appData.RoastLogs == null)
                    {
                        appData.RoastLogs = new List<RoastData>();
                    }

                    // Initialize roast levels if they're null (for backward compatibility)
                    if (appData.RoastLevels == null || appData.RoastLevels.Count == 0)
                    {
                        // Create default roast levels
                        appData.RoastLevels = new List<RoastLevelData>
                        {
                            new RoastLevelData("Under Developed", 0.0, 11.0),
                            new RoastLevelData("Light", 11.0, 13.0),
                            new RoastLevelData("Medium-Light", 13.0, 14.0),
                            new RoastLevelData("Medium", 14.0, 16.0),
                            new RoastLevelData("Dark", 16.0, 18.0),
                            new RoastLevelData("Extra Dark", 18.0, 22.0),
                            new RoastLevelData("Burned", 22.0, 100.0)
                        };
                    }
                }

                _cachedData = appData;
                _isDirty = false;

                return appData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading app data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Return empty data on error
                _cachedData = CreateEmptyAppData();
                return _cachedData;
            }
            finally
            {
                _dataAccessLock.Release();
            }
        }

        // Save all app data
        public async Task<bool> SaveAppDataAsync(AppData appData)
        {
            // Check if we have a path to save to
            if (string.IsNullOrEmpty(_filePath))
            {
                return false;
            }

            // Always save to the current path (user-selected path)
            return await SaveAppDataInternalAsync(appData, _filePath, true);
        }

        // Internal method that actually saves the data
        private async Task<bool> SaveAppDataInternalAsync(AppData appData, string filePath, bool fireEvents)
        {
            // Check if we have a valid path
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            // Use a lock to prevent concurrent reads/writes
            await _dataAccessLock.WaitAsync();

            try
            {
                // Add caller tracking to trace where this method is being called from
                System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
                string caller = stackTrace.GetFrame(1)?.GetMethod()?.Name ?? "Unknown";
                string caller2 = stackTrace.GetFrame(2)?.GetMethod()?.Name ?? "Unknown";

                // Update metadata
                appData.LastModified = DateTime.Now;
                appData.AppVersion = GetAppVersion();

                // Serialize and save
                try
                {
                    string jsonString = JsonSerializer.Serialize(appData, _jsonOptions);

                    // Verify file path and directory
                    string? directory = Path.GetDirectoryName(filePath);
                    if (string.IsNullOrEmpty(directory))
                    {
                        return false;
                    }

                    // Ensure directory exists
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // CRITICAL FIX FOR ANDROID: Use streams for file writing
                    try
                    {
                        // Use file stream with FileMode.Create for better compatibility
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var writer = new StreamWriter(fileStream))
                        {
                            await writer.WriteAsync(jsonString);
                            await writer.FlushAsync();
                        }
                    }
                    catch
                    {
                        // Fall back to direct write if stream fails
                        await File.WriteAllTextAsync(filePath, jsonString);
                    }
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON serialization error: {jsonEx.Message}");
                    throw;
                }

                // Update cache
                _cachedData = appData;
                _isDirty = false;

                // Only fire events if requested (for normal operations, not for imports)
                if (fireEvents)
                {
                    // Notify about the updated data
                    DataChanged?.Invoke(this, appData);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving app data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return false;
            }
            finally
            {
                _dataAccessLock.Release();
            }
        }

        // Special method for bulk imports that completely bypasses event notification
        // This is used specifically for importing data without triggering duplicate saves
        public async Task<bool> SaveAppDataWithoutNotificationAsync(AppData appData)
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                return false;
            }

            return await SaveAppDataInternalAsync(appData, _filePath, false);
        }

        // Helper method to create an empty app data object
        internal AppData CreateEmptyAppData()
        {
            var appData = new AppData
            {
                LastModified = DateTime.Now,
                AppVersion = GetAppVersion(),
                Beans = new List<BeanData>(),
                RoastLogs = new List<RoastData>(),
                RoastLevels = new List<RoastLevelData>
                {
                    new RoastLevelData("Light", 0.0, 12.0),
                    new RoastLevelData("Medium-Light", 12.0, 14.0),
                    new RoastLevelData("Medium", 14.0, 16.0),
                    new RoastLevelData("Medium-Dark", 16.0, 18.0),
                    new RoastLevelData("Dark", 18.0, 100.0)
                }
            };

            return appData;
        }

        // Get current app version
        private string GetAppVersion()
        {
            // Get version from assembly
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;

            return version != null
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : "1.0.0";
        }

        // Check if data file exists
        public bool DataFileExists()
        {
            if (string.IsNullOrEmpty(_filePath))
                return false;

            return File.Exists(_filePath);
        }

        // Create new empty data file
        public async Task<AppData> CreateEmptyDataFileAsync(string filePath)
        {
            try
            {
                // Create empty app data structure
                var emptyData = CreateEmptyAppData();

                // Write it to file
                await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(emptyData, _jsonOptions));

                // Save the path in preferences
                _filePath = filePath;

                // Emit path changed event
                DataFilePathChanged?.Invoke(this, _filePath);

                // Load the newly created data
                var result = await LoadAppDataAsync();

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create empty data file: {ex.Message}");
                throw;
            }
        }

        // Force reload of data from file - only use when absolutely necessary
        public async Task<AppData> ReloadDataAsync()
        {
            // Check if we have a valid path
            if (string.IsNullOrEmpty(_filePath))
            {
                return CreateEmptyAppData();
            }

            _isDirty = true;
            var data = await LoadAppDataInternalAsync(_filePath);

            // Notify about the reloaded data
            DataChanged?.Invoke(this, data);

            return data;
        }
    }
}