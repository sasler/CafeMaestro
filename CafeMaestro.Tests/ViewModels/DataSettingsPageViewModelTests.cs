using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class DataSettingsPageViewModelTests
{
    [Fact]
    public async Task RestoreFromBackupCommand_PreviewsConfirmsRestoresAndCleansTemporaryFile()
    {
        var backupService = new Mock<IDataBackupService>();
        backupService
            .Setup(service => service.PreviewExternalBackupAsync(
                "temporary.json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataBackupSummary(
                "temporary.json",
                "backup.json",
                DateTime.UtcNow,
                DateTime.UtcNow,
                "1.2.0",
                2,
                1));
        backupService
            .Setup(service => service.RestoreExternalBackupAsync(
                "temporary.json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateData(2, 1));
        backupService
            .Setup(service => service.GetSafetyBackupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var userFileService = new Mock<IUserFileService>();
        userFileService
            .Setup(service => service.PickFileAsync(
                UserFileType.Json,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserFileSelection("backup.json", "temporary.json"));
        var alerts = new Mock<IAlertService>();
        alerts
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);
        var viewModel = CreateViewModel(backupService, userFileService, alerts: alerts);

        await viewModel.RestoreFromBackupCommand.ExecuteAsync(null);

        backupService.Verify(
            service => service.RestoreExternalBackupAsync(
                "temporary.json",
                It.IsAny<CancellationToken>()),
            Times.Once);
        userFileService.Verify(
            service => service.DeleteTemporaryFile("temporary.json"),
            Times.Once);
        viewModel.DataSummaryDisplay.Should().Be("Beans: 2  •  Roasts: 1");
    }

    [Fact]
    public async Task RestoreFromBackupCommand_WhenPickerIsCanceled_DoesNothingAndShowsNoError()
    {
        var backupService = new Mock<IDataBackupService>();
        var userFileService = new Mock<IUserFileService>();
        userFileService
            .Setup(service => service.PickFileAsync(
                UserFileType.Json,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserFileSelection?)null);
        var alerts = new Mock<IAlertService>();
        var viewModel = CreateViewModel(backupService, userFileService, alerts: alerts);

        await viewModel.RestoreFromBackupCommand.ExecuteAsync(null);

        backupService.Verify(
            service => service.RestoreExternalBackupAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        alerts.Verify(
            service => service.ShowAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task StartNewDataCommand_WhenConfirmationIsDeclined_PreservesCurrentData()
    {
        var backupService = new Mock<IDataBackupService>();
        var alerts = new Mock<IAlertService>();
        alerts
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(false);
        var viewModel = CreateViewModel(
            backupService,
            new Mock<IUserFileService>(),
            alerts: alerts);

        await viewModel.StartNewDataCommand.ExecuteAsync(null);

        backupService.Verify(
            service => service.StartNewDataAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportCommands_NavigateDirectlyToClearlyNamedImportPages()
    {
        var navigation = new Mock<INavigationService>();
        var viewModel = CreateViewModel(
            new Mock<IDataBackupService>(),
            new Mock<IUserFileService>(),
            navigation: navigation);

        await viewModel.ImportCoffeeBeansCommand.ExecuteAsync(null);
        await viewModel.ImportRoastLogsCommand.ExecuteAsync(null);

        navigation.Verify(service => service.GoToAsync(Routes.BeanImport), Times.Once);
        navigation.Verify(service => service.GoToAsync(Routes.RoastImport), Times.Once);
    }

    private static DataSettingsPageViewModel CreateViewModel(
        Mock<IDataBackupService> backupService,
        Mock<IUserFileService> userFileService,
        Mock<IAlertService>? alerts = null,
        Mock<INavigationService>? navigation = null)
    {
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.CurrentData).Returns(CreateData(1, 0));
        appDataService.SetupGet(service => service.DataFilePath).Returns("cafemaestro_data.json");
        var preferences = new Mock<IPreferencesService>();
        preferences
            .Setup(service => service.GetThemePreferenceAsync())
            .ReturnsAsync(ThemePreference.System);
        var roastLevelService = new Mock<IRoastLevelService>();
        roastLevelService
            .Setup(service => service.GetRoastLevelsAsync())
            .ReturnsAsync([]);

        return new DataSettingsPageViewModel(
            preferences.Object,
            appDataService.Object,
            backupService.Object,
            userFileService.Object,
            Mock.Of<IRoastDataService>(),
            roastLevelService.Object,
            navigation?.Object ?? Mock.Of<INavigationService>(),
            Mock.Of<IShareService>(),
            alerts?.Object ?? Mock.Of<IAlertService>());
    }

    private static AppData CreateData(int beans, int roasts)
    {
        return new AppData
        {
            Beans = Enumerable.Range(0, beans)
                .Select(index => new BeanData
                {
                    CoffeeName = $"Bean {index}",
                    Country = "Test",
                    Quantity = 1,
                    RemainingQuantity = 1
                })
                .ToList(),
            RoastLogs = Enumerable.Range(0, roasts)
                .Select(index => new RoastData
                {
                    BeanType = $"Bean {index}",
                    BatchWeight = 1,
                    Temperature = 200
                })
                .ToList(),
            RoastLevels = AppDataFactory.CreateDefault().RoastLevels
        };
    }
}
