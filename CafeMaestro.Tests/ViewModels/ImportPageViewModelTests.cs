using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class ImportPageViewModelTests : IDisposable
{
    private const string BeanCsv = "Coffee Name,Country,Quantity\nYirgacheffe,Ethiopia,1.5\nHuila,Colombia,2\n,Kenya,1\n";
    private const string RoastCsv = "Date,Coffee Bean,Batch Weight,Time\n2026-03-01,Kenya AA,220,11:30\n2026-03-02,,200,10:00\n";

    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public ImportPageViewModelTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    // ------------------------------------------------------------------ contextual entry

    [Theory]
    [InlineData(ImportKind.Beans)]
    [InlineData(ImportKind.Roasts)]
    public void ApplyQueryAttributes_PreselectsTheKindWithoutAnExtraStep(ImportKind kind)
    {
        Harness harness = CreateHarness();

        harness.ViewModel.ApplyQueryAttributes(
            new Dictionary<string, object> { [ImportPageViewModel.KindParameter] = kind });

        harness.ViewModel.Kind.Should().Be(kind);
        harness.ViewModel.Step.Should().Be(ImportStep.SelectFile);
        harness.ViewModel.Descriptor.Kind.Should().Be(kind);
    }

    [Fact]
    public void ApplyQueryAttributes_AcceptsTheKindAsAQueryString()
    {
        Harness harness = CreateHarness();

        harness.ViewModel.ApplyQueryAttributes(
            new Dictionary<string, object> { [ImportPageViewModel.KindParameter] = "Roasts" });

        harness.ViewModel.Kind.Should().Be(ImportKind.Roasts);
    }

    // ------------------------------------------------------------------ file selection

    [Fact]
    public async Task BrowseCommand_LoadsHeadersAutoMapsAndOpensTheMappingStep()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);

        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        harness.ViewModel.Step.Should().Be(ImportStep.MapColumns);
        harness.ViewModel.FileDisplayName.Should().Be("beans.csv");
        harness.ViewModel.Headers.Should().Contain(["Coffee Name", "Country", "Quantity"]);
        harness.ViewModel.HasMissingRequiredMappings.Should().BeFalse();
        harness.ViewModel.RequiredMappings.Single(mapping => mapping.PropertyKey == "CoffeeName")
            .SelectedHeader.Should().Be("Coffee Name");
        harness.ViewModel.OptionalMappings.Single(mapping => mapping.PropertyKey == "Quantity")
            .SelectedHeader.Should().Be("Quantity");
    }

    [Fact]
    public async Task BrowseCommand_WithNoHeaderRow_ExplainsTheProblemAndStaysOnFileSelection()
    {
        Harness harness = CreateHarness();
        harness.StageFile("empty.csv", string.Empty);

        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        harness.ViewModel.Step.Should().Be(ImportStep.SelectFile);
        harness.ViewModel.HasFileError.Should().BeTrue();
        harness.ViewModel.FileErrorMessage.Should().Contain("no header row");
    }

    [Fact]
    public async Task BrowseCommand_WithHeadersButNoRows_ExplainsTheProblem()
    {
        Harness harness = CreateHarness();
        harness.StageFile("headers.csv", "Coffee Name,Country\n");

        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        harness.ViewModel.Step.Should().Be(ImportStep.SelectFile);
        harness.ViewModel.FileErrorMessage.Should().Contain("no data rows");
        harness.ViewModel.ContinueToMappingCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task BrowseCommand_WhenTheFileCannotBeRead_ReportsItAndClearsTheSelection()
    {
        Harness harness = CreateHarness();
        harness.UserFileService
            .Setup(service => service.PickFileAsync(UserFileType.Csv, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserFileSelection("gone.csv", Path.Combine(_testDirectory, "gone.csv")));

        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        harness.ViewModel.HasFile.Should().BeFalse();
        harness.ViewModel.HasFileError.Should().BeTrue();
        harness.Alerts.Verify(
            service => service.ShowAlertAsync("File error", It.IsAny<string>(), "OK"),
            Times.Once);
    }

    [Fact]
    public async Task BrowseCommand_WhenThePickerIsDismissed_KeepsTheFlowUnchanged()
    {
        Harness harness = CreateHarness();
        harness.UserFileService
            .Setup(service => service.PickFileAsync(UserFileType.Csv, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserFileSelection?)null);

        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        harness.ViewModel.HasFile.Should().BeFalse();
        harness.ViewModel.StatusMessage.Should().Be("No file selected.");
        harness.ViewModel.Step.Should().Be(ImportStep.SelectFile);
    }

    [Fact]
    public async Task BrowseCommand_WhenSelectionIsCancelled_ReportsCancellationWithoutAnError()
    {
        Harness harness = CreateHarness();
        harness.UserFileService
            .Setup(service => service.PickFileAsync(UserFileType.Csv, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        harness.ViewModel.StatusMessage.Should().Contain("cancelled");
        harness.ViewModel.HasFileError.Should().BeFalse();
        harness.Alerts.Verify(
            service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ------------------------------------------------------------------ mapping

    [Fact]
    public async Task ReviewCommand_IsBlockedUntilEveryRequiredFieldIsMapped()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        harness.ViewModel.RequiredMappings.Single(mapping => mapping.PropertyKey == "Country")
            .SelectedHeader = ImportHeaderMatcher.NoneOption;

        harness.ViewModel.HasMissingRequiredMappings.Should().BeTrue();
        harness.ViewModel.MissingRequiredSummary.Should().Contain("1 required field");
        harness.ViewModel.ReviewCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SelectKindCommand_SwitchesTheDestinationRulesButKeepsTheChosenFile()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        harness.ViewModel.SelectKindCommand.Execute(ImportKind.Roasts);

        harness.ViewModel.Kind.Should().Be(ImportKind.Roasts);
        harness.ViewModel.HasFile.Should().BeTrue();
        harness.ViewModel.RequiredMappings.Select(mapping => mapping.PropertyKey)
            .Should().BeEquivalentTo(["RoastDate", "BeanType", "BatchWeight"]);
    }

    // ------------------------------------------------------------------ review

    [Fact]
    public async Task ReviewCommand_ReportsValidInvalidAndTotalCountsBeforeAnythingIsWritten()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        await harness.ViewModel.ReviewCommand.ExecuteAsync(null);

        harness.ViewModel.Step.Should().Be(ImportStep.Review);
        harness.ViewModel.ValidRowCount.Should().Be(2);
        harness.ViewModel.InvalidRowCount.Should().Be(1);
        harness.ViewModel.TotalRowCount.Should().Be(3);
        harness.ViewModel.PreviewRows.Should().HaveCount(2);
        harness.ViewModel.RejectedRows.Should().ContainSingle()
            .Which.Detail.Should().Contain("Coffee name is required");
        harness.ViewModel.ImportActionLabel.Should().Be("IMPORT 2 VALID BEANS");
        harness.AppData.Verify(
            service => service.UpdateAsync(It.IsAny<Action<AppData>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BackToMappingCommand_KeepsTheFileAndTheMappingIntact()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        await harness.ViewModel.ReviewCommand.ExecuteAsync(null);

        harness.ViewModel.BackToMappingCommand.Execute(null);

        harness.ViewModel.Step.Should().Be(ImportStep.MapColumns);
        harness.ViewModel.FileDisplayName.Should().Be("beans.csv");
        harness.ViewModel.RequiredMappings.Single(mapping => mapping.PropertyKey == "Country")
            .SelectedHeader.Should().Be("Country");
    }

    // ------------------------------------------------------------------ import

    [Fact]
    public async Task ImportCommand_WritesOnlyTheReviewedValidRowsAndReportsTheRejectedOnes()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        await harness.ViewModel.ReviewCommand.ExecuteAsync(null);

        await harness.ViewModel.ImportCommand.ExecuteAsync(null);

        harness.ViewModel.Step.Should().Be(ImportStep.Result);
        harness.ViewModel.ImportSucceeded.Should().BeTrue();
        harness.ViewModel.ImportedCount.Should().Be(2);
        harness.ViewModel.SkippedCount.Should().Be(1);
        harness.ViewModel.ResultErrors.Should().ContainSingle();
        harness.ViewModel.DestinationActionLabel.Should().Be("VIEW BEANS");
        harness.AppData.Verify(
            service => service.UpdateAsync(It.IsAny<Action<AppData>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Data.Beans.Select(bean => bean.CoffeeName).Should().Equal("Yirgacheffe", "Huila");
    }

    [Fact]
    public async Task ImportCommand_ForRoasts_UsesTheRoastRulesAndDestination()
    {
        Harness harness = CreateHarness();
        harness.ViewModel.ApplyQueryAttributes(
            new Dictionary<string, object> { [ImportPageViewModel.KindParameter] = ImportKind.Roasts });
        harness.StageFile("roasts.csv", RoastCsv);

        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        await harness.ViewModel.ReviewCommand.ExecuteAsync(null);
        await harness.ViewModel.ImportCommand.ExecuteAsync(null);

        harness.ViewModel.ImportedCount.Should().Be(1);
        harness.ViewModel.SkippedCount.Should().Be(1);
        harness.ViewModel.DestinationActionLabel.Should().Be("VIEW ROAST LOG");
        harness.Data.RoastLogs.Should().ContainSingle()
            .Which.CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
    }

    [Fact]
    public async Task ImportCommand_WhenTheCommitIsRefused_KeepsTheReviewedPlanAndOffersRetry()
    {
        Harness harness = CreateHarness(commitSucceeds: false);
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        await harness.ViewModel.ReviewCommand.ExecuteAsync(null);

        await harness.ViewModel.ImportCommand.ExecuteAsync(null);

        harness.ViewModel.Step.Should().Be(ImportStep.Result);
        harness.ViewModel.ImportFailed.Should().BeTrue();
        harness.ViewModel.ImportedCount.Should().Be(0);
        harness.ViewModel.ResultDetail.Should().Contain("still here");
        harness.ViewModel.RetryCommand.CanExecute(null).Should().BeTrue();
        harness.Data.Beans.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenDestinationCommand_NavigatesToTheImportedSurface()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        await harness.ViewModel.ReviewCommand.ExecuteAsync(null);
        await harness.ViewModel.ImportCommand.ExecuteAsync(null);

        await harness.ViewModel.OpenDestinationCommand.ExecuteAsync(null);

        harness.Navigation.Verify(service => service.GoToAsync(Routes.BeanInventory), Times.Once);
    }

    [Fact]
    public async Task ResultActions_StayHiddenUntilTheResultStep()
    {
        // ImportFailed is just "not yet succeeded", so it is true from the start; only the step
        // may reveal Retry, or it would sit beside every earlier step's primary action.
        Harness harness = CreateHarness();
        harness.ViewModel.ImportFailed.Should().BeTrue();
        harness.ViewModel.ShowRetryAction.Should().BeFalse();
        harness.ViewModel.ShowDestinationAction.Should().BeFalse();

        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        harness.ViewModel.ShowRetryAction.Should().BeFalse();

        await harness.ViewModel.ReviewCommand.ExecuteAsync(null);
        harness.ViewModel.ShowRetryAction.Should().BeFalse();

        await harness.ViewModel.ImportCommand.ExecuteAsync(null);
        harness.ViewModel.ShowRetryAction.Should().BeFalse();
        harness.ViewModel.ShowDestinationAction.Should().BeTrue();
    }

    [Fact]
    public async Task ShowRetryAction_IsRevealedOnlyByAFailedResult()
    {
        Harness harness = CreateHarness(commitSucceeds: false);
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        await harness.ViewModel.ReviewCommand.ExecuteAsync(null);

        await harness.ViewModel.ImportCommand.ExecuteAsync(null);

        harness.ViewModel.ShowRetryAction.Should().BeTrue();
        harness.ViewModel.ShowDestinationAction.Should().BeFalse();
    }

    // ------------------------------------------------------------------ abandoning the flow

    [Fact]
    public async Task CancelCommand_AfterOnlyChoosingAFile_LeavesWithoutAskingAnything()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);

        await harness.ViewModel.CancelCommand.ExecuteAsync(null);

        harness.Alerts.Verify(
            service => service.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        harness.Navigation.Verify(service => service.GoBackAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelCommand_AfterEditingMappings_ConfirmsAndStaysWhenDeclined()
    {
        Harness harness = CreateHarness();
        harness.Alerts
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        harness.ViewModel.OptionalMappings.Single(mapping => mapping.PropertyKey == "Notes")
            .SelectedHeader = "Country";

        await harness.ViewModel.CancelCommand.ExecuteAsync(null);

        harness.Navigation.Verify(service => service.GoBackAsync(), Times.Never);
        harness.ViewModel.FileDisplayName.Should().Be("beans.csv");
    }

    [Fact]
    public async Task OnDisappearing_AfterLeaving_ReleasesTheWorkingCopyOfTheSource()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        string localPath = harness.ViewModel.FilePath;

        await harness.ViewModel.CancelCommand.ExecuteAsync(null);
        harness.ViewModel.OnDisappearing();

        harness.UserFileService.Verify(service => service.DeleteTemporaryFile(localPath), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OnDisappearing_WhileStillInTheFlow_KeepsTheSelectedFile()
    {
        Harness harness = CreateHarness();
        harness.StageFile("beans.csv", BeanCsv);
        await harness.ViewModel.BrowseCommand.ExecuteAsync(null);
        string localPath = harness.ViewModel.FilePath;

        harness.ViewModel.OnDisappearing();

        harness.UserFileService.Verify(service => service.DeleteTemporaryFile(localPath), Times.Never);
        harness.ViewModel.FileDisplayName.Should().Be("beans.csv");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    // ------------------------------------------------------------------ harness

    private Harness CreateHarness(bool commitSucceeds = true)
    {
        var data = new AppData();
        var appData = new Mock<IAppDataService>();
        appData.Setup(service => service.LoadAppDataAsync()).ReturnsAsync(data);
        appData.Setup(service => service.UpdateAsync(It.IsAny<Action<AppData>>(), It.IsAny<CancellationToken>()))
            .Returns((Action<AppData> mutation, CancellationToken _) =>
            {
                if (!commitSucceeds)
                {
                    return Task.FromResult(false);
                }

                mutation(data);
                return Task.FromResult(true);
            });

        var roastLevels = new Mock<IRoastLevelService>();
        roastLevels.Setup(service => service.GetRoastLevelsAsync())
            .ReturnsAsync([new RoastLevelData("Medium", 0, 100)]);

        var importService = new ImportService(
            new CsvParserService(),
            appData.Object,
            [new BeanImportAdapter(appData.Object), new RoastImportAdapter(appData.Object, roastLevels.Object)]);

        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);
        navigation.Setup(service => service.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var alerts = new Mock<IAlertService>();
        alerts.Setup(service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        alerts.Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var userFileService = new Mock<IUserFileService>();

        return new Harness(
            new ImportPageViewModel(importService, navigation.Object, alerts.Object, userFileService.Object),
            data,
            appData,
            navigation,
            alerts,
            userFileService,
            _testDirectory);
    }

    private sealed record Harness(
        ImportPageViewModel ViewModel,
        AppData Data,
        Mock<IAppDataService> AppData,
        Mock<INavigationService> Navigation,
        Mock<IAlertService> Alerts,
        Mock<IUserFileService> UserFileService,
        string Directory)
    {
        /// <summary>Writes a CSV the picker will return, mirroring the read-only working copy.</summary>
        public void StageFile(string fileName, string content)
        {
            string path = Path.Combine(Directory, fileName);
            File.WriteAllText(path, content);
            UserFileService
                .Setup(service => service.PickFileAsync(UserFileType.Csv, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserFileSelection(fileName, path));
        }
    }
}
