using CafeMaestro.Services;
using System.Threading.Tasks;

namespace CafeMaestro;

public partial class App : Application
{
	private AppDataService _appDataService;
	private PreferencesService _preferencesService;
	
	public App()
	{
		InitializeComponent();
		
		// Get the app data service
		_appDataService = Handler?.MauiContext?.Services.GetService<AppDataService>() ?? 
		                 new AppDataService();
		_preferencesService = Handler?.MauiContext?.Services.GetService<PreferencesService>() ?? 
		                     new PreferencesService();
		                     
		// Initialize app data asynchronously
		InitializeAppDataAsync();
		
		// Load theme preference
		LoadThemePreference();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
		// Load and apply the saved theme preference
	private async void LoadThemePreference()
	{
		try
		{
			var theme = await _preferencesService.GetThemePreferenceAsync();
			
			// Apply the theme
			switch (theme)
			{
				case Services.ThemePreference.Light:
					UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Light;
					break;
				case Services.ThemePreference.Dark:
					UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Dark;
					break;
				case Services.ThemePreference.System:
				default:
					UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Unspecified;
					break;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading theme preference: {ex.Message}");
			// Default to system theme
			UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Unspecified;
		}
	}
	
	private async void InitializeAppDataAsync()
	{
		try
		{
			// Check if user has a saved file path preference
			string? savedFilePath = await _preferencesService.GetAppDataFilePathAsync();
			
			if (!string.IsNullOrEmpty(savedFilePath))
			{
				// If custom file exists, use it
				if (System.IO.File.Exists(savedFilePath))
				{
					_appDataService.SetCustomFilePath(savedFilePath);
				}
			}
			
			// Migrate data from old separate files if needed
			await _appDataService.MigrateDataIfNeededAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error initializing app data: {ex.Message}");
		}
	}
}