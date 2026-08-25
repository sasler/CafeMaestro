using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class DataSettingsBusyStateTests
{
    [Fact]
    public void DataCommands_WhenFileOperationIsBusy_AreDisabled()
    {
        var appData = new Mock<IAppDataService>();
        appData.SetupGet(service => service.CurrentData).Returns(AppDataFactory.CreateDefault());
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetThemePreferenceAsync()).ReturnsAsync(ThemePreference.System);
        var viewModel = new DataSettingsPageViewModel(
            preferences.Object,
            appData.Object,
            Mock.Of<IDataBackupService>(),
            Mock.Of<IUserFileService>(),
            Mock.Of<IRoastDataService>(),
            Mock.Of<IRoastLevelService>(),
            Mock.Of<INavigationService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IAlertService>())
        {
            IsDataOperationInProgress = true
        };

        viewModel.SaveBackupCommand.CanExecute(null).Should().BeFalse();
        viewModel.RestoreFromBackupCommand.CanExecute(null).Should().BeFalse();
        viewModel.SaveRecoveryCopyCommand.CanExecute(null).Should().BeFalse();
        viewModel.ImportCoffeeBeansCommand.CanExecute(null).Should().BeFalse();
        viewModel.ImportRoastLogsCommand.CanExecute(null).Should().BeFalse();
        viewModel.ExportRoastLogCsvCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SaveRecoveryCopyCommand_WhenBusyStateChanges_RaisesCanExecuteChanged()
    {
        var appData = new Mock<IAppDataService>();
        appData.SetupGet(service => service.CurrentData).Returns(AppDataFactory.CreateDefault());
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetThemePreferenceAsync()).ReturnsAsync(ThemePreference.System);
        var viewModel = new DataSettingsPageViewModel(
            preferences.Object,
            appData.Object,
            Mock.Of<IDataBackupService>(),
            Mock.Of<IUserFileService>(),
            Mock.Of<IRoastDataService>(),
            Mock.Of<IRoastLevelService>(),
            Mock.Of<INavigationService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IAlertService>());
        int notificationCount = 0;
        viewModel.SaveRecoveryCopyCommand.CanExecuteChanged += (_, _) => notificationCount++;

        viewModel.IsDataOperationInProgress = true;

        notificationCount.Should().Be(1);
        viewModel.SaveRecoveryCopyCommand.CanExecute(null).Should().BeFalse();
    }
}
