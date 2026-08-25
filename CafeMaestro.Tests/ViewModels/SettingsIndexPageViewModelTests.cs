using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class SettingsIndexPageViewModelTests
{
    [Fact]
    public async Task OnAppearing_SummarisesEveryRowFromTheValuesCurrentlyStored()
    {
        var roastPreferences = new Mock<IRoastPreferencesService>();
        roastPreferences.Setup(service => service.GetFirstCrackEnabledAsync()).ReturnsAsync(false);
        roastPreferences.Setup(service => service.GetCoolingDurationSecondsAsync()).ReturnsAsync(300);
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetThemePreferenceAsync()).ReturnsAsync(ThemePreference.Dark);
        var roastLevels = new Mock<IRoastLevelService>();
        roastLevels
            .Setup(service => service.GetRoastLevelsAsync())
            .ReturnsAsync(AppDataFactory.CreateDefault().RoastLevels);
        var backups = new Mock<IDataBackupService>();
        backups
            .Setup(service => service.GetSafetyBackupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DataBackupSummary("a", "Automatic", DateTime.Now, DateTime.Now, "1.8.0", 12, 84)]);

        SettingsIndexPageViewModel viewModel = CreateViewModel(
            roastPreferences,
            preferences,
            roastLevels,
            backups,
            SettingsTestFactory.Data(12, 84));

        await viewModel.OnAppearingAsync();

        viewModel.RoastingSummary.Should().Contain("First Crack off").And.Contain("Cooling 5 min");
        viewModel.AppearanceSummary.Should().Be("Dark");
        viewModel.DataSummary.Should().Be("12 beans · 84 roasts · backed up today");
        viewModel.RoastLevelSummary.Should().Be("7 configured");
        viewModel.AboutSummary.Should().Be("Version 9.9.9");
    }

    [Fact]
    public async Task OnAppearing_AfterAPreferenceChanges_ShowsTheNewValueNotTheOldOne()
    {
        var roastPreferences = new Mock<IRoastPreferencesService>();
        roastPreferences.SetupSequence(service => service.GetFirstCrackEnabledAsync())
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        roastPreferences.SetupSequence(service => service.GetCoolingDurationSecondsAsync())
            .ReturnsAsync(300)
            .ReturnsAsync(600);
        SettingsIndexPageViewModel viewModel = CreateViewModel(roastPreferences: roastPreferences);

        await viewModel.OnAppearingAsync();
        string before = viewModel.RoastingSummary;
        await viewModel.OnAppearingAsync();

        before.Should().Contain("First Crack off").And.Contain("Cooling 5 min");
        viewModel.RoastingSummary.Should().Contain("First Crack on").And.Contain("Cooling 10 min");
    }

    [Fact]
    public async Task EachRow_OpensItsOwnRegisteredDetailRoute()
    {
        var navigation = new Mock<INavigationService>();
        SettingsIndexPageViewModel viewModel = CreateViewModel(navigation: navigation);

        await viewModel.OpenRoastingCommand.ExecuteAsync(null);
        await viewModel.OpenAppearanceCommand.ExecuteAsync(null);
        await viewModel.OpenDataCommand.ExecuteAsync(null);
        await viewModel.OpenRoastLevelsCommand.ExecuteAsync(null);
        await viewModel.OpenAboutCommand.ExecuteAsync(null);

        navigation.Verify(service => service.GoToAsync(Routes.RoastingSettings), Times.Once);
        navigation.Verify(service => service.GoToAsync(Routes.AppearanceSettings), Times.Once);
        navigation.Verify(service => service.GoToAsync(Routes.DataSettings), Times.Once);
        navigation.Verify(service => service.GoToAsync(Routes.RoastLevelSettings), Times.Once);
        navigation.Verify(service => service.GoToAsync(Routes.About), Times.Once);
    }

    [Fact]
    public async Task RootBackRoute_ReturnsToRoastTab()
    {
        var navigation = new Mock<INavigationService>();
        SettingsIndexPageViewModel viewModel = CreateViewModel(navigation: navigation);

        await viewModel.GoBackAsync();

        navigation.Verify(service => service.GoToAsync(Routes.Roast), Times.Once);
    }

    [Theory]
    [InlineData(0, 1, "1 bean · 1 roast · no backup yet")]
    [InlineData(1, 0, "1 bean · 1 roast · backed up today")]
    [InlineData(2, 0, "1 bean · 1 roast · backed up yesterday")]
    [InlineData(4, 0, "1 bean · 1 roast · backed up 3 days ago")]
    public void DataSummary_DescribesHowRecentTheLastBackupIs(
        int backupCase,
        int _,
        string expected)
    {
        var now = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Local);
        DateTime? lastBackup = backupCase switch
        {
            0 => null,
            1 => now.AddHours(-2),
            2 => now.AddDays(-1),
            _ => now.AddDays(-3)
        };

        SettingsIndexPageViewModel.DescribeData(1, 1, lastBackup, now).Should().Be(expected);
    }

    private static SettingsIndexPageViewModel CreateViewModel(
        Mock<IRoastPreferencesService>? roastPreferences = null,
        Mock<IPreferencesService>? preferences = null,
        Mock<IRoastLevelService>? roastLevels = null,
        Mock<IDataBackupService>? backups = null,
        AppData? data = null,
        Mock<INavigationService>? navigation = null)
    {
        if (roastPreferences is null)
        {
            roastPreferences = new Mock<IRoastPreferencesService>();
            roastPreferences.Setup(service => service.GetFirstCrackEnabledAsync()).ReturnsAsync(false);
            roastPreferences.Setup(service => service.GetCoolingDurationSecondsAsync()).ReturnsAsync(300);
        }

        if (preferences is null)
        {
            preferences = new Mock<IPreferencesService>();
            preferences.Setup(service => service.GetThemePreferenceAsync()).ReturnsAsync(ThemePreference.Dark);
        }

        if (roastLevels is null)
        {
            roastLevels = new Mock<IRoastLevelService>();
            roastLevels.Setup(service => service.GetRoastLevelsAsync()).ReturnsAsync([]);
        }

        if (backups is null)
        {
            backups = new Mock<IDataBackupService>();
            backups
                .Setup(service => service.GetSafetyBackupsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        var appData = new Mock<IAppDataService>();
        appData.SetupGet(service => service.CurrentData).Returns(data ?? SettingsTestFactory.Data(1, 1));

        return new SettingsIndexPageViewModel(
            roastPreferences.Object,
            preferences.Object,
            appData.Object,
            roastLevels.Object,
            backups.Object,
            navigation?.Object ?? Mock.Of<INavigationService>(),
            new StubVersionProvider());
    }

    private sealed class StubVersionProvider : IAppVersionProvider
    {
        public string VersionString => "9.9.9";
        public string BuildString => "42";
        public string FirstInstalledVersion => "1.0.0";
        public IReadOnlyList<string> VersionHistory => ["1.0.0", "9.9.9"];
    }
}
