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
	}	// Load and apply the saved theme preference
	private async void LoadThemePreference()
	{
		try
		{
			var theme = await _preferencesService.GetThemePreferenceAsync();

			// Apply the app theme for system-level controls
			switch (theme)
			{
				case Services.ThemePreference.Light:
					UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Light;
					SetTheme("Light");
					break;
				case Services.ThemePreference.Dark:
					UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Dark;
					SetTheme("Dark");
					break;
				case Services.ThemePreference.System:
				default:
					UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Unspecified;
					SetTheme("System");
					break;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading theme preference: {ex.Message}");
			// Default to system theme
			UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Unspecified;
			SetTheme("System");
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
	}	public void SetTheme(string theme)
	{
		try
		{
			// Safely get the merged dictionaries collection
			var mergedDictionaries = Resources?.MergedDictionaries;
			if (mergedDictionaries == null)
				return;

			// Since we can't set Source programmatically, we'll handle styles.xaml differently
			// First, let's identify theme dictionaries and other dictionaries
			var themeDictionaries = new List<ResourceDictionary>();
			var otherDictionaries = new List<ResourceDictionary>();

			foreach (var dict in mergedDictionaries.ToList())
			{
				string? source = dict.Source?.OriginalString;
				if (source != null && (source.Contains("LightTheme.xaml") || source.Contains("DarkTheme.xaml")))
				{
					themeDictionaries.Add(dict);
				}
				else
				{
					otherDictionaries.Add(dict);
				}
			}			// Remove only theme dictionaries, keeping other dictionaries intact
			foreach (var dict in themeDictionaries)
			{
				mergedDictionaries.Remove(dict);
			}
			
			// Add the new theme dictionary
			ResourceDictionary newTheme;
			switch (theme)
			{
				case "Light":
					newTheme = new LightTheme();
					break;
				case "Dark":
					newTheme = new DarkTheme();
					break;
				default:
					// Set theme based on system preference
					if (Current?.RequestedTheme == AppTheme.Dark)
						newTheme = new DarkTheme();
					else
						newTheme = new LightTheme();
					break;
			}
			
			// Add the theme dictionary first for proper precedence
			mergedDictionaries.Add(newTheme);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error in SetTheme: {ex.Message}");
			System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
		}
	}
}