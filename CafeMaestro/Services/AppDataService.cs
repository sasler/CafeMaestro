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
        private AppData _cachedData;
        private bool _isDirty = false;
        
        // Property to get/set the current data file path
        public string DataFilePath 
        { 
            get => _filePath;
            set
            {
                _filePath = value;
                _folderPath = Path.GetDirectoryName(_filePath);
                // Clear cache when path changes
                _cachedData = null;
            }
        }

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
        
        // Set a custom file path for data storage
        public void SetCustomFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
                
            DataFilePath = filePath;
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
        
        // Loads all app data
        public async Task<AppData> LoadAppDataAsync()
        {
            // Return cached data if available
            if (_cachedData != null && !_isDirty)
                return _cachedData;
                
            try
            {
                if (!File.Exists(_filePath))
                {
                    // Create and return empty data structure
                    _cachedData = CreateEmptyAppData();
                    return _cachedData;
                }
                    
                string jsonString = await File.ReadAllTextAsync(_filePath);
                var appData = JsonSerializer.Deserialize<AppData>(jsonString, _jsonOptions);
                
                if (appData == null)
                {
                    appData = CreateEmptyAppData();
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
        }
        
        // Save all app data
        public async Task<bool> SaveAppDataAsync(AppData appData)
        {
            try
            {
                // Update metadata
                appData.LastModified = DateTime.Now;
                appData.AppVersion = GetAppVersion();
                
                // Serialize and save
                string jsonString = JsonSerializer.Serialize(appData, _jsonOptions);
                await File.WriteAllTextAsync(_filePath, jsonString);
                
                // Update cache
                _cachedData = appData;
                _isDirty = false;
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving app data: {ex.Message}");
                return false;
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
        public async Task<bool> CreateEmptyDataFileAsync(string filePath)
        {
            try
            {
                // Create empty app data structure
                var emptyData = CreateEmptyAppData();
                
                // Set file path
                SetCustomFilePath(filePath);
                
                // Make sure directory exists
                string directory = Path.GetDirectoryName(filePath);
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
                    return false;
                }
                
                // Update cache
                _cachedData = emptyData;
                _isDirty = false;
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating empty data file: {ex.Message}");
                return false;
            }
        }
        
        // Migrates data from separate files to the combined format (if needed)
        public async Task<bool> MigrateDataIfNeededAsync()
        {
            try
            {
                // Skip if combined data file already exists
                if (DataFileExists())
                    return true;
                
                var appData = new AppData();
                
                // Try to load beans from old location
                string beanFilePath = Path.Combine(FileSystem.AppDataDirectory, "BeanData", "bean_inventory.json");
                if (File.Exists(beanFilePath))
                {
                    try
                    {
                        string beanJson = await File.ReadAllTextAsync(beanFilePath);
                        var beans = JsonSerializer.Deserialize<List<Bean>>(beanJson, _jsonOptions);
                        if (beans != null)
                        {
                            appData.Beans = beans;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error migrating beans: {ex.Message}");
                    }
                }
                
                // Try to load roast data from old location
                string roastFilePath = Path.Combine(FileSystem.AppDataDirectory, "RoastData", "roast_log.json");
                if (File.Exists(roastFilePath))
                {
                    try
                    {
                        string roastJson = await File.ReadAllTextAsync(roastFilePath);
                        var roastLogs = JsonSerializer.Deserialize<List<RoastData>>(roastJson, _jsonOptions);
                        if (roastLogs != null)
                        {
                            appData.RoastLogs = roastLogs;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error migrating roast logs: {ex.Message}");
                    }
                }
                
                // Save to new combined file
                return await SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during data migration: {ex.Message}");
                return false;
            }
        }
    }
}