using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class RoastLevelSettingsPage : ContentPage
{
    private readonly RoastLevelSettingsPageViewModel _viewModel;

    public RoastLevelSettingsPage(RoastLevelSettingsPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public RoastLevelSettingsPageViewModel ViewModel => _viewModel;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }

    /// <summary>Back closes the editing sheet first rather than leaving the page under it.</summary>
    protected override bool OnBackButtonPressed() =>
        _viewModel.TryHandleBack() || base.OnBackButtonPressed();
}
