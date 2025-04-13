namespace CafeMaestro;

using Microsoft.Maui.Controls;
using System;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void GoToRoastPage_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RoastPage));
    }

    private async void GoToBeanInventory_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(BeanInventoryPage));
    }

    private async void GoToRoastLog_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RoastLogPage));
    }

    private async void GoToSettings_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }
}

