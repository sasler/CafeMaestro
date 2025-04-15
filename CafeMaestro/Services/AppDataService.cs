using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Reflection;
using CafeMaestro.Models;

namespace CafeMaestro.Services
{
    public class AppDataService
    {
        private string _folderPath;
        private string _filePath;
        private readonly string _defaultFileName = "cafemaestro_data.json";
        private readonly JsonSerializerOptions _jsonOptions;
        private AppData? _cachedData;
        private bool _isDirty = false;
        private bool _isInitialized = false;
        private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _dataAccessLock = new SemaphoreSlim(1, 1);
        
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
                    
                    System.Diagnostics.Debug.WriteLine($"Data file path changed from '{oldPath}' to '{_filePath}'");
                    
                    // Raise event to notify other services of the path change
                    DataFilePathChanged?.Invoke(this, _filePath);
                }
            }
        }

        // Get the current cached data
        public AppData CurrentData => _cachedData ?? CreateEmptyAppData();

        public AppDataService()
        {
            // Default initialization using app data directory
            _folderPath = Path.Combine(FileSystem.AppDataDirectory, "AppData");
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(_folderPath))
                Directory.CreateDirectory(_folderPath);
                
            _filePath = Path.Combine(_folderPath, _defaultFileName);
            
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
                    System.Diagnostics.Debug.WriteLine("AppDataService already initialized, returning cached data");
                    return _cachedData ?? await LoadAppDataAsync();
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
                        
                        System.Diagnostics.Debug.WriteLine($"Initialized with saved data file: {savedFilePath}");
                    }
                    else
                    {
                        // File doesn't exist, fall back to default
                        System.Diagnostics.Debug.WriteLine($"Saved file not found: {savedFilePath}, using default");
                        ResetToDefaultPath(false); // Don't fire events during initialization
                        await preferencesService.ClearAppDataFilePathAsync();
                    }
                }
                
                // Load the data to cache it
                var data = await LoadAppDataAsync();
                
                _isInitialized = true;
                
                // Now fire the event to notify other services about the path
                DataFilePathChanged?.Invoke(this, _filePath);
                // Also notify about the loaded data
                DataChanged?.Invoke(this, data);
                
                System.Diagnostics.Debug.WriteLine($"AppDataService initialization complete, notified about path: {_filePath}");
                
                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing AppDataService: {ex.Message}");
                // Use default path on error
                ResetToDefaultPath(false); // Don't fire events during initialization
                
                // Create empty data
                var emptyData = CreateEmptyAppData();
                _cachedData = emptyData;
                
                // Fire the events
                DataFilePathChanged?.Invoke(this, _filePath);
                DataChanged?.Invoke(this, emptyData);
                
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
                
            DataFilePath = filePath;
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
                
            // Load data from the new path
            var data = await LoadAppDataAsync();
            
            // Notify about the loaded data
            DataChanged?.Invoke(this, data);
            
            return data;
        }

        // Reset to the default file path in app data directory
        public async Task<AppData> ResetToDefaultPathAsync()
        {
            // Reset to default location
            _folderPath = Path.Combine(FileSystem.AppDataDirectory, "AppData");
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(_folderPath))
                Directory.CreateDirectory(_folderPath);
                
            // Use property setter to fire path change event
            DataFilePath = Path.Combine(_folderPath, _defaultFileName);
            
            // Load data from default path
            var data = await LoadAppDataAsync();
            
            // Notify about the loaded data
            DataChanged?.Invoke(this, data);
            
            return data;
        }
        
        // Loads all app data - should only be called by the service or during page navigation
        public async Task<AppData> LoadAppDataAsync()
        {
            // Use a lock to prevent concurrent reads/writes
            await _dataAccessLock.WaitAsync();
            
            try
            {
                // Return cached data if available
                if (_cachedData != null && !_isDirty)
                    return _cachedData;
                    
                if (!File.Exists(_filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Data file not found: {_filePath}, creating empty data");
                    // Create and return empty data structure
                    _cachedData = CreateEmptyAppData();
                    return _cachedData;
                }
                
                string jsonString = await File.ReadAllTextAsync(_filePath);
                var appData = JsonSerializer.Deserialize<AppData>(jsonString, _jsonOptions);
                
                if (appData == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to deserialize data file: {_filePath}");
                    appData = CreateEmptyAppData();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded data from: {_filePath}");
                    System.Diagnostics.Debug.WriteLine($"  Beans: {appData.Beans?.Count ?? 0}, Roasts: {appData.RoastLogs?.Count ?? 0}");
                }
                
                _cachedData = appData;
                _isDirty = false;
                
                return appData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading app data: {ex.Message}");
                
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
            // Use a lock to prevent concurrent reads/writes
            await _dataAccessLock.WaitAsync();
            
            try
            {
                // Add caller tracking to trace where this method is being called from
                System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
                string caller = stackTrace.GetFrame(1)?.GetMethod()?.Name ?? "Unknown";
                string caller2 = stackTrace.GetFrame(2)?.GetMethod()?.Name ?? "Unknown";
                
                System.Diagnostics.Debug.WriteLine($"TRACE: SaveAppDataAsync called from {caller} -> {caller2} with {appData.RoastLogs?.Count ?? 0} roasts");
                
                // Check for duplicates in the data being saved
                bool hasDuplicates = false;
                if (appData.RoastLogs != null && appData.RoastLogs.Count > 0)
                {
                    var idSet = new HashSet<Guid>();
                    foreach (var roast in appData.RoastLogs)
                    {
                        if (!idSet.Add(roast.Id))
                        {
                            System.Diagnostics.Debug.WriteLine($"TRACE: DUPLICATE DETECTED - Roast ID {roast.Id} appears multiple times in data being saved!");
                            hasDuplicates = true;
                        }
                    }
                    
                    if (hasDuplicates)
                    {
                        System.Diagnostics.Debug.WriteLine($"TRACE: Duplicate IDs detected in data being saved. This may indicate a problem with the data!");
                    }
                }
                
                // Update metadata
                appData.LastModified = DateTime.Now;
                appData.AppVersion = GetAppVersion();
                
                // Serialize and save
                try
                {
                    string jsonString = JsonSerializer.Serialize(appData, _jsonOptions);
                    System.Diagnostics.Debug.WriteLine($"Serialized data successfully, length: {jsonString.Length}");
                    
                    // Verify file path and directory
                    string? directory = Path.GetDirectoryName(_filePath);
                    if (string.IsNullOrEmpty(directory))
                    {
                        System.Diagnostics.Debug.WriteLine($"Invalid directory path: '{directory}' for file: '{_filePath}'");
                        return false;
                    }
                    
                    // Ensure directory exists
                    if (!Directory.Exists(directory))
                    {
                        System.Diagnostics.Debug.WriteLine($"Creating directory: {directory}");
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Check file write access
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Attempting to write to file: {_filePath}");
                        await File.WriteAllTextAsync(_filePath, jsonString);
                        System.Diagnostics.Debug.WriteLine($"Successfully wrote to file: {_filePath}");
                    }
                    catch (Exception fileEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"File write error: {fileEx.Message}, {fileEx.GetType().Name}");
                        throw;
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
                
                System.Diagnostics.Debug.WriteLine($"Saved app data to: {_filePath}");
                System.Diagnostics.Debug.WriteLine($"  Beans: {appData.Beans?.Count ?? 0}, Roasts: {appData.RoastLogs?.Count ?? 0}");
                
                // Before notifying, check if we're going to cause recursion
                System.Diagnostics.Debug.WriteLine($"TRACE: About to trigger DataChanged event. Subscribers should not call SaveAppDataAsync!");
                
                // Notify about the updated data
                DataChanged?.Invoke(this, appData);
                
                System.Diagnostics.Debug.WriteLine($"TRACE: DataChanged event completed");
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving app data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");
                }
                
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
            // Use a lock to prevent concurrent reads/writes
            await _dataAccessLock.WaitAsync();
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"SPECIAL IMPORT MODE: Saving data WITHOUT triggering DataChanged event");
                
                // Update metadata
                appData.LastModified = DateTime.Now;
                appData.AppVersion = GetAppVersion();
                
                // Serialize and save
                try
                {
                    string jsonString = JsonSerializer.Serialize(appData, _jsonOptions);
                    System.Diagnostics.Debug.WriteLine($"Serialized data successfully, length: {jsonString.Length}");
                    
                    // Verify file path and directory
                    string? directory = Path.GetDirectoryName(_filePath);
                    if (string.IsNullOrEmpty(directory))
                    {
                        System.Diagnostics.Debug.WriteLine($"Invalid directory path: '{directory}' for file: '{_filePath}'");
                        return false;
                    }
                    
                    // Ensure directory exists
                    if (!Directory.Exists(directory))
                    {
                        System.Diagnostics.Debug.WriteLine($"Creating directory: {directory}");
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Write directly to the file
                    await File.WriteAllTextAsync(_filePath, jsonString);
                    System.Diagnostics.Debug.WriteLine($"Successfully wrote to file without notification: {_filePath}");
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"JSON serialization error: {jsonEx.Message}");
                    throw;
                }
                
                // Update cache
                _cachedData = appData;
                _isDirty = false;
                
                System.Diagnostics.Debug.WriteLine($"Saved app data WITHOUT notification to: {_filePath}");
                System.Diagnostics.Debug.WriteLine($"  Beans: {appData.Beans?.Count ?? 0}, Roasts: {appData.RoastLogs?.Count ?? 0}");
                
                // NO EVENT NOTIFICATION HERE - that's the key difference!
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving app data without notification: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");
                }
                
                return false;
            }
            finally
            {
                _dataAccessLock.Release();
            }
        }
        
        // Helper method to create an empty app data object
        private AppData CreateEmptyAppData()
        {
            return new AppData
            {
                Beans = new List<Bean>(),
                RoastLogs = new List<RoastData>(),
                LastModified = DateTime.Now,
                AppVersion = GetAppVersion()
            };
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
            return File.Exists(_filePath);
        }
        
        // Create new empty data file
        public async Task<AppData> CreateEmptyDataFileAsync(string filePath)
        {
            // Use a lock to prevent concurrent operations
            await _dataAccessLock.WaitAsync();
            
            try
            {
                // Create empty app data structure
                var emptyData = CreateEmptyAppData();
                
                // Set file path
                DataFilePath = filePath;
                
                // Make sure directory exists
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Create and save empty data to the file directly to ensure it works
                string jsonString = JsonSerializer.Serialize(emptyData, _jsonOptions);
                await File.WriteAllTextAsync(filePath, jsonString);
                
                // Verify file was created
                if (!File.Exists(filePath))
                {
                    throw new IOException($"Failed to create file at {filePath}");
                }
                
                // Update cache
                _cachedData = emptyData;
                _isDirty = false;
                
                System.Diagnostics.Debug.WriteLine($"Created new empty data file at: {filePath}");
                
                // Notify about the new data
                DataChanged?.Invoke(this, emptyData);
                
                return emptyData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating empty data file: {ex.Message}");
                throw;
            }
            finally
            {
                _dataAccessLock.Release();
            }
        }
        
        // Force reload of data from file - only use when absolutely necessary
        public async Task<AppData> ReloadDataAsync()
        {
            _isDirty = true;
            var data = await LoadAppDataAsync();
            
            // Notify about the reloaded data
            DataChanged?.Invoke(this, data);
            
            return data;
        }
        
        // This internal method is only used during initialization
        private void ResetToDefaultPath(bool fireEvents)
        {
            // Reset to default location
            _folderPath = Path.Combine(FileSystem.AppDataDirectory, "AppData");
            
            // Create the directory if it doesn't exist
            if (!Directory.Exists(_folderPath))
                Directory.CreateDirectory(_folderPath);
             
            if (fireEvents)
            {
                // Use property setter to fire events   
                DataFilePath = Path.Combine(_folderPath, _defaultFileName);
            }
            else
            {
                // Set without firing events
                _filePath = Path.Combine(_folderPath, _defaultFileName);
                _cachedData = null;
                _isDirty = true;
            }
        }
    }
}