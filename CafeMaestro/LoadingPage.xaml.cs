using System.Diagnostics;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class LoadingPage : ContentPage
{
    private readonly AppDataService? _appDataService;
    private readonly PreferencesService? _preferencesService;
    private readonly IServiceProvider _serviceProvider;
    
    public LoadingPage(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        // Store the service provider
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        
        try
        {
            // Try to get services from dependency injection
            _appDataService = _serviceProvider.GetService<AppDataService>();
            _preferencesService = _serviceProvider.GetService<PreferencesService>();
            
            System.Diagnostics.Debug.WriteLine($"LoadingPage: AppDataService: {(_appDataService != null ? "Available" : "Not available")}");
            System.Diagnostics.Debug.WriteLine($"LoadingPage: PreferencesService: {(_preferencesService != null ? "Available" : "Not available")}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadingPage: Error getting services: {ex.Message}");
            // Continue without services - we'll handle this case in the loading sequence
        }
        
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
            
            // Only try to access preferences if the service is available
            if (_preferencesService != null)
            {
                // Check if we have a saved file path
                savedFilePath = await _preferencesService.GetAppDataFilePathAsync();
                isFirstRun = await _preferencesService.IsFirstRunAsync();
                
                Debug.WriteLine($"LoadingPage: First run: {isFirstRun}, Saved path: {savedFilePath}");
            }
            else
            {
                Debug.WriteLine("LoadingPage: PreferencesService is not available, assuming first run");
            }
            
            if (!string.IsNullOrEmpty(savedFilePath) && File.Exists(savedFilePath) && _appDataService != null)
            {
                // We have a valid file path and app data service, load the data
                await UpdateStatusAsync($"Loading data from {Path.GetFileName(savedFilePath)}...");
                
                // Set the file path and load data
                await _appDataService.SetCustomFilePathAsync(savedFilePath);
                var data = await _appDataService.LoadAppDataAsync();
                
                Debug.WriteLine($"LoadingPage: Successfully loaded data - Beans: {data.Beans?.Count ?? 0}, Roasts: {data.RoastLogs?.Count ?? 0}");
                
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
        await MainThread.InvokeOnMainThreadAsync(() => {
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
                    window.Page = new AppShell();
                    Debug.WriteLine($"LoadingPage: Navigated to AppShell (FirstRun: {setFirstRunNeeded})");
                }
                else
                {
                    Debug.WriteLine("LoadingPage: Could not navigate - Window is null");
                }
            }
            else
            {
                Debug.WriteLine("LoadingPage: Could not navigate - Application.Current or Windows collection is null");
            }
        });
    }
    
    private async Task UpdateStatusAsync(string message)
    {
        // Update the status label on the UI thread
        await MainThread.InvokeOnMainThreadAsync(() => {
            StatusLabel.Text = message;
            Debug.WriteLine($"LoadingPage status: {message}");
        });
    }
}