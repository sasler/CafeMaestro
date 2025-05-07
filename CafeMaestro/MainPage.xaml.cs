namespace CafeMaestro;

using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.IO;
using CafeMaestro.Services;
using CafeMaestro.Models;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

public partial class MainPage : ContentPage
{
    private readonly AppDataService _appDataService;
    private readonly PreferencesService _preferencesService;
    private string _userDataFilePath = string.Empty;

    public MainPage(AppDataService appDataService, PreferencesService preferencesService)
    {
        InitializeComponent();

        // Store the injected service instances
        _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));

        // Subscribe to events
        _appDataService.DataChanged += OnAppDataChanged;
        _appDataService.DataFilePathChanged += OnDataFilePathChanged;

        // Display a placeholder initially
        DataFileNameLabel.Text = "File: Loading...";
        DataStatsLabel.Text = "Beans: --  |  Roasts: --";

        // Start with getting the correct file path
        Task.Run(async () => await InitializeWithCorrectPathAsync());
    }

    private async Task InitializeWithCorrectPathAsync()
    {
        try
        {
            // First, get the path from preferences
            string? savedFilePath = await _preferencesService.GetAppDataFilePathAsync();

            if (!string.IsNullOrEmpty(savedFilePath))
            {
                _userDataFilePath = savedFilePath;

                // Wait for the app to fully initialize, then verify we're using the correct path
                await Task.Delay(100);

                // Check if the current path in AppDataService matches the user's path
                string currentPath = _appDataService.DataFilePath;

                if (currentPath != savedFilePath)
                {
                    // Force the path to be set correctly
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _appDataService.SetCustomFilePathAsync(savedFilePath);
                    });
                }
            }

            // Now update the UI with the correct data
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateDataFileInfo();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in InitializeWithCorrectPathAsync: {ex.Message}");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Only update the display with current data
        UpdateDataFileInfo();

        // Check if we need to fetch the correct path again
        if (!string.IsNullOrEmpty(_userDataFilePath) && _appDataService.DataFilePath != _userDataFilePath)
        {
            Task.Run(async () => await InitializeWithCorrectPathAsync());
        }

        // Log the current data state
        var appData = _appDataService.CurrentData;
    }

    private void OnAppDataChanged(object? sender, AppData appData)
    {
        // Update UI with the new data counts
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateDataStats(appData);
        });
    }

    private void OnDataFilePathChanged(object? sender, string filePath)
    {
        // Update UI with the new file path
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateDataFilePath(filePath);

            // Update our saved path
            if (!string.IsNullOrEmpty(filePath))
            {
                _userDataFilePath = filePath;
            }
        });
    }

    private void UpdateDataFileInfo()
    {
        try
        {
            // Update file path
            UpdateDataFilePath(_appDataService.DataFilePath);

            // Update data stats
            var appData = _appDataService.CurrentData;
            UpdateDataStats(appData);

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating data file info: {ex.Message}");
        }
    }

    private void UpdateDataFilePath(string filePath)
    {
        // Extract just the filename for display
        string fileName = Path.GetFileName(filePath);
        DataFileNameLabel.Text = $"File: {fileName}";
    }

    private void UpdateDataStats(AppData appData)
    {
        int beanCount = appData.Beans?.Count ?? 0;
        int roastCount = appData.RoastLogs?.Count ?? 0;

        DataStatsLabel.Text = $"Beans: {beanCount}  |  Roasts: {roastCount}";
    }

    private void GoToRoastPage_Clicked(object sender, EventArgs e)
    {
        NavigateToTabUsingShell("Roast");
    }

    private void GoToBeanInventory_Clicked(object sender, EventArgs e)
    {
        NavigateToTabUsingShell("Beans");
    }

    private void GoToRoastLog_Clicked(object sender, EventArgs e)
    {
        NavigateToTabUsingShell("Roast Log");
    }

    private void GoToSettings_Clicked(object sender, EventArgs e)
    {
        NavigateToTabUsingShell("Settings");
    }

    private void NavigateToTabUsingShell(string tabTitle)
    {
        try
        {
            // Make sure Shell.Current is available
            if (Shell.Current == null)
            {
                return;
            }

            // With TabBar implementation, we need to look for ShellContent items instead of FlyoutItems
            // Get all TabBar items (which will be the only items since we disabled flyout)
            var tabBar = Shell.Current.Items.FirstOrDefault();
            if (tabBar == null)
            {
                return;
            }

            // Find the ShellContent with the matching title
            foreach (var item in Shell.Current.Items.SelectMany(shellItem => shellItem.Items).SelectMany(shellSection => shellSection.Items))
            {
                if (item.Title == tabTitle)
                {
                    Shell.Current.GoToAsync($"//{item.Route}");
                    return;
                }
            }

            // If not found by direct title match, try alternative route names
            string routeName = tabTitle.Replace(" ", ""); // Convert "Roast Log" to "RoastLog"
            Shell.Current.GoToAsync($"//{routeName}Page");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to tab {tabTitle}: {ex.Message}");
        }
    }

    // Override OnBackButtonPressed to handle Android back button on main page
    protected override bool OnBackButtonPressed()
    {
        try
        {
            // Return false to let the system handle the back button
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error handling back button in MainPage: {ex.Message}");
        }

        return base.OnBackButtonPressed();
    }
}

