using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastEditPage : ContentPage
{
    private readonly RoastEditPageViewModel _viewModel;

    public RoastEditPage(RoastEditPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
