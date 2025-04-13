namespace CafeMaestro;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Register routes for navigation
		Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
		Routing.RegisterRoute(nameof(RoastPage), typeof(RoastPage)); // Add RoastPage route
		Routing.RegisterRoute(nameof(RoastLogPage), typeof(RoastLogPage));
		Routing.RegisterRoute(nameof(BeanInventoryPage), typeof(BeanInventoryPage));
		Routing.RegisterRoute(nameof(BeanEditPage), typeof(BeanEditPage));
		Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
	}
}
