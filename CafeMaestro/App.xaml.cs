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
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
	
	private async void InitializeAppDataAsync()
	{
		try
		{
			// Check if user has a saved file path preference
			string savedFilePath = await _preferencesService.GetAppDataFilePathAsync();
			
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