using CafeMaestro.ViewModels;

namespace CafeMaestro.Services;

/// <summary>
/// Builds the ViewModel behind one settings section on demand.
///
/// On a phone each section is a page and Shell resolves its ViewModel. On a tablet the
/// Settings index hosts the same section bodies inline, so it needs the same ViewModels
/// without navigating - and it should only pay for the one the user actually opened.
/// </summary>
public interface ISettingsSectionViewModelFactory
{
    RoastingSettingsPageViewModel CreateRoasting();

    AppearanceSettingsPageViewModel CreateAppearance();

    DataSettingsPageViewModel CreateData();

    RoastLevelSettingsPageViewModel CreateRoastLevels();

    AboutPageViewModel CreateAbout();
}
