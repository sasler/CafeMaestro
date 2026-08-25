using CafeMaestro.Navigation;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace CafeMaestro;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		RegisterRoutes();
		AddDebugDestinations();

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
		Routing.RegisterRoute(Routes.BeanDetail, typeof(BeanDetailPage));
		Routing.RegisterRoute(Routes.BeanImport, typeof(BeanImportPage));
		Routing.RegisterRoute(Routes.SettingsPage, typeof(SettingsPage));
		Routing.RegisterRoute(Routes.RoastImport, typeof(RoastImportPage));
	}

	/// <summary>
	/// Adds the component gallery tab in Debug builds only, so the shared visual system
	/// can be reviewed on a device without ever shipping the harness.
	/// </summary>
	private void AddDebugDestinations()
	{
#if DEBUG
		// The tab itself supplies the route, so it must not also be registered globally.
		if (Items.OfType<TabBar>().FirstOrDefault() is not TabBar tabBar)
		{
			return;
		}

		ShellContent gallery = new()
		{
			Title = "Gallery",
			Route = Routes.ComponentGallery,
			// The page takes its ViewModel through DI, so resolve rather than activate.
			ContentTemplate = new DataTemplate(static () =>
				IPlatformApplication.Current!.Services.GetRequiredService<Views.ComponentGalleryPage>())
		};

		tabBar.Items.Add(gallery);
#endif
	}

	private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
	{
		Debug.WriteLine($"Navigating to: {e.Target.Location}");
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
			Debug.WriteLine($"Preparing to navigate to a page that needs fresh data: {args.Target.Location}");
		}
	}
}
