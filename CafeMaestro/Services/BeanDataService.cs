using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CafeMaestro.Models;

namespace CafeMaestro.Services
{
    public class BeanDataService
    {
        private readonly AppDataService _appDataService;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;
        private string _currentDataFilePath;

        // Property to get the current data file path
        public string DataFilePath 
        { 
            get => _appDataService.DataFilePath;
        }

        public BeanDataService(AppDataService appDataService)
        {
            _appDataService = appDataService;
            _currentDataFilePath = _appDataService.DataFilePath;
            
            // Subscribe to path changes from AppDataService
            _appDataService.DataFilePathChanged += OnDataFilePathChanged;
        }
        
        // Handle data file path changes
        private void OnDataFilePathChanged(object? sender, string newPath)
        {
            System.Diagnostics.Debug.WriteLine($"BeanService noticed data file path change to: {newPath}");
            
            // Track current path to help detect changes
            _currentDataFilePath = newPath;
            
            // When the path changes, we should reload data immediately
            // But don't do it in the event handler to avoid deadlocks
            // Instead, queue it on a background thread
            Task.Run(async () => {
                try
                {
                    await _appDataService.ReloadDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reloading data after path change in BeanService: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine("BeanService already initialized, skipping");
                    return;
                }
                
                // Force a reload of data
                await _appDataService.ReloadDataAsync();
                _currentDataFilePath = _appDataService.DataFilePath;
                
                System.Diagnostics.Debug.WriteLine($"BeanService initialized with path: {DataFilePath}");
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing BeanService from preferences: {ex.Message}");
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<bool> SaveBeansAsync(List<BeanData> beans)
        {
            try
            {
                // First verify current path matches expected path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.SaveBeansAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Replace beans collection
                appData.Beans = beans;
                
                // Save updated app data
                return await _appDataService.SaveAppDataAsync(appData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving beans: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> AddBeanAsync(BeanData bean)
        {
            try
            {
                // Log before adding bean
                System.Diagnostics.Debug.WriteLine($"Adding bean: {bean.CoffeeName}, ID: {bean.Id}, Price: {bean.Price?.ToString() ?? "null"}");
                
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.AddBeanAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded app data with {appData.Beans.Count} beans");
                
                // Add the bean
                appData.Beans.Add(bean);
                System.Diagnostics.Debug.WriteLine($"Added bean to collection, new count: {appData.Beans.Count}");
                
                // Save updated app data
                bool saveResult = await _appDataService.SaveAppDataAsync(appData);
                System.Diagnostics.Debug.WriteLine($"Save result: {saveResult}");
                
                return saveResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding bean: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        
        public async Task<bool> UpdateBeanAsync(BeanData bean)
        {
            try
            {
                // Log before updating bean
                System.Diagnostics.Debug.WriteLine($"Updating bean: {bean.CoffeeName}, ID: {bean.Id}, Price: {bean.Price?.ToString() ?? "null"}");
                
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.UpdateBeanAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded app data with {appData.Beans.Count} beans for update");
                
                // Find the bean to update
                int index = appData.Beans.FindIndex(b => b.Id == bean.Id);
                System.Diagnostics.Debug.WriteLine($"Bean found at index: {index}");
                
                if (index >= 0)
                {
                    // Replace the old bean with the updated one
                    appData.Beans[index] = bean;
                    System.Diagnostics.Debug.WriteLine($"Replaced bean at index {index}");
                    
                    // Save updated app data
                    bool saveResult = await _appDataService.SaveAppDataAsync(appData);
                    System.Diagnostics.Debug.WriteLine($"Save result: {saveResult}");
                    
                    return saveResult;
                }
                
                // If the bean was not found, add it as a new bean instead of failing
                System.Diagnostics.Debug.WriteLine($"Bean with ID {bean.Id} not found in collection, adding it as new bean");
                appData.Beans.Add(bean);
                
                // Save updated app data with the new bean
                bool addResult = await _appDataService.SaveAppDataAsync(appData);
                System.Diagnostics.Debug.WriteLine($"Added bean since it wasn't found, save result: {addResult}");
                
                return addResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating bean: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        
        public async Task<bool> DeleteBeanAsync(Guid beanId)
        {
            try
            {
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.DeleteBeanAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Find and remove the bean
                int index = appData.Beans.FindIndex(b => b.Id == beanId);
                
                if (index >= 0)
                {
                    // Remove the bean
                    appData.Beans.RemoveAt(index);
                    
                    // Save updated app data
                    return await _appDataService.SaveAppDataAsync(appData);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting bean: {ex.Message}");
                return false;
            }
        }
        
        public async Task<BeanData?> GetBeanByIdAsync(Guid id)
        {
            var allBeans = await GetAllBeansAsync();
            return allBeans.FirstOrDefault(b => b.Id == id);
        }

        public async Task<bool> UpdateBeanQuantityAsync(Guid beanId, double usedQuantity)
        {
            try
            {
                // Get the bean
                var bean = await GetBeanByIdAsync(beanId);
                
                if (bean == null)
                    return false;
                
                // Calculate new remaining quantity
                double newQuantity = bean.RemainingQuantity - usedQuantity;
                
                // Ensure we don't go below zero
                bean.RemainingQuantity = Math.Max(0, newQuantity);
                
                // Update the bean
                return await UpdateBeanAsync(bean);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating bean quantity: {ex.Message}");
                return false;
            }
        }
        
        public async Task<List<BeanData>> GetAllBeansAsync()
        {
            try
            {
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.GetAllBeansAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data from current (correct) path
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Log loaded data
                System.Diagnostics.Debug.WriteLine($"BeanService loaded {appData.Beans?.Count ?? 0} beans from {_currentDataFilePath}");
                
                // Log details of each bean for debugging
                if (appData.Beans != null && appData.Beans.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("Beans in collection:");
                    foreach (var bean in appData.Beans)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Bean ID: {bean.Id}, Name: {bean.CoffeeName}, Country: {bean.Country}");
                    }
                }
                
                return appData.Beans ?? new List<BeanData>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading beans: {ex.Message}");
                return new List<BeanData>();
            }
        }
        
        public async Task<List<BeanData>> SearchBeansAsync(string searchTerm = "")
        {
            var beans = await GetAllBeansAsync();
            
            if (string.IsNullOrWhiteSpace(searchTerm))
                return beans;
                
            return beans.FindAll(b => 
                b.Country.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                b.CoffeeName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.Variety.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                b.Process.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }
        
        public async Task<List<BeanData>> GetAvailableBeansAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Getting available beans (RemainingQuantity > 0)...");
                
                // First verify current path matches what we expect
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"Path mismatch detected in BeanService.GetAvailableBeansAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load app data directly from file to ensure freshness
                var appData = await _appDataService.LoadAppDataAsync();
                
                // Check if beans collection is null or empty
                if (appData.Beans == null || appData.Beans.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No beans found in app data");
                    return new List<BeanData>();
                }
                
                // Get beans with remaining quantity
                var availableBeans = appData.Beans.Where(b => b.RemainingQuantity > 0).ToList();
                
                System.Diagnostics.Debug.WriteLine($"Found {availableBeans.Count} available beans out of {appData.Beans.Count} total beans");
                
                // Log details of available beans for debugging
                foreach (var bean in availableBeans)
                {
                    System.Diagnostics.Debug.WriteLine($"  Available bean: ID={bean.Id}, Name={bean.DisplayName}, Remaining={bean.RemainingQuantity}kg");
                }
                
                return availableBeans;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting available beans: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<BeanData>();
            }
        }

        // Make this method static since it doesn't access instance data
        public static async Task<List<string>> GetCsvHeadersAsync(string filePath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Reading CSV headers from: {filePath}");
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    throw new FileNotFoundException("CSV file not found", filePath);
                }
                
                // Make this truly async by using Task.Run for file I/O
                return await Task.Run(() => 
                {
                    // Read all lines to find the actual header line
                    var lines = File.ReadAllLines(filePath);
                    System.Diagnostics.Debug.WriteLine($"File contains {lines.Length} lines");
                    
                    // Skip any comment lines that might appear at the beginning
                    int headerLineIndex = 0;
                    while (headerLineIndex < lines.Length && 
                           (string.IsNullOrWhiteSpace(lines[headerLineIndex]) || 
                            lines[headerLineIndex].TrimStart().StartsWith("//")))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping comment/empty line: {lines[headerLineIndex]}");
                        headerLineIndex++;
                    }
                    
                    // Check if we found a valid header line
                    if (headerLineIndex >= lines.Length)
                    {
                        System.Diagnostics.Debug.WriteLine("No valid header line found in CSV");
                        return new List<string>();
                    }
                    
                    // Get the header line
                    string headerLine = lines[headerLineIndex];
                    System.Diagnostics.Debug.WriteLine($"Found header line: {headerLine}");
                    
                    // Split by comma and return headers
                    var headers = headerLine.Split(',').Select(h => h.Trim()).ToList();
                    System.Diagnostics.Debug.WriteLine($"Extracted {headers.Count} headers: {string.Join(", ", headers)}");
                    
                    return headers;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading CSV headers: {ex.Message}");
                throw;
            }
        }
        
        public async Task<List<Dictionary<string, string>>> ReadCsvContentAsync(string filePath, int maxRows = 100)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Reading CSV content from: {filePath}, maxRows: {maxRows}");
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    throw new FileNotFoundException("CSV file not found", filePath);
                }
                
                // Make this truly async by using Task.Run for file I/O
                return await Task.Run(() => 
                {
                    var result = new List<Dictionary<string, string>>();
                    string[] lines = File.ReadAllLines(filePath);
                    
                    System.Diagnostics.Debug.WriteLine($"File contains {lines.Length} lines");
                    
                    // Skip any comment lines that might appear at the beginning
                    int headerLineIndex = 0;
                    while (headerLineIndex < lines.Length && 
                           (string.IsNullOrWhiteSpace(lines[headerLineIndex]) || 
                            lines[headerLineIndex].TrimStart().StartsWith("//")))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping comment/empty line: {lines[headerLineIndex]}");
                        headerLineIndex++;
                    }
                    
                    // Check if we have enough lines for header + data
                    if (headerLineIndex >= lines.Length - 1)
                    {
                        System.Diagnostics.Debug.WriteLine("Not enough lines for headers + data");
                        return result;
                    }
                    
                    // Get headers from the correct line
                    string[] headers = lines[headerLineIndex].Split(',').Select(h => h.Trim()).ToArray();
                    System.Diagnostics.Debug.WriteLine($"Found {headers.Length} headers: {string.Join(", ", headers)}");
                    
                    // Process data rows (limit to maxRows)
                    int rowsProcessed = 0;
                    for (int i = headerLineIndex + 1; i < lines.Length && rowsProcessed < maxRows; i++)
                    {
                        string line = lines[i];
                        
                        // Skip empty lines
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipping empty line {i}");
                            continue;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Processing line {i}: {line}");
                        
                        // Split CSV line (handling quoted values)
                        List<string> values = SplitCsvLine(line);
                        System.Diagnostics.Debug.WriteLine($"Split into {values.Count} values (headers: {headers.Length})");
                        
                        // Create a dictionary for this row
                        var rowData = new Dictionary<string, string>();
                        
                        // Map values to headers
                        for (int j = 0; j < Math.Min(headers.Length, values.Count); j++)
                        {
                            rowData[headers[j]] = values[j];
                            System.Diagnostics.Debug.WriteLine($"  Mapped '{headers[j]}' = '{values[j]}'");
                        }
                        
                        result.Add(rowData);
                        rowsProcessed++;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Processed {result.Count} rows from CSV");
                    return result;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading CSV content: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
                throw;
            }
        }
        
        // Helper method to split CSV line properly handling quoted values
        private List<string> SplitCsvLine(string line)
        {
            System.Diagnostics.Debug.WriteLine($"Splitting CSV line: {line}");
            var result = new List<string>();
            bool inQuotes = false;
            var currentValue = new StringBuilder();
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    System.Diagnostics.Debug.WriteLine($"  Quote found at position {i}, inQuotes now: {inQuotes}");
                    // Also add the quote character to preserve it for proper cleaning later
                    currentValue.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    // End of field, add to result
                    string value = CleanCsvValue(currentValue.ToString());
                    result.Add(value);
                    System.Diagnostics.Debug.WriteLine($"  Field ended at position {i}, value: '{value}'");
                    currentValue.Clear();
                }
                else
                {
                    // Add character to current field
                    currentValue.Append(c);
                }
            }
            
            // Add the last value
            string lastValue = CleanCsvValue(currentValue.ToString());
            result.Add(lastValue);
            System.Diagnostics.Debug.WriteLine($"  Last field value: '{lastValue}'");
            
            System.Diagnostics.Debug.WriteLine($"Split CSV line into {result.Count} values: {string.Join(" | ", result)}");
            
            return result;
        }
        
        // Remove quotes and trim values
        private string CleanCsvValue(string value)
        {
            string original = value;
            value = value.Trim();
            
            // Remove surrounding quotes if present
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Substring(1, value.Length - 2);
            }
            
            // Replace any escaped quotes (two double quotes) with a single quote
            value = value.Replace("\"\"", "\"");
            
            System.Diagnostics.Debug.WriteLine($"Cleaned CSV value: '{original}' -> '{value}'");
            return value;
        }
        
        // Import beans from CSV file
        public async Task<(int Success, int Failed, List<string> Errors)> ImportBeansFromCsvAsync(
            string filePath, 
            Dictionary<string, string> columnMapping)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting beans import from CSV file: {filePath}");
                System.Diagnostics.Debug.WriteLine($"Column mappings for import: {string.Join(", ", columnMapping.Select(m => $"{m.Key}={m.Value}"))}");
                
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    throw new FileNotFoundException("CSV file not found", filePath);
                }
                
                // Read the CSV data
                var csvData = await ReadCsvContentAsync(filePath, int.MaxValue);
                System.Diagnostics.Debug.WriteLine($"Read {csvData.Count} rows from CSV");
                
                var errors = new List<string>();
                int successCount = 0;
                int failedCount = 0;
                
                // Load the app data once at the beginning
                var appData = await _appDataService.LoadAppDataAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded app data with {appData.Beans.Count} existing beans");
                
                // Use the beans from the loaded app data for duplicate checking
                // IMPORTANT FIX: Don't load beans separately, use the ones already in appData
                var existingBeans = appData.Beans;
                System.Diagnostics.Debug.WriteLine($"Using {existingBeans.Count} existing beans for duplicate checking");
                
                // Process each row
                foreach (var row in csvData)
                {
                    try
                    {
                        var bean = new BeanData
                        {
                            Id = Guid.NewGuid(),
                            PurchaseDate = DateTime.Now, // Default
                            Quantity = 1, // Default
                            RemainingQuantity = 1 // Default
                        };
                        
                        System.Diagnostics.Debug.WriteLine($"Processing row with data: {string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}"))}");
                        
                        // Apply values based on mapping
                        foreach (var mapping in columnMapping)
                        {
                            string beanProperty = mapping.Key;
                            string csvColumn = mapping.Value;
                            
                            if (string.IsNullOrEmpty(csvColumn) || !row.ContainsKey(csvColumn))
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping property {beanProperty}: Column '{csvColumn}' not found in row");
                                continue; // Skip if no mapping or column not found
                            }
                            
                            string csvValue = row[csvColumn];
                            System.Diagnostics.Debug.WriteLine($"Mapping {beanProperty} = {csvValue} (from column {csvColumn})");
                            
                            // Skip empty values
                            if (string.IsNullOrWhiteSpace(csvValue))
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping empty value for {beanProperty}");
                                continue;
                            }
                            
                            // Apply value based on property
                            switch (beanProperty)
                            {
                                case "CoffeeName":
                                    bean.CoffeeName = csvValue;
                                    System.Diagnostics.Debug.WriteLine($"Set CoffeeName = {bean.CoffeeName}");
                                    break;
                                case "Country":
                                    bean.Country = csvValue;
                                    System.Diagnostics.Debug.WriteLine($"Set Country = {bean.Country}");
                                    break;
                                case "Variety":
                                    bean.Variety = csvValue;
                                    System.Diagnostics.Debug.WriteLine($"Set Variety = {bean.Variety}");
                                    break;
                                case "Process":
                                    bean.Process = csvValue;
                                    System.Diagnostics.Debug.WriteLine($"Set Process = {bean.Process}");
                                    break;
                                case "Notes":
                                    bean.Notes = csvValue;
                                    System.Diagnostics.Debug.WriteLine($"Set Notes = {bean.Notes}");
                                    break;
                                case "Link":
                                    bean.Link = csvValue;
                                    System.Diagnostics.Debug.WriteLine($"Set Link = {bean.Link}");
                                    break;
                                case "PurchaseDate":
                                    try {
                                        // Try multiple date formats, including European format (dd/MM/yyyy)
                                        if (DateTime.TryParse(csvValue, out DateTime purchaseDate))
                                        {
                                            bean.PurchaseDate = purchaseDate;
                                            System.Diagnostics.Debug.WriteLine($"Set PurchaseDate = {bean.PurchaseDate} (from {csvValue})");
                                        }
                                        else if (DateTime.TryParseExact(
                                            csvValue, 
                                            new[] { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" }, 
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            System.Globalization.DateTimeStyles.None, 
                                            out purchaseDate))
                                        {
                                            bean.PurchaseDate = purchaseDate;
                                            System.Diagnostics.Debug.WriteLine($"Set PurchaseDate = {bean.PurchaseDate} (from {csvValue} using TryParseExact)");
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Failed to parse date: {csvValue}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error parsing date '{csvValue}': {ex.Message}");
                                    }
                                    break;
                                case "Quantity":
                                    if (double.TryParse(csvValue, out double quantity))
                                    {
                                        bean.Quantity = quantity;
                                        bean.RemainingQuantity = quantity; // Set remaining to full
                                        System.Diagnostics.Debug.WriteLine($"Set Quantity = {bean.Quantity} (from {csvValue})");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Failed to parse quantity: {csvValue}");
                                    }
                                    break;
                                case "Price":
                                    if (decimal.TryParse(csvValue, out decimal price))
                                    {
                                        bean.Price = price;
                                        System.Diagnostics.Debug.WriteLine($"Set Price = {bean.Price} (from {csvValue})");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Failed to parse price: {csvValue}");
                                    }
                                    break;
                            }
                        }
                        
                        // Set null or empty properties to sensible defaults
                        if (string.IsNullOrWhiteSpace(bean.CoffeeName))
                        {
                            System.Diagnostics.Debug.WriteLine("ERROR: Bean name is required");
                            throw new ArgumentException("Bean name is required");
                        }
                        
                        if (string.IsNullOrWhiteSpace(bean.Country))
                        {
                            System.Diagnostics.Debug.WriteLine("ERROR: Country is required");
                            throw new ArgumentException("Country is required");
                        }
                        
                        if (string.IsNullOrWhiteSpace(bean.Variety))
                        {
                            bean.Variety = string.Empty; // Default to empty string
                            System.Diagnostics.Debug.WriteLine("Set Variety to empty string (default)");
                        }
                        
                        if (string.IsNullOrWhiteSpace(bean.Process))
                        {
                            bean.Process = string.Empty; // Default to empty string
                            System.Diagnostics.Debug.WriteLine("Set Process to empty string (default)");
                        }
                        
                        if (bean.Quantity <= 0)
                        {
                            bean.Quantity = 1; // Default to 1kg if not specified
                            bean.RemainingQuantity = 1;
                            System.Diagnostics.Debug.WriteLine("Set Quantity to 1kg (default)");
                        }
                        
                        // Check for duplicates based on name, country and variety
                        bool isDuplicate = existingBeans.Any(b => 
                            b.CoffeeName.Equals(bean.CoffeeName, StringComparison.OrdinalIgnoreCase) &&
                            b.Country.Equals(bean.Country, StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrEmpty(bean.Variety) && string.IsNullOrEmpty(b.Variety) || 
                             !string.IsNullOrEmpty(bean.Variety) && !string.IsNullOrEmpty(b.Variety) && 
                             b.Variety.Equals(bean.Variety, StringComparison.OrdinalIgnoreCase)));
                        
                        if (isDuplicate)
                        {
                            System.Diagnostics.Debug.WriteLine($"ERROR: Duplicate bean detected - {bean.CoffeeName} from {bean.Country}");
                            throw new InvalidOperationException($"Bean '{bean.CoffeeName}' from {bean.Country} already exists in inventory");
                        }
                        
                        // Dump the complete bean for debugging
                        System.Diagnostics.Debug.WriteLine($"Adding bean: ID={bean.Id}, Name={bean.CoffeeName}, Country={bean.Country}, " +
                                                       $"Variety={bean.Variety}, Process={bean.Process}, Date={bean.PurchaseDate}, " +
                                                       $"Quantity={bean.Quantity}, Price={bean.Price?.ToString() ?? "null"}");
                        
                        // Add the bean to the app data directly
                        appData.Beans.Add(bean);
                        
                        // Track success in memory
                        successCount++;
                        System.Diagnostics.Debug.WriteLine($"Successfully processed bean: {bean.CoffeeName}");
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        string error = $"Row error: {ex.Message}";
                        errors.Add(error);
                        System.Diagnostics.Debug.WriteLine($"ERROR: {error}");
                        System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
                    }
                }
                
                // Now save all the beans at once using the special non-notifying method
                System.Diagnostics.Debug.WriteLine($"BULK SAVE: Saving all {appData.Beans.Count} beans with special non-notifying method");
                bool saveResult = await _appDataService.SaveAppDataWithoutNotificationAsync(appData);
                
                if (!saveResult)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Failed to save all beans, attempting recovery...");
                    // If bulk save failed, try loading data again and give up
                    var freshData = await _appDataService.LoadAppDataAsync();
                    System.Diagnostics.Debug.WriteLine($"Recovery loaded {freshData.Beans.Count} beans");
                }
                
                // Final verification
                var finalBeanCount = await GetBeanCountAsync();
                System.Diagnostics.Debug.WriteLine($"FINAL BEAN COUNT: {finalBeanCount} (expected: {existingBeans.Count})");
                
                System.Diagnostics.Debug.WriteLine($"Import completed: {successCount} succeeded, {failedCount} failed");
                return (successCount, failedCount, errors);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing beans from CSV: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception details: {ex.StackTrace}");
                throw;
            }
        }

        // Simple method to get just the count of beans
        private async Task<int> GetBeanCountAsync()
        {
            try
            {
                var appData = await _appDataService.LoadAppDataAsync();
                return appData.Beans?.Count ?? 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting bean count: {ex.Message}");
                return -1;
            }
        }

        // Add a special version of AddBeanAsync that avoids event recursion
        private async Task<bool> AddBeanDirectAsync(BeanData bean)
        {
            try
            {
                // Log before adding bean
                System.Diagnostics.Debug.WriteLine($"TRACE: Adding bean directly: {bean.CoffeeName}, ID: {bean.Id}");
                
                // First verify current path
                if (_currentDataFilePath != _appDataService.DataFilePath)
                {
                    System.Diagnostics.Debug.WriteLine($"TRACE: Path mismatch detected in AddBeanDirectAsync. Expected: {_currentDataFilePath}, Actual: {_appDataService.DataFilePath}");
                    // Synchronize the path
                    _currentDataFilePath = _appDataService.DataFilePath;
                }
                
                // Load full app data
                var appData = await _appDataService.LoadAppDataAsync();
                System.Diagnostics.Debug.WriteLine($"TRACE: Loaded app data with {appData.Beans.Count} beans");
                
                // Add the bean
                appData.Beans.Add(bean);
                System.Diagnostics.Debug.WriteLine($"TRACE: Added bean to collection, new count: {appData.Beans.Count}");
                
                // Save updated app data
                bool saveResult = await _appDataService.SaveAppDataAsync(appData);
                System.Diagnostics.Debug.WriteLine($"TRACE: Save result: {saveResult}");
                
                return saveResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TRACE ERROR: Error adding bean directly: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"TRACE ERROR: Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"TRACE ERROR: Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}