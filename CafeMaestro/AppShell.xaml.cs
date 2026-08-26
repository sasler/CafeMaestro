using CafeMaestro.Navigation;
using System.Diagnostics;

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
		Routing.RegisterRoute(Routes.BeanEdit, typeof(BeanEditPage));
		Routing.RegisterRoute(Routes.BeanDetail, typeof(BeanDetailPage));
		Routing.RegisterRoute(Routes.RoastEdit, typeof(RoastEditPage));
		Routing.RegisterRoute(Routes.RoastDetail, typeof(RoastDetailPage));
		Routing.RegisterRoute(Routes.BeanImport, typeof(BeanImportPage));
		Routing.RegisterRoute(Routes.RoastImport, typeof(RoastImportPage));
#if DEBUG
		Routing.RegisterRoute(Routes.ComponentGallery, typeof(Views.ComponentGalleryPage));
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
