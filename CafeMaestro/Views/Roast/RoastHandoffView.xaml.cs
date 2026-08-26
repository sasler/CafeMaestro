using CafeMaestro.ViewModels;

namespace CafeMaestro.Views.Roast;

public partial class RoastHandoffView : ContentView
{
    public RoastHandoffView()
    {
        InitializeComponent();
        BindingContextChanged += (_, _) => OnPropertyChanged(nameof(ViewModel));
    }

    public RoastPageViewModel? ViewModel => BindingContext as RoastPageViewModel;
}
