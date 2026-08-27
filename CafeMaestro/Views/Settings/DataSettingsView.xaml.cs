using CafeMaestro.ViewModels;

namespace CafeMaestro.Views.Settings;

public partial class DataSettingsView : ContentView
{
    public DataSettingsView() => InitializeComponent();

    /// <summary>Lets backup item templates reach the commands that sit above their own item.</summary>
    public DataSettingsPageViewModel? ViewModel => BindingContext as DataSettingsPageViewModel;
}
