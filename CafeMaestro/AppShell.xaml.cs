using CafeMaestro.Models;

namespace CafeMaestro;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		RegisterRoutes();

		Navigating += OnNavigating;
		Navigated += OnNavigated;
	}

	private void RegisterRoutes()
	{
		Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
		Routing.RegisterRoute(nameof(RoastPage), typeof(RoastPage));
		Routing.RegisterRoute(nameof(RoastLogPage), typeof(RoastLogPage));
		Routing.RegisterRoute(nameof(BeanInventoryPage), typeof(BeanInventoryPage));
		Routing.RegisterRoute(nameof(BeanEditPage), typeof(BeanEditPage));
		Routing.RegisterRoute(nameof(BeanImportPage), typeof(BeanImportPage));
		Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
		Routing.RegisterRoute(nameof(RoastImportPage), typeof(RoastImportPage));
	}

	private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
	{
		System.Diagnostics.Debug.WriteLine($"Navigating to: {e.Target.Location}");
	}

	private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		if (CurrentPage != null && Application.Current is App app)
		{
			if (CurrentPage.BindingContext is NavigationParameters)
				return;

			app.PassDataToPage(CurrentPage);
		}
	}

	protected override void OnNavigating(ShellNavigatingEventArgs args)
	{
		base.OnNavigating(args);

		if (args.Target.Location.ToString().Contains(nameof(RoastLogPage)) ||
			args.Target.Location.ToString().Contains(nameof(BeanInventoryPage)) ||
			args.Target.Location.ToString().Contains(nameof(SettingsPage)))
		{
			System.Diagnostics.Debug.WriteLine($"Preparing to navigate to a page that needs fresh data: {args.Target.Location}");
		}
	}
}
