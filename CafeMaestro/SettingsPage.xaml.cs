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

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        bool isWide = Width >= 600;
        SettingsBody.ColumnDefinitions[0].Width = isWide
            ? new GridLength(2, GridUnitType.Star)
            : GridLength.Star;
        SettingsBody.ColumnDefinitions[1].Width = isWide
            ? new GridLength(3, GridUnitType.Star)
            : new GridLength(0);
        WideDetailPane.IsVisible = isWide;
        _viewModel.SetWideLayout(isWide);
    }
}
