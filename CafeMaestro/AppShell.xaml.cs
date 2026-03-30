using CafeMaestro.Models;
using CafeMaestro.Navigation;

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
		Routing.RegisterRoute(Routes.MainPage, typeof(MainPage));
		Routing.RegisterRoute(Routes.RoastPage, typeof(RoastPage));
		Routing.RegisterRoute(Routes.RoastLogPage, typeof(RoastLogPage));
		Routing.RegisterRoute(Routes.BeanInventoryPage, typeof(BeanInventoryPage));
		Routing.RegisterRoute(Routes.BeanEdit, typeof(BeanEditPage));
		Routing.RegisterRoute(Routes.BeanImport, typeof(BeanImportPage));
		Routing.RegisterRoute(Routes.SettingsPage, typeof(SettingsPage));
		Routing.RegisterRoute(Routes.RoastImport, typeof(RoastImportPage));
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

		if (args.Target.Location.ToString().Contains(Routes.RoastLogPage) ||
			args.Target.Location.ToString().Contains(Routes.BeanInventoryPage) ||
			args.Target.Location.ToString().Contains(Routes.SettingsPage))
		{
			System.Diagnostics.Debug.WriteLine($"Preparing to navigate to a page that needs fresh data: {args.Target.Location}");
		}
	}
}
