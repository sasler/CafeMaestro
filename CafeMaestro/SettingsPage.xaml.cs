using CafeMaestro.ViewModels;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage
{
    private readonly DataSettingsPageViewModel _viewModel;

    public SettingsPage(DataSettingsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();

        if (_viewModel.ShouldHighlightDataFileSection)
        {
            await HighlightDataFileSectionAsync();
            _viewModel.MarkDataFileSectionHighlighted();
        }
    }

    protected override void OnDisappearing()
    {
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.GoBackAsync();
        return true;
    }

    private async Task HighlightDataFileSectionAsync()
    {
        var originalColor = DataFileSection.BackgroundColor;
        DataFileSection.BackgroundColor = GetResourceColor("HighlightColor", originalColor);
        DataFileSection.Scale = 0.97;
        await DataFileSection.FadeToAsync(0.85, 250);
        await DataFileSection.FadeToAsync(1, 250);
        await DataFileSection.ScaleToAsync(1.02, 150);
        await DataFileSection.ScaleToAsync(1.0, 150);
        await Task.Delay(500);
        DataFileSection.BackgroundColor = originalColor;
    }

    private static Color GetResourceColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out object? value) == true &&
            value is Color color)
        {
            return color;
        }

        return fallback;
    }
}