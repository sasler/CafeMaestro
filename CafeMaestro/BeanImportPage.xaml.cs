using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CafeMaestro;

public partial class BeanImportPage : ContentPage
{
    private readonly BeanService _beanService;
    private readonly AppDataService _appDataService;
    private string? _selectedFilePath;
    private List<string> _csvHeaders = new List<string>();
    private Dictionary<string, string> _columnMapping = new Dictionary<string, string>();
    private List<Dictionary<string, string>> _previewData = new List<Dictionary<string, string>>();

    public BeanImportPage(BeanService? beanService = null, AppDataService? appDataService = null)
    {
        InitializeComponent();

        // First try to get the services from the application resources (our stored service provider)
        if (Application.Current?.Resources != null && 
            Application.Current?.Resources.TryGetValue("ServiceProvider", out var serviceProviderObj) == true && 
            serviceProviderObj is IServiceProvider serviceProvider)
        {
            _appDataService = appDataService ?? 
                             serviceProvider.GetService<AppDataService>() ??
                             Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                             throw new InvalidOperationException("AppDataService not available");
            
            _beanService = beanService ?? 
                          serviceProvider.GetService<BeanService>() ??
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ??
                          throw new InvalidOperationException("BeanService not available");
        }
        else
        {
            // Fall back to the old way if app resources doesn't have our provider
            _appDataService = appDataService ?? 
                            Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                            throw new InvalidOperationException("AppDataService not available");
            
            _beanService = beanService ?? 
                          Application.Current?.Handler?.MauiContext?.Services.GetService<BeanService>() ??
                          throw new InvalidOperationException("BeanService not available");
        }

        Debug.WriteLine($"BeanImportPage constructor - Using AppDataService at path: {_appDataService.DataFilePath}");

        // Initialize pickers
        SetupPickers();

        // Attach event handlers for pickers
        CoffeeNamePicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
        CountryPicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
        VarietyPicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
        ProcessPicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
        PurchaseDatePicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
        QuantityPicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
        PricePicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
        NotesPicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
        LinkPicker.SelectedIndexChanged += ColumnPicker_SelectedIndexChanged;
    }

    private void SetupPickers()
    {
        // Add an empty option to all pickers
        var emptyList = new List<string> { "-- None --" };
        
        CoffeeNamePicker.ItemsSource = new List<string>(emptyList);
        CountryPicker.ItemsSource = new List<string>(emptyList);
        VarietyPicker.ItemsSource = new List<string>(emptyList);
        ProcessPicker.ItemsSource = new List<string>(emptyList);
        PurchaseDatePicker.ItemsSource = new List<string>(emptyList);
        QuantityPicker.ItemsSource = new List<string>(emptyList);
        PricePicker.ItemsSource = new List<string>(emptyList);
        NotesPicker.ItemsSource = new List<string>(emptyList);
        LinkPicker.ItemsSource = new List<string>(emptyList);
    }

    private void UpdatePickersWithHeaders()
    {
        // Add empty option first
        var options = new List<string> { "-- None --" };
        
        // Add all headers
        options.AddRange(_csvHeaders);
        
        // Update all pickers with the same options
        CoffeeNamePicker.ItemsSource = options;
        CountryPicker.ItemsSource = options;
        VarietyPicker.ItemsSource = options;
        ProcessPicker.ItemsSource = options;
        PurchaseDatePicker.ItemsSource = options;
        QuantityPicker.ItemsSource = options;
        PricePicker.ItemsSource = options;
        NotesPicker.ItemsSource = options;
        LinkPicker.ItemsSource = options;
        
        // Try to automatically match columns based on names
        TryAutoMatchColumns();
    }

    private void TryAutoMatchColumns()
    {
        // Reset mapping
        _columnMapping.Clear();
        
        // Define keywords for matching
        var mappings = new Dictionary<string, List<string>>
        {
            { "CoffeeName", new List<string> { "coffee", "name", "bean" } },
            { "Country", new List<string> { "country", "origin", "region" } },
            { "Variety", new List<string> { "variety", "variaty", "varietal", "cultivar" } },
            { "Process", new List<string> { "process", "method", "processing" } },
            { "PurchaseDate", new List<string> { "date", "purchase", "acquired" } },
            { "Quantity", new List<string> { "quantity", "amount", "weight", "kg", "order", "oreder" } },
            { "Price", new List<string> { "price", "cost", "$" } },
            { "Notes", new List<string> { "note", "notes", "description", "flavor", "profile" } },
            { "Link", new List<string> { "link", "url", "website", "web" } }
        };
        
        // For each CSV header, check if it matches any of our properties
        foreach (var header in _csvHeaders)
        {
            foreach (var mapping in mappings)
            {
                string property = mapping.Key;
                List<string> keywords = mapping.Value;
                
                // Check if the header contains any of the keywords
                if (keywords.Any(keyword => header.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    // Add to mapping
                    _columnMapping[property] = header;
                    break;
                }
            }
        }
        
        // Special case handling for common CSV formats
        // If we have "Coffee" column but no CoffeeName mapping, use it
        if (!_columnMapping.ContainsKey("CoffeeName") && 
            _csvHeaders.Any(h => h.Equals("Coffee", StringComparison.OrdinalIgnoreCase)))
        {
            _columnMapping["CoffeeName"] = _csvHeaders.First(h => h.Equals("Coffee", StringComparison.OrdinalIgnoreCase));
        }

        // Handle the specific misspelled columns in your CSV
        if (!_columnMapping.ContainsKey("Variety") && 
            _csvHeaders.Any(h => h.Equals("Variaty", StringComparison.OrdinalIgnoreCase)))
        {
            _columnMapping["Variety"] = "Variaty";
        }

        if (!_columnMapping.ContainsKey("Quantity") && 
            _csvHeaders.Any(h => h.Contains("Oreder", StringComparison.OrdinalIgnoreCase)))
        {
            _columnMapping["Quantity"] = _csvHeaders.First(h => h.Contains("Oreder", StringComparison.OrdinalIgnoreCase));
        }
        
        // Add debug logging to see what mappings were created
        System.Diagnostics.Debug.WriteLine("Column mappings created:");
        foreach (var mapping in _columnMapping)
        {
            System.Diagnostics.Debug.WriteLine($"  Bean property '{mapping.Key}' -> CSV column '{mapping.Value}'");
        }
        
        // Apply mappings to pickers
        SetPickerSelectionFromMapping();
    }

    private void SetPickerSelectionFromMapping()
    {
        // Helper function to set picker based on mapping
        void SetPickerValue(Picker picker, string property)
        {
            if (_columnMapping.TryGetValue(property, out var columnName) && columnName != null)
            {
                int index = ((List<string>)picker.ItemsSource).IndexOf(columnName);
                if (index >= 0)
                {
                    picker.SelectedIndex = index;
                }
            }
        }
        
        // Set all pickers
        SetPickerValue(CoffeeNamePicker, "CoffeeName");
        SetPickerValue(CountryPicker, "Country");
        SetPickerValue(VarietyPicker, "Variety");
        SetPickerValue(ProcessPicker, "Process");
        SetPickerValue(PurchaseDatePicker, "PurchaseDate");
        SetPickerValue(QuantityPicker, "Quantity");
        SetPickerValue(PricePicker, "Price");
        SetPickerValue(NotesPicker, "Notes");
        SetPickerValue(LinkPicker, "Link");
    }

    private void ColumnPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            // Skip if "None" is selected
            if (picker.SelectedIndex == 0)
            {
                return;
            }
            
            string selectedColumnName = (string)picker.SelectedItem;
            string propertyName = "";
            
            // Determine which property this picker represents
            if (picker == CoffeeNamePicker) propertyName = "CoffeeName";
            else if (picker == CountryPicker) propertyName = "Country";
            else if (picker == VarietyPicker) propertyName = "Variety";
            else if (picker == ProcessPicker) propertyName = "Process";
            else if (picker == PurchaseDatePicker) propertyName = "PurchaseDate";
            else if (picker == QuantityPicker) propertyName = "Quantity";
            else if (picker == PricePicker) propertyName = "Price";
            else if (picker == NotesPicker) propertyName = "Notes";
            else if (picker == LinkPicker) propertyName = "Link";
            
            // Update mapping
            _columnMapping[propertyName] = selectedColumnName;
            
            // Update preview
            _ = UpdatePreview();
        }
    }

    private async Task UpdatePreview()
    {
        try
        {
            PreviewSection.IsVisible = true;
            
            // Check if required mappings are present
            bool hasRequiredMappings = _columnMapping.ContainsKey("CoffeeName") && _columnMapping.ContainsKey("Country");
            
            if (!hasRequiredMappings)
            {
                PreviewStatusLabel.Text = "Please map both Coffee Name and Country to continue.";
                ImportButton.IsEnabled = false;
                return;
            }
            
            // Count rows in preview
            if (_previewData.Count == 0 && !string.IsNullOrEmpty(_selectedFilePath))
            {
                // Load preview data if not already loaded
                _previewData = await _beanService.ReadCsvContentAsync(_selectedFilePath, 5);
            }
            
            int totalRows = _previewData.Count;
            
            if (totalRows == 0)
            {
                PreviewStatusLabel.Text = "No data rows found in CSV file.";
                ImportButton.IsEnabled = false;
                return;
            }
            
            // Count how many rows have coffee name and country
            int validRows = 0;
            foreach (var row in _previewData)
            {
                string coffeeName = GetMappedValue(row, "CoffeeName");
                string country = GetMappedValue(row, "Country");
                
                if (!string.IsNullOrWhiteSpace(coffeeName) && !string.IsNullOrWhiteSpace(country))
                {
                    validRows++;
                }
            }
            
            // Show preview statistics
            PreviewStatusLabel.Text = $"Found {validRows} valid beans out of {totalRows} rows in the preview.\n" +
                              $"The first bean will be '{GetMappedValue(_previewData[0], "CoffeeName")}' from {GetMappedValue(_previewData[0], "Country")}.";
            
            // Enable import button if we have valid data
            ImportButton.IsEnabled = validRows > 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating preview: {ex.Message}");
            PreviewStatusLabel.Text = $"Error previewing data: {ex.Message}";
            ImportButton.IsEnabled = false;
        }
    }
    
    // Helper to get value from a row using the mapping
    private string GetMappedValue(Dictionary<string, string> row, string propertyName)
    {
        if (_columnMapping.TryGetValue(propertyName, out var columnName) && 
            columnName != null &&
            row.TryGetValue(columnName, out var value))
        {
            return value ?? string.Empty;
        }
        
        return string.Empty;
    }

    private async void BrowseButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            // Configure file picker options
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                    { DevicePlatform.Android, new[] { "text/csv", "text/comma-separated-values", "application/csv", "*/*" } },
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Select CSV file with bean data",
                FileTypes = customFileType
            };

            // Show file picker
            var result = await FilePicker.Default.PickAsync(options);
            
            if (result != null)
            {
                // Reset UI state
                _csvHeaders.Clear();
                _columnMapping.Clear();
                _previewData.Clear();
                MapFieldsSection.IsVisible = false;
                PreviewSection.IsVisible = false;
                ImportButton.IsEnabled = false;
                
                // Show loading indicator
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
                
                try
                {
                    // Save selected file path
                    _selectedFilePath = result.FullPath;
                    FilePathEntry.Text = _selectedFilePath;
                    
                    // Get CSV headers - use the static method
                    _csvHeaders = await BeanService.GetCsvHeadersAsync(_selectedFilePath);
                    
                    if (_csvHeaders.Count == 0)
                    {
                        FileStatusLabel.Text = "Error: No headers found in CSV file";
                        return;
                    }
                    
                    // Load preview data
                    _previewData = await _beanService.ReadCsvContentAsync(_selectedFilePath, 5);
                    
                    // Update pickers with headers
                    UpdatePickersWithHeaders();
                    
                    // Show mapping section
                    MapFieldsSection.IsVisible = true;
                    
                    // Update preview section
                    await UpdatePreview();
                    
                    FileStatusLabel.Text = $"Found {_csvHeaders.Count} columns and {_previewData.Count} rows in preview.";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading CSV file: {ex.Message}");
                    FileStatusLabel.Text = $"Error: {ex.Message}";
                }
                finally
                {
                    // Hide loading indicator
                    LoadingIndicator.IsVisible = false;
                    LoadingIndicator.IsRunning = false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error selecting file: {ex.Message}");
            await DisplayAlert("Error", $"Failed to select file: {ex.Message}", "OK");
        }
    }

    private async void ImportButton_Clicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath))
        {
            await DisplayAlert("Error", "Please select a CSV file first", "OK");
            return;
        }
        
        try
        {
            // Show loading indicator
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            ImportButton.IsEnabled = false;

            // Log what we're trying to import
            System.Diagnostics.Debug.WriteLine("======= STARTING BEAN IMPORT =======");
            System.Diagnostics.Debug.WriteLine($"Selected file: {_selectedFilePath}");
            System.Diagnostics.Debug.WriteLine($"Column mappings: {string.Join(", ", _columnMapping.Select(m => $"{m.Key}={m.Value}"))}");
            
            // Check path exists
            if (!File.Exists(_selectedFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: File not found: {_selectedFilePath}");
                await DisplayAlert("Error", $"File not found: {_selectedFilePath}", "OK");
                return;
            }
            
            // ADDITIONAL FIX: Get existing beans FIRST to avoid potential duplication
            var existingBeans = await _beanService.GetAllBeansAsync();
            int originalBeanCount = existingBeans.Count;
            System.Diagnostics.Debug.WriteLine($"INITIAL BEAN COUNT BEFORE IMPORT: {originalBeanCount}");
            
            // Verify if the existing beans already contain duplicates
            var distinctBeanIds = new HashSet<Guid>();
            bool existingDuplicates = false;
            
            foreach (var bean in existingBeans)
            {
                if (!distinctBeanIds.Add(bean.Id))
                {
                    existingDuplicates = true;
                    System.Diagnostics.Debug.WriteLine($"DUPLICATE BEAN DETECTED IN EXISTING DATA - ID: {bean.Id}, Name: {bean.CoffeeName}");
                }
            }
            
            if (existingDuplicates)
            {
                bool shouldContinue = await DisplayAlert(
                    "Existing Duplicates", 
                    "Your database already contains duplicate beans. Would you like to remove them before importing?", 
                    "Yes, clean up duplicates", "No, continue anyway");
                    
                if (shouldContinue)
                {
                    // Remove duplicates before proceeding
                    System.Diagnostics.Debug.WriteLine("Removing existing duplicates before import...");
                    
                    // Create a deduplicated list based on unique IDs
                    var deduplicatedBeans = new List<Bean>();
                    var processedIds = new HashSet<Guid>();
                    
                    foreach (var bean in existingBeans)
                    {
                        if (processedIds.Add(bean.Id))
                        {
                            deduplicatedBeans.Add(bean);
                        }
                    }
                    
                    // Save the deduplicated beans
                    bool saveResult = await _beanService.SaveBeansAsync(deduplicatedBeans);
                    
                    if (!saveResult)
                    {
                        await DisplayAlert("Error", "Failed to remove duplicates from the database.", "OK");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Removed {existingBeans.Count - deduplicatedBeans.Count} duplicate beans. New count: {deduplicatedBeans.Count}");
                    existingBeans = deduplicatedBeans;
                }
            }

            // Perform import
            System.Diagnostics.Debug.WriteLine("Calling BeanService.ImportBeansFromCsvAsync...");
            var result = await _beanService.ImportBeansFromCsvAsync(_selectedFilePath, _columnMapping);
            
            System.Diagnostics.Debug.WriteLine($"Import result: Success={result.Success}, Failed={result.Failed}, Errors={result.Errors.Count}");
            foreach (var error in result.Errors)
            {
                System.Diagnostics.Debug.WriteLine($"Import error: {error}");
            }
            
            // ADDITIONAL FIX: Verify the beans weren't duplicated during import
            var afterImportBeans = await _beanService.GetAllBeansAsync();
            int afterImportCount = afterImportBeans.Count;
            
            System.Diagnostics.Debug.WriteLine($"BEAN COUNT AFTER IMPORT: {afterImportCount}");
            System.Diagnostics.Debug.WriteLine($"EXPECTED COUNT: {originalBeanCount + result.Success}");
            
            // Check for new duplicates
            var afterImportIds = new HashSet<Guid>();
            bool newDuplicatesFound = false;
            
            foreach (var bean in afterImportBeans)
            {
                if (!afterImportIds.Add(bean.Id))
                {
                    newDuplicatesFound = true;
                    System.Diagnostics.Debug.WriteLine($"NEW DUPLICATE FOUND AFTER IMPORT - ID: {bean.Id}, Name: {bean.CoffeeName}");
                }
            }
            
            if (newDuplicatesFound)
            {
                System.Diagnostics.Debug.WriteLine("CRITICAL: Duplicates were created during this import operation!");
                bool shouldFix = await DisplayAlert(
                    "Duplication Detected", 
                    "Beans were duplicated during import. Would you like to automatically fix this?", 
                    "Yes, remove duplicates", "No, keep as is");
                    
                if (shouldFix)
                {
                    // Fix the duplicates
                    System.Diagnostics.Debug.WriteLine("Removing duplicates created during import...");
                    
                    // Create a deduplicated list
                    var finalDedupedBeans = new List<Bean>();
                    var finalProcessedIds = new HashSet<Guid>();
                    
                    foreach (var bean in afterImportBeans)
                    {
                        if (finalProcessedIds.Add(bean.Id))
                        {
                            finalDedupedBeans.Add(bean);
                        }
                    }
                    
                    // Save the deduplicated list
                    bool fixResult = await _beanService.SaveBeansAsync(finalDedupedBeans);
                    
                    if (fixResult)
                    {
                        System.Diagnostics.Debug.WriteLine($"Successfully removed {afterImportBeans.Count - finalDedupedBeans.Count} duplicates");
                        afterImportCount = finalDedupedBeans.Count;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to remove duplicates!");
                    }
                }
            }
            
            // Show result
            string message = $"Import complete!\n\n" +
                           $"Beans successfully imported: {result.Success}\n" +
                           $"Failed imports: {result.Failed}\n" +
                           $"Final bean count: {afterImportCount}";
            
            if (result.Errors.Count > 0)
            {
                message += "\n\nErrors:";
                foreach (var error in result.Errors.Take(5)) // Show first 5 errors
                {
                    message += $"\n- {error}";
                }
                
                if (result.Errors.Count > 5)
                {
                    message += $"\n...and {result.Errors.Count - 5} more errors.";
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Showing result dialog: {message}");
            await DisplayAlert("Import Complete", message, "OK");
            
            // Return to bean inventory page
            System.Diagnostics.Debug.WriteLine("Navigating back to Bean Inventory page");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR importing beans: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            await DisplayAlert("Error", $"Failed to import beans: {ex.Message}", "OK");
        }
        finally
        {
            // Hide loading indicator
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            ImportButton.IsEnabled = true;
            System.Diagnostics.Debug.WriteLine("======= BEAN IMPORT COMPLETED =======");
        }
    }

    private async void CancelButton_Clicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
    
    // Override OnBackButtonPressed to handle Android back button
    protected override bool OnBackButtonPressed()
    {
        // Use the same logic as CancelButton_Clicked but in a synchronous way
        MainThread.BeginInvokeOnMainThread(() => {
            Navigation.PopAsync();
            System.Diagnostics.Debug.WriteLine("Navigated back using hardware back button in BeanImportPage");
        });
        return true; // Indicate we've handled the back button
    }
}