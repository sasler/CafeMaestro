using Microsoft.Extensions.Logging;
using CafeMaestro.Services;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;

namespace CafeMaestro;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("digital-7-mono.ttf", "Digital7");
			});

		// Register services
		builder.Services.AddSingleton<AppDataService>();
		builder.Services.AddSingleton<RoastDataService>();
		builder.Services.AddSingleton<BeanDataService>();
		builder.Services.AddSingleton<TimerService>();
		builder.Services.AddSingleton<PreferencesService>();
		builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);
		builder.Services.AddSingleton<IFolderPicker>(FolderPicker.Default);

        // Register Pages for DI - changing to transient to avoid state retention
        builder.Services.AddTransient<LoadingPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<RoastPage>();
        builder.Services.AddTransient<BeanInventoryPage>();
        builder.Services.AddTransient<BeanEditPage>();
        builder.Services.AddTransient<RoastLogPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
