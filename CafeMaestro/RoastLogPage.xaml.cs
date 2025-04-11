using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class RoastLogPage : ContentPage
{
    private readonly RoastDataService _roastDataService;
    private readonly PreferencesService _preferencesService;
    private ObservableCollection<RoastData> _roastLogs;
    public ICommand RefreshCommand { get; private set; }

    public RoastLogPage()
    {
        InitializeComponent();

        // Get the services from DI
        _roastDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ?? 
                            new RoastDataService();
        _preferencesService = Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ?? 
                             new PreferencesService();

        _roastLogs = new ObservableCollection<RoastData>();
        RoastLogCollection.ItemsSource = _roastLogs;

        RefreshCommand = new Command(async () => await LoadRoastData());
        BindingContext = this;

        // Load custom file path if available
        InitializeDataFilePath();
    }

    private async void InitializeDataFilePath()
    {
        try
        {
            // Check if user has a saved file path preference
            string savedFilePath = await _preferencesService.GetRoastDataFilePathAsync();
            
            if (!string.IsNullOrEmpty(savedFilePath))
            {
                // If file exists, use it
                if (File.Exists(savedFilePath))
                {
                    _roastDataService.SetCustomFilePath(savedFilePath);
                    await DisplayAlert("Data File Loaded", $"Using data file: {Path.GetFileName(savedFilePath)}", "OK");
                }
            }
            
            // Load data initially
            await LoadRoastData();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to initialize data file: {ex.Message}", "OK");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadRoastData();
    }

    private async Task LoadRoastData(string searchTerm = "")
    {
        try
        {
            RoastLogRefreshView.IsRefreshing = true;

            List<RoastData> logs;

            if (string.IsNullOrWhiteSpace(searchTerm))
                logs = await _roastDataService.LoadRoastDataAsync();
            else
                logs = await _roastDataService.SearchRoastDataAsync(searchTerm);

            _roastLogs.Clear();

            // Sort by newest first
            foreach (var log in logs.OrderByDescending(l => l.RoastDate))
            {
                _roastLogs.Add(log);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load roast logs: {ex.Message}", "OK");
        }
        finally
        {
            RoastLogRefreshView.IsRefreshing = false;
        }
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        await LoadRoastData(e.NewTextValue);
    }

    private async void OnRoastSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is RoastData selectedRoast)
        {
            // Clear selection
            RoastLogCollection.SelectedItem = null;

            // Show details
            string details = $"Bean: {selectedRoast.BeanType}\n" +
                             $"Date: {selectedRoast.RoastDate:MM/dd/yyyy HH:mm}\n" +
                             $"Temperature: {selectedRoast.Temperature}°C\n" +
                             $"Batch Weight: {selectedRoast.BatchWeight}g\n" +
                             $"Final Weight: {selectedRoast.FinalWeight}g\n" +
                             $"Weight Loss: {selectedRoast.WeightLossPercentage:F1}%\n" +
                             $"Roast Time: {selectedRoast.FormattedTime}\n" +
                             $"Roast Level: {selectedRoast.RoastLevel}";

            if (!string.IsNullOrWhiteSpace(selectedRoast.Notes))
            {
                details += $"\n\nNotes:\n{selectedRoast.Notes}";
            }

            await DisplayAlert("Roast Details", details, "Close");
        }
    }

    private async void DataFileButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            string action = await DisplayActionSheet(
                "Roast Data File", 
                "Cancel", 
                null, 
                "Select Data File", 
                "Create New Data File",
                "Use Default Location");
                
            switch (action)
            {
                case "Select Data File":
                    await SelectExistingDataFile();
                    break;
                    
                case "Create New Data File":
                    await CreateNewDataFile();
                    break;
                    
                case "Use Default Location":
                    await UseDefaultDataLocation();
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to manage data file: {ex.Message}", "OK");
        }
    }
    
    private async Task SelectExistingDataFile()
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.json" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Select Roast Data File",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                // Set this as the data file
                _roastDataService.SetCustomFilePath(result.FullPath);
                
                // Save preference
                await _preferencesService.SaveRoastDataFilePathAsync(result.FullPath);
                
                // Reload data
                await LoadRoastData();
                
                await DisplayAlert("Success", $"Now using data file: {Path.GetFileName(result.FullPath)}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select data file: {ex.Message}", "OK");
        }
    }
    
    private async Task CreateNewDataFile()
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.json" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Create New Data File (Choose Location)",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                string filePath = result.FullPath;
                
                // Ensure the file has the correct extension
                if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    filePath += ".json";
                }
                
                // Create empty JSON array file
                if (File.Exists(filePath))
                {
                    bool replace = await DisplayAlert("File Exists", 
                        "The selected file already exists. Do you want to replace it? This will erase any existing data!", 
                        "Replace", "Cancel");
                        
                    if (!replace)
                        return;
                }
                
                // Create new empty JSON array file
                File.WriteAllText(filePath, "[]");
                
                // Set this as the data file
                _roastDataService.SetCustomFilePath(filePath);
                
                // Save preference
                await _preferencesService.SaveRoastDataFilePathAsync(filePath);
                
                // Reload data (should be empty)
                await LoadRoastData();
                
                await DisplayAlert("Success", $"Created new data file: {Path.GetFileName(filePath)}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create data file: {ex.Message}", "OK");
        }
    }
    
    private async Task UseDefaultDataLocation()
    {
        try
        {
            // Clear saved preference
            await _preferencesService.ClearRoastDataFilePathAsync();
            
            // Reset to default by creating a new service instance
            _roastDataService.SetCustomFilePath(Path.Combine(FileSystem.AppDataDirectory, "RoastData", "roast_log.json"));
            
            // Reload data
            await LoadRoastData();
            
            await DisplayAlert("Success", "Now using default data file location", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to use default location: {ex.Message}", "OK");
        }
    }

    private async void ExportButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } },
                    { DevicePlatform.Android, new[] { "text/csv" } },
                    { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "Select where to save CSV file",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                await _roastDataService.ExportRoastLogAsync(result.FullPath);
                await DisplayAlert("Success", "Roast log exported successfully!", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to export data: {ex.Message}", "OK");
        }
    }

    private async void BackButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}