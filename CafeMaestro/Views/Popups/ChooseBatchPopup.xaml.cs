using CafeMaestro.ViewModels.Popups;

namespace CafeMaestro.Views.Popups;

public partial class ChooseBatchPopup : ContentView
{
    public ChooseBatchPopup() => InitializeComponent();

    public ChooseBatchViewModel? ViewModel => BindingContext as ChooseBatchViewModel;
}
