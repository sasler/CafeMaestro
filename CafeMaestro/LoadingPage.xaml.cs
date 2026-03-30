using System.Diagnostics;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class LoadingPage : ContentPage
{
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly AppShell _appShell;

    public LoadingPage(IAppDataService appDataService, IPreferencesService preferencesService, AppShell appShell)
    {
        InitializeComponent();
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _appShell = appShell ?? throw new ArgumentNullException(nameof(appShell));

        // Start the loading sequence after page appears
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Start the loading sequence
        Task.Run(async () => await LoadDataAndNavigateAsync());
    }

    private async Task LoadDataAndNavigateAsync()
    {
        try
        {
            // Update UI with progress
            await UpdateStatusAsync("Initializing services...");

            string? savedFilePath = null;
            bool isFirstRun = true;

            savedFilePath = await _preferencesService.GetAppDataFilePathAsync();
            isFirstRun = await _preferencesService.IsFirstRunAsync();

            if (!string.IsNullOrEmpty(savedFilePath) && File.Exists(savedFilePath))
            {
                // We have a valid file path and app data service, load the data
                await UpdateStatusAsync($"Loading data from {Path.GetFileName(savedFilePath)}...");

                // Set the file path and load data
                await _appDataService.SetCustomFilePathAsync(savedFilePath);
                var data = await _appDataService.LoadAppDataAsync();

                // Short delay for visual consistency
                await Task.Delay(500);

                // Navigate to the main shell
                await NavigateToAppShell(false);
            }
            else
            {
                // No valid file path or missing services, show first run experience
                await UpdateStatusAsync("Preparing first run experience...");

                // Short delay for visual consistency
                await Task.Delay(500);

                // Navigate to the app shell and trigger first run flow
                await NavigateToAppShell(true);
            }
        }
        catch (Exception ex)
        {
            // If anything goes wrong, log the error and still navigate to app shell
            Debug.WriteLine($"LoadingPage: Error loading data - {ex.Message}");
            await UpdateStatusAsync("Error loading data. Starting app anyway...");

            // Short delay to show error
            await Task.Delay(1000);

            // Navigate to the main shell as fallback
            await NavigateToAppShell(true);
        }
    }

    // Helper method to navigate to AppShell and optionally trigger first run
    private async Task NavigateToAppShell(bool setFirstRunNeeded)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var app = Application.Current as App;
            if (app != null && setFirstRunNeeded)
            {
                app.SetFirstRunNeeded(true);
            }

            if (Application.Current != null && Application.Current.Windows.Count > 0)
            {
                var window = Application.Current.Windows[0];
                if (window != null)
                {
                    window.Page = _appShell;
                }
            }
        });
    }

    private async Task UpdateStatusAsync(string message)
    {
        // Update the status label on the UI thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusLabel.Text = message;
        });
    }
}
