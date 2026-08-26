using System.Diagnostics;
using CafeMaestro.Services;

namespace CafeMaestro;

public partial class LoadingPage : ContentPage
{
    private readonly IAppDataService _appDataService;
    private readonly IPreferencesService _preferencesService;
    private readonly IAppActivationService _activationService;
    private readonly ICoolingNotificationWorkflow _notificationWorkflow;
    private readonly AppShell _appShell;

    public LoadingPage(
        IAppDataService appDataService,
        IPreferencesService preferencesService,
        IAppActivationService activationService,
        ICoolingNotificationWorkflow notificationWorkflow,
        AppShell appShell)
    {
        InitializeComponent();
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _activationService = activationService ?? throw new ArgumentNullException(nameof(activationService));
        _notificationWorkflow = notificationWorkflow ?? throw new ArgumentNullException(nameof(notificationWorkflow));
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
            await _notificationWorkflow.ReconcileAsync();
            await NavigateToAppShell();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadingPage: Error loading data - {ex.Message}");
            await UpdateStatusAsync("Your data file needs attention. Opening CafeMaestro safely...");
            await Task.Delay(750);
            await NavigateToAppShell();
            return;
        }

        try
        {
            _activationService.SetReady();
            await MainThread.InvokeOnMainThreadAsync(
                () => _activationService.HandlePendingAsync());
        }
        catch (Exception ex)
        {
            // The activation service keeps a failed payload queued for Ticket 10 to retry.
            // A deep-link failure must never replace the usable Shell with a data warning.
            Debug.WriteLine($"LoadingPage: Deferred activation failed - {ex.Message}");
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
