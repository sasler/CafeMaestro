using System.Collections.ObjectModel;
using System.Windows.Input;
using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

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
    public ICommand EditRoastCommand { get; private set; }
    public ICommand DeleteRoastCommand { get; private set; }
    public ICommand ItemTappedCommand { get; private set; }

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
            Task.Run(async () =>
            {
                try
                {
                    // Get the path from preferences to ensure it's the user-defined one
                    if (_preferencesService != null)
                    {
                        string? savedPath = await _preferencesService.GetAppDataFilePathAsync();
                        if (!string.IsNullOrEmpty(savedPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"RoastLogPage - Setting path from preferences: {savedPath}");
                            await _appDataService.SetCustomFilePathAsync(savedPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting file path in RoastLogPage: {ex.Message}");
                }
            });
        }

        System.Diagnostics.Debug.WriteLine($"RoastLogPage constructor - Using AppDataService at path: {_appDataService.DataFilePath}");

        _roastLogs = new ObservableCollection<RoastData>();
        RoastLogCollection.ItemsSource = _roastLogs;

        RefreshCommand = new Command(async () => await LoadRoastData());
        
        // Edit command - navigate to RoastPage with the selected roast data
        EditRoastCommand = new Command<RoastData>(async (roastData) => {
            try {
                // We already have the roast data from the collection view, so we can use it directly
                System.Diagnostics.Debug.WriteLine($"Editing roast with ID: {roastData.Id}, Date: {roastData.RoastDate}");
                
                // Navigate to RoastPage with parameters - convert Guid to string
                var navigationParameter = new Dictionary<string, object>
                {
                    { "EditRoastId", roastData.Id.ToString() }
                };
                await Shell.Current.GoToAsync("//RoastPage", navigationParameter);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error preparing to edit roast: {ex.Message}");
                await DisplayAlert("Error", "Error preparing to edit roast", "OK");
            }
        });
        
        // Delete command - delete the selected roast data without confirmation
        DeleteRoastCommand = new Command<RoastData>(async (roastData) => {
            try {
                // Use RoastDataService's DeleteRoastLogAsync method
                bool success = await _roastDataService.DeleteRoastLogAsync(roastData.Id);

                if (success)
                {
                    // Refresh the list after deletion
                    await RefreshRoastLogs();
                }
                else
                {
                    await DisplayAlert("Error", "Failed to delete roast log", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting roast log: {ex.Message}");
                await DisplayAlert("Error", $"Failed to delete roast log: {ex.Message}", "OK");
            }
        });
        
        // Item tapped command - show action sheet for Windows users
        ItemTappedCommand = new Command<RoastData>(async (roastData) => {
            try {
                // Show action sheet with options
                string action = await DisplayActionSheet(
                    $"Roast from {roastData.RoastDate.ToString("MM/dd/yyyy")}", 
                    "Cancel",
                    null,
                    "Edit", 
                    "Delete");
                
                switch (action)
                {
                    case "Edit":
                        // Use the existing EditRoastCommand
                        EditRoastCommand.Execute(roastData);
                        break;
                    case "Delete":
                        // Use the existing DeleteRoastCommand
                        DeleteRoastCommand.Execute(roastData);
                        break;
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error handling item tap: {ex.Message}");
            }
        });

        // Subscribe to data changes
        _appDataService.DataChanged += OnAppDataChanged;

        // Subscribe to navigation events to refresh data when returning to this page
        this.Loaded += RoastLogPage_Loaded;
        this.NavigatedTo += RoastLogPage_NavigatedTo;
    }

    private void RoastLogPage_Loaded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("RoastLogPage_Loaded event triggered");
        // Refresh data when page is loaded
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ForceRefreshData();
        });
    }

    private void RoastLogPage_NavigatedTo(object? sender, NavigatedToEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("RoastLogPage_NavigatedTo event triggered");
        // Refresh data when navigated to this page
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ForceRefreshData();
        });
    }

    // Force a full refresh from the data store
    private async Task ForceRefreshData()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Force refreshing roast log data");

            // Force a reload from the data file
            await _appDataService.ReloadDataAsync();

            // Update the UI with fresh data
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateRoastLogsFromAppData(_appDataService.CurrentData);
                System.Diagnostics.Debug.WriteLine($"Force refreshed roast logs with {_roastLogs.Count} logs");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error force refreshing roast logs: {ex.Message}");
        }
    }

    private void UpdateRecordCount(int count)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RecordCountLabel.Text = $"{count}";
        });
    }

    private void OnAppDataChanged(object? sender, AppData appData)
    {
        // Reload the UI with the new data
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateRoastLogsFromAppData(appData);
            // Update the record count display
            UpdateRecordCount(appData.RoastLogs?.Count ?? 0);
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

        // Update the record count display
        UpdateRecordCount(_roastLogs.Count);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("RoastLogPage.OnAppearing");

        // Set RefreshView's command execution
        RoastLogRefreshView.Command = new Command(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("RoastLogRefreshView.Command executing");
                await RefreshRoastLogs();
            }
            finally
            {
                // Always ensure IsRefreshing is set to false when complete
                RoastLogRefreshView.IsRefreshing = false;
                System.Diagnostics.Debug.WriteLine("RoastLogRefreshView.Command completed");
            }
        });

        // Initial load of roast logs - awaited with _ to suppress warning while still allowing execution to continue
        _ = RefreshRoastLogs();
    }

    private async Task RefreshRoastLogs()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Refreshing roast logs from service...");

            // Get all roast logs from service
            var logs = await _roastDataService.GetAllRoastLogsAsync();

            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Clear existing roast logs
                _roastLogs.Clear();

                // Add logs in order (newest first)
                foreach (var log in logs.OrderByDescending(l => l.RoastDate))
                {
                    _roastLogs.Add(log);
                }

                // Update the record count
                UpdateRecordCount(logs.Count);

                System.Diagnostics.Debug.WriteLine($"Refreshed roast logs: {_roastLogs.Count} loaded");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing roast logs: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private async Task LoadRoastLogs()
    {
        try
        {
            RoastLogRefreshView.IsRefreshing = true;

            // Get all roast logs and update the UI
            var logs = await _roastDataService.GetAllRoastLogsAsync();

            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                System.Diagnostics.Debug.WriteLine($"Loaded {logs.Count} roast logs");

                // Clear and repopulate the collection
                _roastLogs.Clear();

                // Add each log in descending date order (newest first)
                foreach (var log in logs.OrderByDescending(l => l.RoastDate))
                {
                    _roastLogs.Add(log);
                }

                // Force refresh the CollectionView by resetting its ItemsSource
                RoastLogCollection.ItemsSource = null;
                RoastLogCollection.ItemsSource = _roastLogs;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading roast logs: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load roast logs: {ex.Message}", "OK");
        }
        finally
        {
            RoastLogRefreshView.IsRefreshing = false;
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

    private void DataFileButton_Clicked(object sender, EventArgs e)
    {
        // Removed functionality for DataFileButton
    }

    private void ExportButton_Clicked(object sender, EventArgs e)
    {
        // Removed functionality for ExportButton
    }

    private async void ImportButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Navigate to the roast import page
            System.Diagnostics.Debug.WriteLine("Navigating to RoastImportPage");
            await Navigation.PushAsync(new RoastImportPage());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error navigating to RoastImportPage: {ex.Message}");
            await DisplayAlert("Error", $"Could not navigate to import page: {ex.Message}", "OK");
        }
    }

    private async void AddRoast_Clicked(object sender, EventArgs e)
    {
        // Navigate to the RoastPage for adding a new roast
        await Shell.Current.GoToAsync("//RoastPage");
    }

    // We'll keep OnBackButtonPressed for hardware back button support
    protected override bool OnBackButtonPressed()
    {
        // Use the same logic as before but in a synchronous way
        try
        {
            // Navigate back to MainPage using direct Shell.CurrentItem assignment
            // This works better on Android than GoToAsync
            if (Shell.Current?.Items.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Shell.Current.CurrentItem = Shell.Current.Items[0]; // MainPage is the first item
                    System.Diagnostics.Debug.WriteLine("Navigated back to MainPage using hardware back button");
                });
                return true; // Indicate we've handled the back button
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling back button: {ex.Message}");
        }

        return base.OnBackButtonPressed(); // Let the system handle it if our code fails
    }
}