namespace CafeMaestro;

using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
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
            Debug.WriteLine($"Attempting to navigate to tab: {tabTitle}");
            
            // Make sure Shell.Current is available
            if (Shell.Current == null)
            {
                Debug.WriteLine("Shell.Current is null - cannot navigate");
                return;
            }
            
            // Find the tab with the matching title and select it
            foreach (var item in Shell.Current.Items)
            {
                if (item is FlyoutItem flyoutItem && flyoutItem.Title == tabTitle)
                {
                    Debug.WriteLine($"Found matching tab with title: {tabTitle}");
                    Shell.Current.CurrentItem = flyoutItem;
                    return;
                }
            }
            
            Debug.WriteLine($"No matching tab found with title: {tabTitle}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to tab {tabTitle}: {ex.Message}");
        }
    }
}

