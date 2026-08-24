using System.Collections.Concurrent;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace CafeMaestro.Controls;

/// <summary>
/// Renders one glyph from <c>Resources/Styles/IconGeometries.xaml</c>.
/// </summary>
/// <remarks>
/// Every icon is authored on a 24 x 24 grid with a 1.75 stroke. The control keeps the
/// path at its authored size and compensates its stroke for the scale, so outlines
/// retain the same optical weight at 18, 24 and 32 dp.
/// Colour always comes from a semantic theme resource - the control never has one of
/// its own.
/// </remarks>
public class IconView : ContentView
{
    /// <summary>The unit grid every glyph is authored on.</summary>
    public const double DesignGrid = 24d;

    /// <summary>The authored stroke width, before the glyph is scaled.</summary>
    public const double DesignStrokeThickness = 1.75d;

    private static readonly ConcurrentDictionary<string, Geometry> GeometryCache = new(StringComparer.Ordinal);

    public static readonly BindableProperty DataProperty = BindableProperty.Create(
        nameof(Data), typeof(string), typeof(IconView), propertyChanged: OnDataChanged);

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(
        nameof(IconSize), typeof(double), typeof(IconView), DesignGrid, propertyChanged: OnIconSizeChanged);

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), typeof(Color), typeof(IconView), propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty IsFilledProperty = BindableProperty.Create(
        nameof(IsFilled), typeof(bool), typeof(IconView), false, propertyChanged: OnAppearanceChanged);

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(IconView), propertyChanged: OnDescriptionChanged);

    // Constructed before the IconView constructor body; callbacks also guard against
    // early style application by keeping all sizing/appearance work idempotent.
    private readonly Path _path = new()
    {
        Aspect = Stretch.None,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        WidthRequest = DesignGrid,
        HeightRequest = DesignGrid,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };

    public IconView()
    {
        Content = _path;
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;

        // Decorative until a caller gives the glyph a meaning; icon-only controls are
        // expected to set Description so screen readers announce the action.
        SemanticProperties.SetDescription(this, null);
        ApplySize();
        ApplyAppearance();
    }

    /// <summary>Path data from <c>IconGeometries.xaml</c>, e.g. <c>{StaticResource IconDropData}</c>.</summary>
    public string? Data
    {
        get => (string?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Rendered size in device-independent units. 18, 24 and 32 are the shipped steps.</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Semantic colour for the glyph. Falls back to the primary text colour.</summary>
    public Color? IconColor
    {
        get => (Color?)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    /// <summary>Solid rather than outlined. Reserved for the First Crack bolt.</summary>
    public bool IsFilled
    {
        get => (bool)GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    /// <summary>Accessible name. Leave unset for purely decorative glyphs.</summary>
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>How far the authored 24-unit grid is scaled to reach <paramref name="iconSize"/>.</summary>
    public static double ScaleFor(double iconSize) =>
        double.IsFinite(iconSize) && iconSize > 0 ? iconSize / DesignGrid : 1d;

    /// <summary>
    /// Stroke width in authored-grid units. The path scale multiplies this value, so
    /// dividing by scale holds the rendered outline at <see cref="DesignStrokeThickness"/>.
    /// Filled glyphs carry no stroke.
    /// </summary>
    public static double StrokeThicknessFor(double iconSize, bool isFilled) =>
        isFilled ? 0d : DesignStrokeThickness / ScaleFor(iconSize);

    /// <summary>
    /// Resolves an explicit colour first, then the semantic primary-text resource.
    /// A missing semantic resource returns <see langword="null"/> so the glyph draws
    /// nothing rather than introducing an unthemed fallback colour.
    /// </summary>
    public static Color? ResolveColor(Color? explicitColor, ResourceDictionary? resources)
    {
        if (explicitColor is not null)
        {
            return explicitColor;
        }

        return resources?.TryGetValue("PrimaryTextColor", out object? value) == true
            ? value as Color
            : null;
    }

    /// <summary>Parses path data once and reuses it - the same glyph appears many times per screen.</summary>
    public static Geometry? GetGeometry(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        return GeometryCache.GetOrAdd(data, static value =>
            (Geometry)new PathGeometryConverter().ConvertFromInvariantString(value)!);
    }

    private static void OnDataChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((IconView)bindable)._path.Data = GetGeometry(newValue as string);

    private static void OnIconSizeChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((IconView)bindable).ApplySize();

    private static void OnAppearanceChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((IconView)bindable).ApplyAppearance();

    private static void OnDescriptionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        IconView icon = (IconView)bindable;
        string? description = newValue as string;

        SemanticProperties.SetDescription(icon, description);
        AutomationProperties.SetIsInAccessibleTree(icon, !string.IsNullOrWhiteSpace(description));
    }

    private void ApplySize()
    {
        double size = double.IsFinite(IconSize) && IconSize > 0 ? IconSize : DesignGrid;

        WidthRequest = size;
        HeightRequest = size;

        // The path keeps its authored 24 x 24 box and is scaled about its centre, so
        // every glyph lands on the same optical grid regardless of its own bounds.
        _path.Scale = ScaleFor(size);
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        double size = double.IsFinite(IconSize) && IconSize > 0 ? IconSize : DesignGrid;
        Color? color = ResolveColor(IconColor, Application.Current?.Resources);

        if (color is null)
        {
            _path.Fill = Brush.Transparent;
            _path.Stroke = Brush.Transparent;
            _path.StrokeThickness = 0;
            return;
        }

        if (IsFilled)
        {
            _path.Fill = new SolidColorBrush(color);
            _path.Stroke = Brush.Transparent;
            _path.StrokeThickness = StrokeThicknessFor(size, true);
        }
        else
        {
            // Outlines carry no fill - that is what keeps the set one visual language.
            _path.Fill = Brush.Transparent;
            _path.Stroke = new SolidColorBrush(color);
            _path.StrokeThickness = StrokeThicknessFor(size, false);
        }
    }
}
