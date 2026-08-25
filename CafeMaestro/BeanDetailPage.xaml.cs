using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class BeanDetailPage : ContentPage
{
    private readonly BeanDetailPageViewModel _viewModel;

    public BeanDetailPage(BeanDetailPageViewModel viewModel)
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
