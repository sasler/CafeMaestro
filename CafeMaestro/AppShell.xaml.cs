using CafeMaestro.Navigation;
using System.Diagnostics;

namespace CafeMaestro;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		RegisterRoutes();

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
		SubscribeNavigationEvents();
	}

	private bool _navigationEventsSubscribed;

	private void OnLoaded(object? sender, EventArgs e) => SubscribeNavigationEvents();

	private void OnUnloaded(object? sender, EventArgs e)
	{
		if (!_navigationEventsSubscribed)
		{
			return;
		}

		Navigating -= OnNavigating;
		_navigationEventsSubscribed = false;
	}

	private void SubscribeNavigationEvents()
	{
		if (_navigationEventsSubscribed)
		{
			return;
		}

		Navigating += OnNavigating;
		_navigationEventsSubscribed = true;
	}

	private void RegisterRoutes()
	{
		Routing.RegisterRoute(Routes.BeanEdit, typeof(BeanEditPage));
		Routing.RegisterRoute(Routes.BeanDetail, typeof(BeanDetailPage));
		Routing.RegisterRoute(Routes.RoastEdit, typeof(RoastEditPage));
		Routing.RegisterRoute(Routes.RoastDetail, typeof(RoastDetailPage));
		Routing.RegisterRoute(Routes.Import, typeof(ImportPage));
		Routing.RegisterRoute(Routes.RoastingSettings, typeof(RoastingSettingsPage));
		Routing.RegisterRoute(Routes.AppearanceSettings, typeof(AppearanceSettingsPage));
		Routing.RegisterRoute(Routes.DataSettings, typeof(DataSettingsPage));
		Routing.RegisterRoute(Routes.RoastLevelSettings, typeof(RoastLevelSettingsPage));
		Routing.RegisterRoute(Routes.About, typeof(AboutPage));
#if DEBUG
		Routing.RegisterRoute(Routes.ComponentGallery, typeof(Views.ComponentGalleryPage));
#endif
	}

	private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
	{
		Debug.WriteLine($"Navigating to: {e.Target.Location}");
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
