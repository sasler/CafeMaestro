using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using CafeMaestro.Models;

namespace CafeMaestro;

public partial class AppShell : Shell
{
	private IServiceProvider? _serviceProvider;

	// Add parameterless constructor for default initialization
	public AppShell()
	{
		InitializeComponent();
		
		// Try to get service provider from Application if available
		if (Application.Current?.Handler?.MauiContext?.Services is IServiceProvider sp)
		{
			_serviceProvider = sp;
			
			// Store in application resources
			if (Application.Current.Resources.ContainsKey("ServiceProvider"))
				Application.Current.Resources["ServiceProvider"] = sp;
			else
				Application.Current.Resources.Add("ServiceProvider", sp);
				
			System.Diagnostics.Debug.WriteLine("AppShell initialized with service provider from Application");
		}
		else
		{
			System.Diagnostics.Debug.WriteLine("AppShell initialized without service provider");
		}

		// Register routes for navigation
		RegisterRoutes();
		
		// Handle navigation events to pass data between pages
		Navigating += OnNavigating;
		Navigated += OnNavigated;
	}

	// Keep existing constructor for backward compatibility
	public AppShell(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;

		// Store the service provider for the entire application to use
		// Add null check to prevent possible null reference exception
		if (Application.Current != null)
		{
			if (Application.Current.Resources.ContainsKey("ServiceProvider"))
				Application.Current.Resources["ServiceProvider"] = serviceProvider;
			else
				Application.Current.Resources.Add("ServiceProvider", serviceProvider);
				
			System.Diagnostics.Debug.WriteLine("AppShell initialized with provided service provider");
		}

		// Register routes for navigation
		RegisterRoutes();
		
		// Handle navigation events to pass data between pages
		Navigating += OnNavigating;
		Navigated += OnNavigated;
	}
	
	private void RegisterRoutes()
	{
		// Register routes for navigation
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
		// Called before navigation occurs
		System.Diagnostics.Debug.WriteLine($"Navigating to: {e.Target.Location}");
	}
	
	private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		// Called after navigation is complete
		System.Diagnostics.Debug.WriteLine($"Navigated to: {e.Current.Location}");
		
		// Pass app data to the current page if needed
		if (CurrentPage != null && Application.Current is App app)
		{
			// Check if the page already has our navigation parameters
			if (CurrentPage.BindingContext is NavigationParameters)
				return;
				
			// Pass data to the current page
			app.PassDataToPage(CurrentPage);
		}
	}
	
	protected override void OnNavigating(ShellNavigatingEventArgs args)
	{
		base.OnNavigating(args);
		
		// This is called when navigation is about to occur
		if (args.Target.Location.ToString().Contains(nameof(RoastLogPage)) ||
			args.Target.Location.ToString().Contains(nameof(BeanInventoryPage)) ||
			args.Target.Location.ToString().Contains(nameof(SettingsPage)))
		{
			// For these pages, ensure they get fresh data
			System.Diagnostics.Debug.WriteLine($"Preparing to navigate to a page that needs fresh data: {args.Target.Location}");
		}
	}
}
