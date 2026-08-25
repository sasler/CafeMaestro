using CafeMaestro.Drawing;

namespace CafeMaestro.Views.Roast;

public sealed class CoolingArcView : GraphicsView
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress),
        typeof(double),
        typeof(CoolingArcView),
        0d,
        propertyChanged: OnProgressChanged);

    private RoastInstrumentDrawable? _drawable;

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public CoolingArcView()
    {
        Loaded += OnLoaded;
        HeightRequest = 48;
        WidthRequest = 48;
        AutomationProperties.SetIsInAccessibleTree(this, false);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _drawable = new RoastInstrumentDrawable
        {
            TrackColor = ResourceColor("BorderColor"),
            RoastColor = ResourceColor("RoastColor"),
            PausedColor = ResourceColor("MutedTextColor"),
            CoolingColor = ResourceColor("CoolingColor"),
            IsCooling = true,
            Progress = Progress
        };
        Drawable = _drawable;
        Invalidate();
    }

    private static void OnProgressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (CoolingArcView)bindable;
        if (view._drawable is not null)
        {
            view._drawable.Progress = (double)newValue;
            view.Invalidate();
        }
    }

    private Color ResourceColor(string key) =>
        (Color)(Resources.TryGetValue(key, out object value) ? value : Application.Current!.Resources[key]);
}
