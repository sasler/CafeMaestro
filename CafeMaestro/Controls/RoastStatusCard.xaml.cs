using CafeMaestro.ViewModels;

namespace CafeMaestro.Controls;

public partial class RoastStatusCard : ContentView
{
    public static readonly BindableProperty CardProperty = BindableProperty.Create(
        nameof(Card),
        typeof(RoastLogCard),
        typeof(RoastStatusCard),
        null);

    public RoastStatusCard()
    {
        InitializeComponent();
    }

    public RoastLogCard? Card
    {
        get => (RoastLogCard?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }
}
