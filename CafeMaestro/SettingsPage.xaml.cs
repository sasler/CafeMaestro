using System.ComponentModel;
using CafeMaestro.ViewModels;
using CafeMaestro.Views.Settings;

namespace CafeMaestro;

public partial class SettingsPage : ContentPage
{
    /// <summary>
    /// Below this width the rows own the whole page and each one navigates; above it the
    /// chosen section opens beside them instead.
    /// </summary>
    private const double WideLayoutThreshold = 600;

    /// <summary>
    /// Section bodies already shown once, kept so switching back to a section does not rebuild
    /// it - and so a section the user never opens is never built at all.
    /// </summary>
    private readonly Dictionary<SettingsSection, View> _sectionViews = [];

    private readonly SettingsIndexPageViewModel _viewModel;

    public SettingsPage(SettingsIndexPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        bool isWide = Width >= WideLayoutThreshold;
        SettingsBody.ColumnDefinitions[0].Width = isWide
            ? new GridLength(2, GridUnitType.Star)
            : GridLength.Star;
        SettingsBody.ColumnDefinitions[1].Width = isWide
            ? new GridLength(3, GridUnitType.Star)
            : new GridLength(0);
        WideDetailPane.IsVisible = isWide;
        _ = _viewModel.SetWideLayoutAsync(isWide);
        ShowSelectedSection();
    }

    /// <summary>
    /// The selection and the ViewModel behind it arrive as two separate changes - the section
    /// is chosen first, then built - so the host is refreshed on either.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(SettingsIndexPageViewModel.SelectedSection) or
            nameof(SettingsIndexPageViewModel.IsWideLayout) or
            nameof(SettingsIndexPageViewModel.RoastingViewModel) or
            nameof(SettingsIndexPageViewModel.AppearanceViewModel) or
            nameof(SettingsIndexPageViewModel.DataViewModel) or
            nameof(SettingsIndexPageViewModel.RoastLevelViewModel) or
            nameof(SettingsIndexPageViewModel.AboutViewModel)))
        {
            return;
        }

        ShowSelectedSection();
    }

    /// <summary>
    /// Puts the open section's own body in the pane. Nothing is shown until its ViewModel
    /// exists, so no section body ever renders against an empty binding context.
    /// </summary>
    private void ShowSelectedSection()
    {
        if (!_viewModel.IsWideLayout)
        {
            SectionHost.Content = null;
            return;
        }

        SettingsSection section = _viewModel.SelectedSection;
        if (SectionViewModelFor(section) is not { } sectionViewModel)
        {
            SectionHost.Content = null;
            return;
        }

        if (!_sectionViews.TryGetValue(section, out View? sectionView))
        {
            sectionView = CreateSectionView(section);
            _sectionViews[section] = sectionView;
        }

        sectionView.BindingContext = sectionViewModel;
        SectionHost.Content = sectionView;
    }

    private object? SectionViewModelFor(SettingsSection section) => section switch
    {
        SettingsSection.Roasting => _viewModel.RoastingViewModel,
        SettingsSection.Appearance => _viewModel.AppearanceViewModel,
        SettingsSection.Data => _viewModel.DataViewModel,
        SettingsSection.RoastLevels => _viewModel.RoastLevelViewModel,
        _ => _viewModel.AboutViewModel
    };

    private static View CreateSectionView(SettingsSection section) => section switch
    {
        SettingsSection.Roasting => new RoastingSettingsView(),
        SettingsSection.Appearance => new AppearanceSettingsView(),
        SettingsSection.Data => new DataSettingsView(),
        SettingsSection.RoastLevels => new RoastLevelSettingsView(),
        _ => new AboutView()
    };
}
