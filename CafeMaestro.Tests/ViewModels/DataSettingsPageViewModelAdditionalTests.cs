using System.Text;
using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class DataSettingsPageViewModelAdditionalTests
{
    [Fact]
    public async Task RestoreFromBackupCommand_WhenConfirmationIsDeclined_DoesNotReplaceDataAndCleansCache()
    {
        var backupService = new Mock<IDataBackupService>();
        backupService
            .Setup(service => service.PreviewExternalBackupAsync(
                "cached.json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSummary("cached.json"));
        var userFiles = new Mock<IUserFileService>();
        userFiles
            .Setup(service => service.PickFileAsync(
                UserFileType.Json,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserFileSelection("backup.json", "cached.json"));
        var alerts = new Mock<IAlertService>();
        alerts
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(false);
        DataSettingsPageViewModel viewModel =
            CreateViewModel(backupService, userFiles, alerts: alerts);

        await viewModel.RestoreFromBackupCommand.ExecuteAsync(null);

        backupService.Verify(
            service => service.RestoreExternalBackupAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        userFiles.Verify(service => service.DeleteTemporaryFile("cached.json"), Times.Once);
    }

    [Fact]
    public async Task SaveBackupCommand_WhenSaveAsIsCanceled_ShowsNoErrorOrSuccess()
    {
        var backupService = new Mock<IDataBackupService>();
        backupService
            .Setup(service => service.CreateExportStreamAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("{}")));
        var userFiles = new Mock<IUserFileService>();
        userFiles
            .Setup(service => service.SaveFileAsync(
                It.IsAny<string>(),
                "application/json",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSaveResult(false, true));
        var alerts = new Mock<IAlertService>();
        DataSettingsPageViewModel viewModel =
            CreateViewModel(backupService, userFiles, alerts: alerts);

        await viewModel.SaveBackupCommand.ExecuteAsync(null);

        alerts.Verify(
            service => service.ShowAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task StartNewDataCommand_WhenConfirmed_RefreshesStatusAndSafetyHistory()
    {
        var backupService = new Mock<IDataBackupService>();
        backupService
            .Setup(service => service.StartNewDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateData(0, 0));
        backupService
            .Setup(service => service.GetSafetyBackupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateSummary("safety.json")]);
        var alerts = new Mock<IAlertService>();
        alerts
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);
        DataSettingsPageViewModel viewModel = CreateViewModel(
            backupService,
            new Mock<IUserFileService>(),
            alerts: alerts);

        await viewModel.StartNewDataCommand.ExecuteAsync(null);

        viewModel.DataSummaryDisplay.Should().Be("Beans: 0  •  Roasts: 0");
        viewModel.AutomaticBackups.Should().ContainSingle();
        viewModel.HasAutomaticBackups.Should().BeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_ShowsCurrentCountsAndFiveItemSafetyHistory()
    {
        var backupService = new Mock<IDataBackupService>();
        backupService
            .Setup(service => service.GetSafetyBackupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 5)
                .Select(index => CreateSummary($"safety-{index}.json"))
                .ToList());
        DataSettingsPageViewModel viewModel = CreateViewModel(
            backupService,
            new Mock<IUserFileService>(),
            currentData: CreateData(2, 1));

        await viewModel.OnAppearingAsync();

        viewModel.DataStatusDisplay.Should().Be("Saved automatically on this device");
        viewModel.DataSummaryDisplay.Should().Be("Beans: 2  •  Roasts: 1");
        viewModel.AutomaticBackups.Should().HaveCount(5);
    }

    [Fact]
    public async Task ResetRoastLevelsToDefaultsCommand_WhenConfirmed_SavesDefaultLevels()
    {
        var roastLevels = new Mock<IRoastLevelService>();
        roastLevels
            .Setup(service => service.SaveRoastLevelsAsync(It.IsAny<List<RoastLevelData>>()))
            .ReturnsAsync(true);
        roastLevels.Setup(service => service.GetRoastLevelsAsync()).ReturnsAsync([]);
        var alerts = new Mock<IAlertService>();
        alerts
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);
        DataSettingsPageViewModel viewModel = CreateViewModel(
            new Mock<IDataBackupService>(),
            new Mock<IUserFileService>(),
            roastLevels: roastLevels,
            alerts: alerts);

        await viewModel.ResetRoastLevelsToDefaultsCommand.ExecuteAsync(null);

        roastLevels.Verify(
            service => service.SaveRoastLevelsAsync(
                It.Is<List<RoastLevelData>>(levels => levels.Count == 7)),
            Times.Once);
    }

    [Fact]
    public async Task SaveRoastLevelCommand_WithInvalidRange_DoesNotPersist()
    {
        var roastLevels = new Mock<IRoastLevelService>();
        var alerts = new Mock<IAlertService>();
        DataSettingsPageViewModel viewModel = CreateViewModel(
            new Mock<IDataBackupService>(),
            new Mock<IUserFileService>(),
            roastLevels: roastLevels,
            alerts: alerts);
        viewModel.AddRoastLevelCommand.Execute(null);
        viewModel.RoastLevelName = "Invalid";
        viewModel.MinWeightLossText = "15.0";
        viewModel.MaxWeightLossText = "10.0";

        await viewModel.SaveRoastLevelCommand.ExecuteAsync(null);

        roastLevels.Verify(
            service => service.AddRoastLevelAsync(It.IsAny<RoastLevelData>()),
            Times.Never);
        alerts.Verify(
            service => service.ShowAlertAsync(
                "Invalid Roast Level",
                It.IsAny<string>(),
                "OK"),
            Times.Once);
    }

    private static DataSettingsPageViewModel CreateViewModel(
        Mock<IDataBackupService> backupService,
        Mock<IUserFileService> userFiles,
        Mock<IRoastLevelService>? roastLevels = null,
        Mock<IAlertService>? alerts = null,
        AppData? currentData = null)
    {
        var appData = new Mock<IAppDataService>();
        appData.SetupGet(service => service.CurrentData).Returns(currentData ?? CreateData(1, 0));
        appData.SetupGet(service => service.DataFilePath).Returns("cafemaestro_data.json");
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetThemePreferenceAsync()).ReturnsAsync(ThemePreference.System);
        roastLevels ??= new Mock<IRoastLevelService>();
        roastLevels.Setup(service => service.GetRoastLevelsAsync()).ReturnsAsync([]);

        return new DataSettingsPageViewModel(
            preferences.Object,
            appData.Object,
            backupService.Object,
            userFiles.Object,
            Mock.Of<IRoastDataService>(),
            roastLevels.Object,
            Mock.Of<INavigationService>(),
            Mock.Of<IShareService>(),
            alerts?.Object ?? Mock.Of<IAlertService>());
    }

    private static DataBackupSummary CreateSummary(string id) =>
        new(id, "Automatic safety backup", DateTime.Now, DateTime.UtcNow, "1.2.0", 1, 0);

    private static AppData CreateData(int beans, int roasts) =>
        new()
        {
            LastModified = DateTime.UtcNow,
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
