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
    public async Task SaveRecoveryCopyCommand_ExportsRawArtifactWithoutRestoringIt()
    {
        byte[] raw = Encoding.UTF8.GetBytes("{ invalid but preserved }");
        var backupService = new Mock<IDataBackupService>();
        backupService
            .Setup(service => service.CreateSafetyBackupExportStreamAsync(
                "raw.json",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(raw));
        var userFiles = new Mock<IUserFileService>();
        userFiles
            .Setup(service => service.SaveFileAsync(
                It.IsAny<string>(),
                "application/json",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentSaveResult(true, false));
        DataSettingsPageViewModel viewModel = CreateViewModel(backupService, userFiles);
        var recovery = new DataBackupSummary(
            "raw.json",
            "Raw recovery copy",
            DateTime.Now,
            DateTime.UtcNow,
            "Unvalidated",
            0,
            0,
            IsRestorable: false);

        await viewModel.SaveRecoveryCopyCommand.ExecuteAsync(recovery);

        backupService.Verify(
            service => service.RestoreSafetyBackupAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        userFiles.Verify(
            service => service.SaveFileAsync(
                It.Is<string>(name => name.Contains("Raw_Recovery", StringComparison.Ordinal)),
                "application/json",
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
    public async Task OnAppearingAsync_WhenCanonicalNeedsRecovery_ShowsRecoveryStatus()
    {
        var backupService = new Mock<IDataBackupService>();
        backupService
            .Setup(service => service.GetSafetyBackupsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        DataSettingsPageViewModel viewModel = CreateViewModel(
            backupService,
            new Mock<IUserFileService>(),
            recoveryRequired: true);

        await viewModel.OnAppearingAsync();

        viewModel.DataStatusDisplay.Should().Be("Recovery required");
        viewModel.DataSummaryDisplay.Should().Contain("could not be loaded");
        viewModel.LastModifiedDisplay.Should().Contain("Share Backup");
    }

    private static DataSettingsPageViewModel CreateViewModel(
        Mock<IDataBackupService> backupService,
        Mock<IUserFileService> userFiles,
        Mock<IAlertService>? alerts = null,
        AppData? currentData = null,
        bool recoveryRequired = false,
        Mock<IRoastSessionService>? session = null)
    {
        var appData = new Mock<IAppDataService>();
        appData.SetupGet(service => service.CurrentData).Returns(currentData ?? CreateData(1, 0));
        appData.SetupGet(service => service.DataFilePath).Returns("cafemaestro_data.json");
        appData.SetupGet(service => service.IsRecoveryRequired).Returns(recoveryRequired);

        return new DataSettingsPageViewModel(
            appData.Object,
            backupService.Object,
            userFiles.Object,
            Mock.Of<IRoastDataService>(),
            (session ?? SettingsTestFactory.IdleSession()).Object,
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
