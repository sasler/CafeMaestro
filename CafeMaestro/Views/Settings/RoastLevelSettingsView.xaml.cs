using CafeMaestro.ViewModels;

namespace CafeMaestro.Views.Settings;

public partial class RoastLevelSettingsView : ContentView
{
    public RoastLevelSettingsView() => InitializeComponent();

    /// <summary>Lets level item templates reach the commands that sit above their own item.</summary>
    public RoastLevelSettingsPageViewModel? ViewModel => BindingContext as RoastLevelSettingsPageViewModel;
}
