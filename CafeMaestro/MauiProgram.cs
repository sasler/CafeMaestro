using Microsoft.Extensions.Logging;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;

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
			})
			.ConfigureEssentials(essentials =>
			{
				essentials.UseVersionTracking();
			});

		// Register services
		builder.Services.AddSingleton<IAppDataService, AppDataService>();
		builder.Services.AddSingleton<ICsvParserService, CsvParserService>();
		builder.Services.AddSingleton<IRoastDataService, RoastDataService>();
		builder.Services.AddSingleton<IBeanDataService, BeanDataService>();
		builder.Services.AddSingleton<ITimerService, TimerService>();
		builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
		builder.Services.AddSingleton<IRoastLevelService, RoastLevelService>();
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		builder.Services.AddSingleton<IAlertService, AlertService>();
		builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);
		builder.Services.AddSingleton<IFolderPicker>(FolderPicker.Default);

        // Register Pages for DI - changing to transient to avoid state retention
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<LoadingPage>();
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<SettingsPageViewModel>();
        builder.Services.AddTransient<RoastPageViewModel>();
        builder.Services.AddTransient<BeanInventoryPageViewModel>();
        builder.Services.AddTransient<BeanEditPageViewModel>();
        builder.Services.AddTransient<RoastLogPageViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<RoastPage>();
        builder.Services.AddTransient<BeanInventoryPage>();
        builder.Services.AddTransient<BeanEditPage>();
        builder.Services.AddTransient<RoastLogPage>();
        builder.Services.AddTransient<BeanImportPage>();
        builder.Services.AddTransient<RoastImportPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
