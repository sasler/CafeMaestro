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
            await UpdateStatusAsync("Preparing your CafeMaestro data...");
            await _appDataService.InitializeAsync(_preferencesService);
            await NavigateToAppShell();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadingPage: Error loading data - {ex.Message}");
            await UpdateStatusAsync("Your data file needs attention. Opening CafeMaestro safely...");
            await Task.Delay(750);
            await NavigateToAppShell();
        }
    }

    private async Task NavigateToAppShell()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current?.Windows.FirstOrDefault() is Window window)
            {
                window.Page = _appShell;
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
