using Microsoft.Extensions.Logging;
using CafeMaestro.Services;

namespace CafeMaestro;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("digital-7-mono.ttf", "Digital7");
			});

		// Register services
		builder.Services.AddSingleton<AppDataService>();
		builder.Services.AddSingleton<RoastDataService>();
		builder.Services.AddSingleton<BeanService>();
		builder.Services.AddSingleton<TimerService>();
		builder.Services.AddSingleton<PreferencesService>();

        // Register Pages for DI (optional but good practice)
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<RoastPage>(); // Use Transient if state shouldn't persist across navigations
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
