using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastDetailPage : ContentPage
{
    private readonly RoastDetailPageViewModel _viewModel;

    public RoastDetailPage(RoastDetailPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }

    protected override void OnDisappearing()
    {
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }
}
