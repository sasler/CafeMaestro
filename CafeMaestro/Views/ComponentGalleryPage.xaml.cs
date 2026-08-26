using CafeMaestro.Controls;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;

namespace CafeMaestro.Views;

/// <summary>
/// Debug-only review harness for the shared visual system.
/// </summary>
public partial class ComponentGalleryPage : ContentPage
{
    private const string IconKeyPrefix = "Icon";
    private const string IconKeySuffix = "Data";

    /// <summary>Left + right of the default PagePadding token if resources are unavailable.</summary>
    private const double HorizontalPagePaddingFallback = 32;

    private const double PaletteCellWidth = 92;
    private const double IconCellWidth = 84;

    /// <summary>Semantic colours worth eyeballing side by side in both themes.</summary>
    private static readonly string[] PaletteKeys =
    [
        "SurfaceColor",
        "ElevatedSurfaceColor",
        "RaisedSurfaceColor",
        "PrimaryTextColor",
        "SecondaryTextColor",
        "MutedTextColor",
        "BorderColor",
        "RoastColor",
        "CoolingColor",
        "ReadyColor",
        "AttentionColor",
        "DangerColor",
        "FocusColor",
        "DisabledColor"
    ];

    private readonly ComponentGalleryPageViewModel _viewModel;

    private int _paletteColumns;
    private int _iconColumns;
    private bool _isViewModelSubscribed;
    private bool _isSizeObserved;

    public ComponentGalleryPage(ComponentGalleryPageViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;

        // The swatch and icon grids are laid out for the width that is actually
        // available, so the gallery reflows the same way the real surfaces will.
        ObserveSizeChanges();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isViewModelSubscribed)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _isViewModelSubscribed = true;
        }

        ObserveSizeChanges();

        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_isViewModelSubscribed)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _isViewModelSubscribed = false;
        }

        if (_isSizeObserved)
        {
            GalleryScroll.SizeChanged -= OnGalleryScrollSizeChanged;
            _isSizeObserved = false;
        }
    }

    private void ObserveSizeChanges()
    {
        if (_isSizeObserved)
        {
            return;
        }

        GalleryScroll.SizeChanged += OnGalleryScrollSizeChanged;
        _isSizeObserved = true;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ComponentGalleryPageViewModel.SelectedTheme))
        {
            return;
        }

        ApplyTheme(_viewModel.SelectedTheme);
    }

    // Applying a theme is app-level UI work, so it stays in code-behind.
    private static void ApplyTheme(ThemePreference preference)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        switch (preference)
        {
            case ThemePreference.Light:
                app.UserAppTheme = AppTheme.Light;
                app.SetTheme("Light");
                break;
            case ThemePreference.Dark:
                app.UserAppTheme = AppTheme.Dark;
                app.SetTheme("Dark");
                break;
            default:
                app.UserAppTheme = AppTheme.Unspecified;
                app.SetTheme("System");
                break;
        }
    }

    private void OnGalleryScrollSizeChanged(object? sender, EventArgs e)
    {
        int paletteColumns = CalculateColumnCount(
            GalleryScroll.Width, PaletteCellWidth, Application.Current?.Resources);
        int iconColumns = CalculateColumnCount(
            GalleryScroll.Width, IconCellWidth, Application.Current?.Resources);

        if (paletteColumns == 0 || iconColumns == 0)
        {
            return;
        }

        if (paletteColumns == _paletteColumns && iconColumns == _iconColumns)
        {
            return;
        }

        _paletteColumns = paletteColumns;
        _iconColumns = iconColumns;

        FillGrid(PaletteGallery, PaletteKeys.Select(BuildPaletteCell).ToList(), paletteColumns, PaletteCellWidth);
        FillGrid(IconGallery, IconCells().ToList(), iconColumns, IconCellWidth);
    }

    /// <summary>
    /// Resolves the horizontal space consumed by the shared PagePadding token. The
    /// fallback keeps the debug gallery usable before app resources are available.
    /// </summary>
    public static double ResolveHorizontalPagePadding(ResourceDictionary? resources)
    {
        if (resources?.TryGetValue("PagePadding", out object? value) == true
            && value is Thickness padding
            && double.IsFinite(padding.Left)
            && double.IsFinite(padding.Right)
            && padding.Left >= 0
            && padding.Right >= 0)
        {
            return padding.Left + padding.Right;
        }

        return HorizontalPagePaddingFallback;
    }

    /// <summary>Calculates responsive grid columns from the viewport and shared padding token.</summary>
    public static int CalculateColumnCount(
        double viewportWidth,
        double cellWidth,
        ResourceDictionary? resources)
    {
        if (!double.IsFinite(viewportWidth)
            || !double.IsFinite(cellWidth)
            || cellWidth <= 0)
        {
            return 0;
        }

        double available = viewportWidth - ResolveHorizontalPagePadding(resources);
        return available > 0 ? Math.Max(1, (int)(available / cellWidth)) : 0;
    }

    /// <summary>
    /// Lays the cells out in an explicit Grid. A wrapping layout measures against its
    /// own content rather than the viewport here, and silently runs off the edge.
    /// </summary>
    private static void FillGrid(Layout container, IReadOnlyList<View> cells, int columns, double cellWidth)
    {
        container.Children.Clear();

        int rows = (cells.Count + columns - 1) / columns;
        Grid grid = new();

        for (int column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(cellWidth)));
        }

        for (int row = 0; row < rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (int index = 0; index < cells.Count; index++)
        {
            View cell = cells[index];
            grid.Add(cell, index % columns, index / columns);
        }

        container.Children.Add(grid);

        // Runtime replacement does not propagate the generated Grid's measured height
        // through Android's ScrollView/VerticalStackLayout chain. Mirror the actual
        // measured height (including dynamic text) back to the host whenever it changes.
        grid.SizeChanged += (_, _) =>
        {
            if (grid.Height > 0 && Math.Abs(container.HeightRequest - grid.Height) > 0.5)
            {
                container.HeightRequest = grid.Height;
                ((IView)container).InvalidateMeasure();
            }
        };

        // Android does not re-measure the container when its children are replaced
        // after the first layout pass, and the sections below end up drawn on top.
        ((IView)container).InvalidateMeasure();
    }

    /// <summary>
    /// Renders every glyph declared in IconGeometries.xaml, so a new icon shows up here
    /// without anyone remembering to add it.
    /// </summary>
    private static IEnumerable<View> IconCells()
    {
        foreach (string key in ResourceKeys()
                     .Where(key => key.StartsWith(IconKeyPrefix, StringComparison.Ordinal)
                         && key.EndsWith(IconKeySuffix, StringComparison.Ordinal))
                     .OrderBy(key => key, StringComparer.Ordinal))
        {
            if (Application.Current?.Resources.TryGetValue(key, out object? value) == true
                && value is string data)
            {
                yield return BuildIconCell(key, data);
            }
        }
    }

    private static IEnumerable<string> ResourceKeys()
    {
        ResourceDictionary? resources = Application.Current?.Resources;

        if (resources is null)
        {
            return [];
        }

        return resources.Keys
            .Concat(resources.MergedDictionaries.SelectMany(FlattenKeys))
            .Distinct(StringComparer.Ordinal);
    }

    private static IEnumerable<string> FlattenKeys(ResourceDictionary dictionary) =>
        dictionary.Keys.Concat(dictionary.MergedDictionaries.SelectMany(FlattenKeys));

    private static Style? CaptionStyle() =>
        Application.Current?.Resources.TryGetValue("CardCaptionStyle", out object? style) == true
            ? style as Style
            : null;

    private static View BuildIconCell(string key, string data)
    {
        // The First Crack bolt is the one filled glyph in the set.
        bool isFilled = key == "IconFirstCrackData";

        IconView icon = new()
        {
            Data = data,
            IsFilled = isFilled,
            IconSize = 24
        };

        if (isFilled)
        {
            icon.SetDynamicResource(IconView.IconColorProperty, "RoastColor");
        }

        Label caption = new()
        {
            Text = key[IconKeyPrefix.Length..^IconKeySuffix.Length],
            HorizontalTextAlignment = TextAlignment.Center,
            Style = CaptionStyle()
        };

        return new VerticalStackLayout
        {
            WidthRequest = IconCellWidth,
            Padding = 8,
            Spacing = 6,
            Children = { icon, caption }
        };
    }

    private static View BuildPaletteCell(string key)
    {
        BoxView swatch = new()
        {
            HeightRequest = 32,
            WidthRequest = 32,
            CornerRadius = 8,
            HorizontalOptions = LayoutOptions.Center
        };
        swatch.SetDynamicResource(BoxView.ColorProperty, key);

        Border swatchFrame = new()
        {
            Padding = 2,
            StrokeThickness = 1,
            Content = swatch,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }
        };
        swatchFrame.SetDynamicResource(Border.StrokeProperty, "BorderColor");

        Label caption = new()
        {
            Text = key.Replace("Color", string.Empty, StringComparison.Ordinal),
            HorizontalTextAlignment = TextAlignment.Center,
            Style = CaptionStyle()
        };

        return new VerticalStackLayout
        {
            WidthRequest = PaletteCellWidth,
            Padding = 6,
            Spacing = 6,
            Children = { swatchFrame, caption }
        };
    }
}
