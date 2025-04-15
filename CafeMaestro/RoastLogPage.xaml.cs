using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class RoastLogPage : ContentPage
{
    private readonly RoastDataService _roastDataService;
    private readonly AppDataService _appDataService;
    private readonly PreferencesService _preferencesService;
    private ObservableCollection<RoastData> _roastLogs;
    public ICommand RefreshCommand { get; private set; }

    public RoastLogPage()
    {
        InitializeComponent();

        // Get the services from DI
        _appDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<AppDataService>() ?? 
                         new AppDataService();
        _roastDataService = Application.Current?.Handler?.MauiContext?.Services.GetService<RoastDataService>() ?? 
                           new RoastDataService(_appDataService);
        _preferencesService = Application.Current?.Handler?.MauiContext?.Services.GetService<PreferencesService>() ?? 
                             new PreferencesService();

        _roastLogs = new ObservableCollection<RoastData>();
        RoastLogCollection.ItemsSource = _roastLogs;

        RefreshCommand = new Command(async () => await LoadRoastData());
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRoastData();
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