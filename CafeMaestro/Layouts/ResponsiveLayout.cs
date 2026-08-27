namespace CafeMaestro.Layouts;

/// <summary>
/// Keeps full-width content readable on tablets.
///
/// A form or reading column that is comfortable at 360 dp becomes a stretched, hard-to-scan
/// band at 1000 dp. Attaching <see cref="MaxContentWidthProperty"/> to a layout caps how wide
/// its content is allowed to run and centres what is left over, so the same XAML fills a phone
/// and sits in a centred column on a tablet.
///
/// The cap is applied as symmetric horizontal padding on top of whatever padding the layout
/// already declares, so <c>PagePadding</c> and friends keep working unchanged.
/// </summary>
public static class ResponsiveLayout
{
    /// <summary>Sentinel meaning "we have not written this layout's padding yet".</summary>
    private static readonly Thickness Unwritten = new(double.NaN);

    /// <summary>
    /// The widest the attached layout's content may run, in device-independent units.
    /// Anything wider is absorbed as equal padding on both sides.
    /// </summary>
    public static readonly BindableProperty MaxContentWidthProperty =
        BindableProperty.CreateAttached(
            "MaxContentWidth",
            typeof(double),
            typeof(ResponsiveLayout),
            double.PositiveInfinity,
            propertyChanged: OnMaxContentWidthChanged);

    /// <summary>The layout's own padding, before this helper widened it.</summary>
    private static readonly BindableProperty BasePaddingProperty =
        BindableProperty.CreateAttached(
            "BasePadding",
            typeof(Thickness),
            typeof(ResponsiveLayout),
            default(Thickness));

    /// <summary>The padding this helper last wrote, used to detect changes made elsewhere.</summary>
    private static readonly BindableProperty AppliedPaddingProperty =
        BindableProperty.CreateAttached(
            "AppliedPadding",
            typeof(Thickness),
            typeof(ResponsiveLayout),
            Unwritten);

    public static double GetMaxContentWidth(BindableObject target) =>
        (double)target.GetValue(MaxContentWidthProperty);

    public static void SetMaxContentWidth(BindableObject target, double value) =>
        target.SetValue(MaxContentWidthProperty, value);

    /// <summary>
    /// How much padding to add to each side so <paramref name="availableWidth"/> of space
    /// presents a content column no wider than <paramref name="maxContentWidth"/>.
    /// </summary>
    public static double ComputeSideInset(double availableWidth, double maxContentWidth)
    {
        if (double.IsNaN(availableWidth) || availableWidth <= 0)
        {
            return 0;
        }

        if (double.IsNaN(maxContentWidth) || maxContentWidth <= 0 || double.IsInfinity(maxContentWidth))
        {
            return 0;
        }

        return Math.Max(0, (availableWidth - maxContentWidth) / 2);
    }

    private static void OnMaxContentWidthChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Layout layout)
        {
            return;
        }

        layout.SizeChanged -= OnLayoutSizeChanged;

        if (newValue is not double maxWidth || maxWidth <= 0 || double.IsInfinity(maxWidth))
        {
            Restore(layout);
            return;
        }

        layout.SizeChanged += OnLayoutSizeChanged;
        Apply(layout);
    }

    private static void OnLayoutSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Layout layout)
        {
            Apply(layout);
        }
    }

    private static void Apply(Layout layout)
    {
        // Re-baseline whenever the padding changed behind our back - XAML parse order and
        // later code both set Padding, and neither knows this helper exists.
        Thickness applied = (Thickness)layout.GetValue(AppliedPaddingProperty);
        if (double.IsNaN(applied.Left) || layout.Padding != applied)
        {
            layout.SetValue(BasePaddingProperty, layout.Padding);
        }

        Thickness basePadding = (Thickness)layout.GetValue(BasePaddingProperty);
        double inset = ComputeSideInset(layout.Width, GetMaxContentWidth(layout));
        var target = new Thickness(
            basePadding.Left + inset,
            basePadding.Top,
            basePadding.Right + inset,
            basePadding.Bottom);

        layout.SetValue(AppliedPaddingProperty, target);
        if (layout.Padding != target)
        {
            layout.Padding = target;
        }
    }

    private static void Restore(Layout layout)
    {
        Thickness applied = (Thickness)layout.GetValue(AppliedPaddingProperty);
        if (double.IsNaN(applied.Left))
        {
            return;
        }

        layout.SetValue(AppliedPaddingProperty, Unwritten);
        layout.Padding = (Thickness)layout.GetValue(BasePaddingProperty);
    }
}
