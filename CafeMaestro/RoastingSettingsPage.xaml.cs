using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastingSettingsPage : ContentPage
{
    private readonly RoastingSettingsPageViewModel _viewModel;

    public RoastingSettingsPage(RoastingSettingsPageViewModel viewModel)
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
