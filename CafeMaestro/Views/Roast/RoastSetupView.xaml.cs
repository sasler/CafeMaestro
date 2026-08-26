using CafeMaestro.ViewModels;

namespace CafeMaestro.Views.Roast;

public partial class RoastSetupView : ContentView
{
    public RoastPageViewModel? ViewModel => BindingContext as RoastPageViewModel;

    public RoastSetupView()
    {
        InitializeComponent();
        BindingContextChanged += (_, _) => OnPropertyChanged(nameof(ViewModel));
    }
}
