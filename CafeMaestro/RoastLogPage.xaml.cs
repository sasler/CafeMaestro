using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class RoastLogPage : ContentPage
{
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
        }

        _preferencesService = preferencesService ?? 
                             Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ??
                             throw new InvalidOperationException("PreferencesService not available");

        System.Diagnostics.Debug.WriteLine($"RoastLogPage constructor - Using AppDataService at path: {_appDataService.DataFilePath}");

        _roastLogs = new ObservableCollection<RoastData>();
        RoastLogCollection.ItemsSource = _roastLogs;

        RefreshCommand = new Command(async () => await LoadRoastData());
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // First ensure the service is fully initialized
        try 
        {
            // Force reload data from the AppDataService to ensure we have the latest
            await _appDataService.ReloadDataAsync();
            
            // Then load the roast logs
            await LoadRoastData();
            
            System.Diagnostics.Debug.WriteLine($"RoastLogPage loaded roast logs successfully on appearance from: {_appDataService.DataFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in RoastLogPage.OnAppearing: {ex.Message}");
            await DisplayAlert("Error", "Failed to load roast log data. Please try again.", "OK");
        }
    }

    private async Task LoadRoastData()
    {
        try
        {
            RoastLogRefreshView.IsRefreshing = true;

            List<RoastData> logs = await _roastDataService.LoadRoastDataAsync();

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