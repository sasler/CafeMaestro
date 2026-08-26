using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class AboutPage : ContentPage
{
    private readonly AboutPageViewModel _viewModel;

    public AboutPage(AboutPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
