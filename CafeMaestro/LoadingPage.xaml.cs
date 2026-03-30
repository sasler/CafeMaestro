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

        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        Task.Run(async () => await LoadDataAndNavigateAsync());
    }

    private async Task LoadDataAndNavigateAsync()
    {
        try
        {
            await UpdateStatusAsync("Initializing services...");

            string? savedFilePath = null;
            bool isFirstRun = true;

            savedFilePath = await _preferencesService.GetAppDataFilePathAsync();
            isFirstRun = await _preferencesService.IsFirstRunAsync();

            if (!string.IsNullOrEmpty(savedFilePath) && File.Exists(savedFilePath))
            {
                await UpdateStatusAsync($"Loading data from {Path.GetFileName(savedFilePath)}...");

                await _appDataService.SetCustomFilePathAsync(savedFilePath);
                await _appDataService.LoadAppDataAsync();

                await Task.Delay(500);

                await NavigateToAppShell(false);
            }
            else
            {
                await UpdateStatusAsync("Preparing first run experience...");

                await Task.Delay(500);

                await NavigateToAppShell(true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadingPage: Error loading data - {ex.Message}");
            await UpdateStatusAsync("Error loading data. Starting app anyway...");

            await Task.Delay(1000);

            await NavigateToAppShell(true);
        }
    }

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
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusLabel.Text = message;
        });
    }
}
