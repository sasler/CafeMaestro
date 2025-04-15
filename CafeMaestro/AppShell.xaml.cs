using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro;

public partial class AppShell : Shell
{
	private readonly IServiceProvider _serviceProvider;

	public AppShell(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;

		// Store the service provider for the entire application to use
		// Add null check to prevent possible null reference exception
		if (Application.Current != null)
		{
			Application.Current.Resources["ServiceProvider"] = serviceProvider;
		}

		// Register routes for navigation
		Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
		Routing.RegisterRoute(nameof(RoastPage), typeof(RoastPage));
		Routing.RegisterRoute(nameof(RoastLogPage), typeof(RoastLogPage));
		Routing.RegisterRoute(nameof(BeanInventoryPage), typeof(BeanInventoryPage));
		Routing.RegisterRoute(nameof(BeanEditPage), typeof(BeanEditPage));
		Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
	}
}
