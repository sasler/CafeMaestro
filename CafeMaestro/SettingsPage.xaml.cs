using CafeMaestro.Services;
using System.Reflection;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage
{
    private readonly RoastDataService _roastDataService;
    private readonly AppDataService _appDataService;
    private readonly PreferencesService _preferencesService;

    public SettingsPage()
    {
        InitializeComponent();

        // Get services from DI
        _appDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ?? 
                         new AppDataService();
        _roastDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ?? 
                           new RoastDataService(_appDataService);
        _preferencesService = Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ?? 
                             new PreferencesService();
        
        // Set version number from assembly
        VersionLabel.Text = GetVersionNumber();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCurrentDataFilePath();
    }

    private string GetVersionNumber()
    {
        // Get version from assembly
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        
        return version != null 
            ? $"{version.Major}.{version.Minor}.{version.Build}" 
            : "1.0.0";
    }

    private async Task LoadCurrentDataFilePath()
    {
        try
        {
            // Get current data file path
            string path = _appDataService.DataFilePath;
            
            // Check if using default path
            string savedPath = await _preferencesService.GetAppDataFilePathAsync();
            
            if (string.IsNullOrEmpty(savedPath))
            {
                DataFilePath.Text = $"{path} (Default)";
            }
            else
            {
                DataFilePath.Text = path;
            }
        }
        catch (Exception ex)
        {
            DataFilePath.Text = $"Error loading path: {ex.Message}";
        }
    }

    private async void SelectDataFileButton_Clicked(object sender, EventArgs e)
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
                PickerTitle = "Select Data File",
                FileTypes = customFileType
            };

            var result = await FilePicker.PickAsync(options);
            if (result != null)
            {
                // Set this as the data file
                _appDataService.SetCustomFilePath(result.FullPath);
                
                // Save preference
                await _preferencesService.SaveAppDataFilePathAsync(result.FullPath);
                
                // Update display
                await LoadCurrentDataFilePath();
                
                await DisplayAlert("Success", $"Now using data file: {Path.GetFileName(result.FullPath)}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select data file: {ex.Message}", "OK");
        }
    }
    
    private async void CreateDataFileButton_Clicked(object sender, EventArgs e)
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
                
                // Check if file exists
                if (File.Exists(filePath))
                {
                    bool replace = await DisplayAlert("File Exists", 
                        "The selected file already exists. Do you want to replace it? This will erase any existing data!", 
                        "Replace", "Cancel");
                        
                    if (!replace)
                        return;
                }
                
                // Create new empty data file
                bool success = await _appDataService.CreateEmptyDataFileAsync(filePath);
                
                if (success)
                {
                    // Save preference
                    await _preferencesService.SaveAppDataFilePathAsync(filePath);
                    
                    // Update display
                    await LoadCurrentDataFilePath();
                    
                    await DisplayAlert("Success", $"Created new data file: {Path.GetFileName(filePath)}", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to create data file", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create data file: {ex.Message}", "OK");
        }
    }
    
    private async void UseDefaultButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Clear saved preference
            await _preferencesService.ClearAppDataFilePathAsync();
            
            // Reset to default location
            _appDataService.SetCustomFilePath(Path.Combine(FileSystem.AppDataDirectory, "AppData", "cafemaestro_data.json"));
            
            // Update display
            await LoadCurrentDataFilePath();
            
            await DisplayAlert("Success", "Now using default data file location", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to use default location: {ex.Message}", "OK");
        }
    }
}