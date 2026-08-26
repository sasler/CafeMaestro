using Microsoft.Extensions.Logging;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using CafeMaestro.ViewModels.Popups;
using CafeMaestro.Views.Popups;

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
        builder.Services.AddSingleton<IAppDataService, ManagedAppDataService>();
        builder.Services.AddSingleton<ICsvParserService, CsvParserService>();
        builder.Services.AddSingleton<IRoastDataService, RoastDataService>();
        builder.Services.AddSingleton<IBeanDataService, BeanDataService>();
        builder.Services.AddSingleton<ITimerService, TimerService>();
        builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
        builder.Services.AddSingleton<IRoastLevelService, RoastLevelService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IAlertService, AlertService>();
        builder.Services.AddSingleton<IShareService, ShareService>();
        builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);
        builder.Services.AddSingleton<Microsoft.Maui.Storage.IFilePicker>(Microsoft.Maui.Storage.FilePicker.Default);
#if ANDROID
        builder.Services.AddSingleton<IDocumentSaveService, AndroidDocumentSaveService>();
#else
        builder.Services.AddSingleton<IDocumentSaveService, ToolkitDocumentSaveService>();
#endif
        builder.Services.AddSingleton<IUserFileService, UserFileService>();
        builder.Services.AddSingleton<IDataBackupService, DataBackupService>();

        // Roast session domain: the single writer of active-session and roast-workflow state
        builder.Services.AddSingleton<Microsoft.Maui.Storage.IPreferences>(
            Microsoft.Maui.Storage.Preferences.Default);
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<IRoastPreferencesService, RoastPreferencesService>();
        builder.Services.AddSingleton<ICoolingNotificationService, NoOpCoolingNotificationService>();
        builder.Services.AddSingleton<IRoastSessionService, RoastSessionService>();
        builder.Services.AddSingleton<IRoastQueryService, RoastQueryService>();
        builder.Services.AddSingleton<IDisplayWakeService, DisplayWakeService>();
        builder.Services.AddSingleton<IAppActivationHandler, NoOpAppActivationHandler>();
        builder.Services.AddSingleton<IAppActivationService, AppActivationService>();
        builder.Services.AddSingleton<IRoastRecoveryAdapter, RoastRecoveryAdapter>();
        builder.Services.AddSingleton<IOverlayService, OverlayService>();
        builder.Services.AddSingleton<IImportAdapter, BeanImportAdapter>();
        builder.Services.AddSingleton<IImportAdapter, RoastImportAdapter>();
        builder.Services.AddSingleton<IImportService, ImportService>();

        builder.Services.AddTransientPopup<WeighInPopup, WeighInViewModel>();
        builder.Services.AddTransientPopup<ChooseBatchPopup, ChooseBatchViewModel>();
        builder.Services.AddTransientPopup<DiscardRoastPopup, DiscardRoastViewModel>();
        builder.Services.AddTransientPopup<ConfirmNavigationPopup, ConfirmNavigationViewModel>();
        builder.Services.AddTransientPopup<ConfirmResetPopup, ConfirmResetViewModel>();
        builder.Services.AddTransientPopup<TimeCorrectionPopup, TimeCorrectionViewModel>();

        // Register Pages for DI - changing to transient to avoid state retention
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<LoadingPage>();
        builder.Services.AddTransient<DataSettingsPageViewModel>();
        builder.Services.AddSingleton<RoastPageViewModel>();
        builder.Services.AddTransient<RoastEditPageViewModel>();
        builder.Services.AddTransient<BeanInventoryPageViewModel>();
        builder.Services.AddTransient<BeanDetailPageViewModel>();
        builder.Services.AddTransient<BeanEditPageViewModel>();
        builder.Services.AddTransient<RoastLogPageViewModel>();
        builder.Services.AddTransient<RoastDetailPageViewModel>();
        builder.Services.AddTransient<ImportPageViewModel>();
        builder.Services.AddTransient<RoastPage>();
        builder.Services.AddTransient<RoastEditPage>();
        builder.Services.AddTransient<BeanInventoryPage>();
        builder.Services.AddTransient<BeanDetailPage>();
        builder.Services.AddTransient<BeanEditPage>();
        builder.Services.AddTransient<RoastLogPage>();
        builder.Services.AddTransient<RoastDetailPage>();
        builder.Services.AddTransient<ImportPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        // Review harness for the shared visual system - Debug builds only.
        builder.Services.AddTransient<ComponentGalleryPageViewModel>();
        builder.Services.AddTransient<Views.ComponentGalleryPage>();

        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
