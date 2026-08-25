using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsIndexPageViewModel _viewModel;

    public SettingsPage(SettingsIndexPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Every return from a detail page re-reads the preferences, so the summaries below the
    /// row titles are never stale.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.GoBackAsync();
        return true;
    }
}
