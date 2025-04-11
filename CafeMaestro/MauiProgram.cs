﻿using Microsoft.Extensions.Logging;
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
		builder.Services.AddSingleton<RoastDataService>();
		builder.Services.AddSingleton<TimerService>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
