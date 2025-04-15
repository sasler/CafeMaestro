using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

[QueryProperty(nameof(NavParams), "NavParams")]
public partial class RoastLogPage : ContentPage
{
    private NavigationParameters? _navParams; // Make nullable to fix constructor warning
    public NavigationParameters NavParams
    {
        get => _navParams ?? new NavigationParameters();
        set
        {
            _navParams = value;
            // When navigation parameters are set, update the roast logs list
            if (_navParams?.AppData != null)
            {
                UpdateRoastLogsFromAppData(_navParams.AppData);
            }
        }
    }
    
    private readonly RoastDataService _roastDataService;
    private readonly AppDataService _appDataService;
    private readonly PreferencesService _preferencesService;
    private ObservableCollection<RoastData> _roastLogs;
    public ICommand RefreshCommand { get; private set; }

    public RoastLogPage(RoastDataService? roastDataService = null, AppDataService? appDataService = null, PreferencesService? preferencesService = null)
    {
        InitializeComponent();

        // First try to get the services from the application resources (our stored service provider)
        if (Application.Current?.Resources != null && 
            Application.Current.Resources.TryGetValue("ServiceProvider", out var serviceProviderObj) == true && 
            serviceProviderObj is IServiceProvider serviceProvider)
        {
            _appDataService = appDataService ?? 
                             serviceProvider.GetService<AppDataService>() ??
                             Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                             throw new InvalidOperationException("AppDataService not available");
            
            _roastDataService = roastDataService ?? 
                               serviceProvider.GetService<RoastDataService>() ??
                               Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ??
                               throw new InvalidOperationException("RoastDataService not available");
                               
            _preferencesService = preferencesService ?? 
                                 serviceProvider.GetService<PreferencesService>() ??
                                 Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ??
                                 throw new InvalidOperationException("PreferencesService not available");
        }
        else
        {
            // Fall back to the old way if app resources doesn't have our provider
            _appDataService = appDataService ?? 
                            Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ??
                            throw new InvalidOperationException("AppDataService not available");
            
            _roastDataService = roastDataService ?? 
                              Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ??
                              throw new InvalidOperationException("RoastDataService not available");
                              
            _preferencesService = preferencesService ?? 
                                 Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ??
                                 throw new InvalidOperationException("PreferencesService not available");
        }

        // IMPORTANT: Ensure we're using the latest path from preferences
        if (Application.Current is App app)
        {
            // Get the data from the app directly instead of creating new services
            var appData = app.GetAppData();
            System.Diagnostics.Debug.WriteLine($"RoastLogPage constructor - Getting data directly from App: {_appDataService.DataFilePath}");
            
            // Force the AppDataService to use the same path as the main app
            Task.Run(async () => {
                try {
                    // Get the path from preferences to ensure it's the user-defined one
                    if (_preferencesService != null) {
                        string? savedPath = await _preferencesService.GetAppDataFilePathAsync();
                        if (!string.IsNullOrEmpty(savedPath)) {
                            System.Diagnostics.Debug.WriteLine($"RoastLogPage - Setting path from preferences: {savedPath}");
                            await _appDataService.SetCustomFilePathAsync(savedPath);
                        }
                    }
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Error setting file path in RoastLogPage: {ex.Message}");
                }
            });
        }

        System.Diagnostics.Debug.WriteLine($"RoastLogPage constructor - Using AppDataService at path: {_appDataService.DataFilePath}");

        _roastLogs = new ObservableCollection<RoastData>();
        RoastLogCollection.ItemsSource = _roastLogs;

        RefreshCommand = new Command(async () => await LoadRoastData());
        
        // Subscribe to data changes
        _appDataService.DataChanged += OnAppDataChanged;
    }
    
    private void OnAppDataChanged(object? sender, AppData appData)
    {
        // Reload the UI with the new data
        MainThread.BeginInvokeOnMainThread(() => {
            UpdateRoastLogsFromAppData(appData);
        });
    }
    
    private void UpdateRoastLogsFromAppData(AppData appData)
    {
        _roastLogs.Clear();
        
        // Sort by newest first
        foreach (var log in appData.RoastLogs.OrderByDescending(l => l.RoastDate))
        {
            _roastLogs.Add(log);
        }
        
        System.Diagnostics.Debug.WriteLine($"Updated RoastLogPage with {_roastLogs.Count} logs from AppData");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Get the data from our binding context if available
        if (BindingContext is NavigationParameters navParams)
        {
            UpdateRoastLogsFromAppData(navParams.AppData);
        }
        else
        {
            // Fallback to loading from the AppDataService if binding context not set
            await LoadRoastData();
        }
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private async Task LoadRoastData()
    {
        try
        {
            RoastLogRefreshView.IsRefreshing = true;

            // Use the current data from AppDataService directly
            var appData = _appDataService.CurrentData;
            UpdateRoastLogsFromAppData(appData);
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
            // Navigate to settings page where data file can be managed
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to navigate to settings: {ex.Message}", "OK");
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