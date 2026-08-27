using CafeMaestro.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CafeMaestro.Services;

/// <summary>
/// Resolves section ViewModels from the same container Shell uses, so an inline section on a
/// tablet is wired exactly like the page a phone would have navigated to.
/// </summary>
public sealed class SettingsSectionViewModelFactory : ISettingsSectionViewModelFactory
{
    private readonly IServiceProvider _services;

    public SettingsSectionViewModelFactory(IServiceProvider services) =>
        _services = services ?? throw new ArgumentNullException(nameof(services));

    public RoastingSettingsPageViewModel CreateRoasting() =>
        _services.GetRequiredService<RoastingSettingsPageViewModel>();

    public AppearanceSettingsPageViewModel CreateAppearance() =>
        _services.GetRequiredService<AppearanceSettingsPageViewModel>();

    public DataSettingsPageViewModel CreateData() =>
        _services.GetRequiredService<DataSettingsPageViewModel>();

    public RoastLevelSettingsPageViewModel CreateRoastLevels() =>
        _services.GetRequiredService<RoastLevelSettingsPageViewModel>();

    public AboutPageViewModel CreateAbout() =>
        _services.GetRequiredService<AboutPageViewModel>();
}
