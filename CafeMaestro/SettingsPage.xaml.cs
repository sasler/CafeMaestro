using CafeMaestro.Services;
using System.Reflection;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage
{
    private readonly RoastDataService _roastDataService;
    private readonly PreferencesService _preferencesService;

    public SettingsPage()
    {
        InitializeComponent();

        // Get services from DI
        _roastDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ?? 
                            new RoastDataService();
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
            string path = _roastDataService.DataFilePath;
            
            // Check if using default path
            string savedPath = await _preferencesService.GetRoastDataFilePathAsync();
            
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
                
                // Update display
                await LoadCurrentDataFilePath();
                
                await DisplayAlert("Success", $"Created new data file: {Path.GetFileName(filePath)}", "OK");
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
            await _preferencesService.ClearRoastDataFilePathAsync();
            
            // Reset to default by creating a new service instance
            _roastDataService.SetCustomFilePath(Path.Combine(FileSystem.AppDataDirectory, "RoastData", "roast_log.json"));
            
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