using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class RoastLogPageViewModelTests
{
    [Fact]
    public async Task RefreshAndSearchCommands_FilterOpenWorkAndHistory()
    {
        DateTimeOffset now = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        var history = new List<RoastData>
        {
            Unweighed("Ethiopia", now.AddDays(-1)),
            Complete("Brazil", now.AddDays(-2))
        };
        var queryService = new Mock<IRoastQueryService>();
        queryService.Setup(service => service.GetOpenWorkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Work("Brazil reserve", now, RoastEffectiveStatus.Cooling)]);
        queryService.Setup(service => service.GetHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);
        var viewModel = CreateViewModel(queryService: queryService);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.OpenWork.Should().ContainSingle();
        viewModel.History.Select(roast => roast.BeanDisplay).Should().ContainInOrder("Ethiopia", "Brazil");
        viewModel.RecordCount.Should().Be(3);

        viewModel.SearchText = "braz";
        await viewModel.SearchCommand.ExecuteAsync(null);

        viewModel.OpenWork.Should().ContainSingle();
        viewModel.History.Should().ContainSingle().Which.BeanDisplay.Should().Be("Brazil");

        viewModel.SearchText = string.Empty;
        viewModel.SelectFilterCommand.Execute(RoastLogFilter.Unweighed);
        viewModel.OpenWork.Should().BeEmpty();
        viewModel.History.Should().ContainSingle().Which.BeanDisplay.Should().Be("Ethiopia");
    }

    [Fact]
    public async Task OpenDetailCommand_NavigatesToFocusedDetailRoute()
    {
        var roast = new RoastData
        {
            Id = Guid.NewGuid(),
            BeanType = "Kenya",
            RoastDate = DateTime.Today,
            BatchWeight = 200,
            FinalWeight = 168,
            RoastMinutes = 12,
            RoastSeconds = 5,
            Temperature = 215
        };

        var navigationService = new Mock<INavigationService>();
        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);

        var viewModel = CreateViewModel(navigationService: navigationService);

        await viewModel.OpenDetailCommand.ExecuteAsync(RoastLogCard.FromHistory(roast));

        navigationService.Verify(
            service => service.GoToAsync(
                Routes.RoastDetail,
                It.Is<IDictionary<string, object>>(parameters => parameters["RoastId"].ToString() == roast.Id.ToString())),
            Times.Once);
    }

    [Fact]
    public async Task SearchText_DebouncesToLatestTermWithoutReloadingStorage()
    {
        var queryService = new Mock<IRoastQueryService>();
        queryService.Setup(service => service.GetOpenWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        queryService.Setup(service => service.GetHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Complete("Brazil", DateTimeOffset.UtcNow), Unweighed("Ethiopia", DateTimeOffset.UtcNow)]);
        var viewModel = CreateViewModel(queryService: queryService);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.SearchText = "braz";
        viewModel.SearchText = "eth";
        await Task.Delay(300);

        viewModel.History.Should().ContainSingle().Which.BeanDisplay.Should().Be("Ethiopia");
        queryService.Verify(service => service.GetHistoryAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TimeProjection_WithNoOpenWork_DoesNotQueryOrReplaceHistory()
    {
        var queryService = new Mock<IRoastQueryService>();
        queryService.Setup(service => service.GetOpenWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        queryService.Setup(service => service.GetHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Complete("Brazil", DateTimeOffset.UtcNow)]);
        var viewModel = CreateViewModel(queryService: queryService);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        var history = viewModel.History;

        await viewModel.RefreshTimeProjectionAsync();

        viewModel.History.Should().BeSameAs(history);
        queryService.Verify(service => service.GetOpenWorkAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WeighCommand_WithMultipleReadyBatches_RequiresExplicitSelection()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RoastWorkItem first = Work("Guji", now.AddMinutes(-10), RoastEffectiveStatus.NeedsWeight, batch: 1);
        RoastWorkItem second = Work("Guji", now.AddMinutes(-5), RoastEffectiveStatus.NeedsWeight, batch: 2);
        var queryService = new Mock<IRoastQueryService>();
        queryService.Setup(service => service.GetOpenWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        queryService.Setup(service => service.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var overlay = new Mock<IOverlayService>();
        overlay.Setup(service => service.ChooseBatchAsync(It.IsAny<IReadOnlyList<BatchChoice>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchChoiceOutcome(new BatchChoice
            {
                RoastId = second.RoastId,
                BatchNumber = second.BatchNumber,
                BeanDisplaySnapshot = second.BeanDisplaySnapshot,
                BatchWeight = second.BatchWeight,
                DroppedAtUtc = second.DroppedAtUtc,
                TotalSeconds = second.TotalSeconds
            }));
        overlay.Setup(service => service.ShowWeighInAsync(It.IsAny<WeighInRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WeighInOutcome.Cancelled);
        var viewModel = CreateViewModel(queryService: queryService, overlayService: overlay);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.WeighCommand.ExecuteAsync(viewModel.OpenWork[0]);

        overlay.Verify(service => service.ChooseBatchAsync(
            It.Is<IReadOnlyList<BatchChoice>>(choices => choices.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        overlay.Verify(service => service.ShowWeighInAsync(
            It.Is<WeighInRequest>(request => request.RoastId == second.RoastId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnAppearingAndDataChanged_RefreshesProjectionsWhileVisibleOnly()
    {
        var initialRoast = new RoastData
        {
            Id = Guid.NewGuid(),
            BeanType = "Initial",
            RoastDate = new DateTime(2025, 1, 1),
            BatchWeight = 200,
            FinalWeight = 170,
            RoastMinutes = 10,
            RoastSeconds = 0,
            Temperature = 210
        };

        var updatedAppData = new AppData
        {
            Beans = [],
            RoastLogs =
            [
                new RoastData
                {
                    Id = Guid.NewGuid(),
                    BeanType = "Updated",
                    RoastDate = new DateTime(2025, 2, 1),
                    BatchWeight = 200,
                    FinalWeight = 168,
                    RoastMinutes = 11,
                    RoastSeconds = 30,
                    Temperature = 214
                }
            ]
        };

        var queryService = new Mock<IRoastQueryService>();
        queryService.Setup(service => service.GetOpenWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        queryService.SetupSequence(service => service.GetHistoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([initialRoast])
            .ReturnsAsync(updatedAppData.RoastLogs);

        var appDataService = CreateAppDataServiceMock();
        var viewModel = CreateViewModel(queryService: queryService, appDataService: appDataService);

        await viewModel.OnAppearingAsync();

        appDataService.Raise(service => service.DataChanged += null, appDataService.Object, updatedAppData);

        await viewModel.LastRefreshTask;
        viewModel.History.Should().ContainSingle();
        viewModel.History.Single().BeanDisplay.Should().Be("Updated");

        viewModel.OnDisappearing();
        appDataService.Raise(service => service.DataChanged += null, appDataService.Object, new AppData
        {
            Beans = [],
            RoastLogs =
            [
                new RoastData
                {
                    Id = Guid.NewGuid(),
                    BeanType = "Ignored",
                    RoastDate = DateTime.Today,
                    BatchWeight = 200,
                    FinalWeight = 170,
                    RoastMinutes = 10,
                    RoastSeconds = 0,
                    Temperature = 210
                }
            ]
        });

        viewModel.History.Single().BeanDisplay.Should().Be("Updated");
    }

    [Fact]
    public void SearchText_RaisesPropertyChangedNotification()
    {
        var viewModel = CreateViewModel();
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        viewModel.SearchText = "Ethiopia";

        changedProperties.Should().Contain(nameof(RoastLogPageViewModel.SearchText));
    }

    [Fact]
    public async Task DeleteRoastCommand_DeletesOnlyAfterAnIdentifyingConfirmation()
    {
        RoastData roast = Complete("Kenya", new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero));
        RoastLogCard card = RoastLogCard.FromHistory(roast);
        var roastService = new Mock<IRoastDataService>();
        roastService.Setup(service => service.DeleteRoastLogAsync(roast.Id)).ReturnsAsync(true);
        var alertService = new Mock<IAlertService>();
        alertService.SetupSequence(service => service.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        var viewModel = CreateViewModel(roastService: roastService, alertService: alertService);

        await viewModel.DeleteRoastCommand.ExecuteAsync(card);

        roastService.Verify(service => service.DeleteRoastLogAsync(It.IsAny<Guid>()), Times.Never);

        await viewModel.DeleteRoastCommand.ExecuteAsync(card);

        roastService.Verify(service => service.DeleteRoastLogAsync(roast.Id), Times.Once);
        alertService.Verify(service => service.ShowConfirmationAsync(
            "Delete roast?",
            It.Is<string>(message => message.Contains("Kenya") && message.Contains(card.DateDisplay)),
            "Delete",
            "Cancel"), Times.Exactly(2));
    }

    private static RoastLogPageViewModel CreateViewModel(
        Mock<IRoastDataService>? roastService = null,
        Mock<IRoastQueryService>? queryService = null,
        Mock<IAppDataService>? appDataService = null,
        Mock<INavigationService>? navigationService = null,
        Mock<IOverlayService>? overlayService = null,
        Mock<IAlertService>? alertService = null,
        Mock<IUserFileService>? userFileService = null)
    {
        roastService ??= new Mock<IRoastDataService>();
        if (queryService is null)
        {
            queryService = new Mock<IRoastQueryService>();
            queryService.Setup(service => service.GetOpenWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
            queryService.Setup(service => service.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        }
        appDataService ??= CreateAppDataServiceMock();
        navigationService ??= new Mock<INavigationService>();
        overlayService ??= new Mock<IOverlayService>();
        alertService ??= new Mock<IAlertService>();
        userFileService ??= new Mock<IUserFileService>();

        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);

        return new RoastLogPageViewModel(
            roastService.Object,
            queryService.Object,
            appDataService.Object,
            navigationService.Object,
            overlayService.Object,
            alertService.Object,
            userFileService.Object);
    }

    private static Mock<IAppDataService> CreateAppDataServiceMock()
    {
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.DataFilePath).Returns(@"C:\data\cafemaestro_data.json");
        return appDataService;
    }

    private static RoastWorkItem Work(
        string bean,
        DateTimeOffset droppedAt,
        RoastEffectiveStatus status,
        int batch = 1) => new()
        {
            RoastId = Guid.NewGuid(),
            BeanDisplaySnapshot = bean,
            BatchNumber = batch,
            BatchWeight = 240,
            Temperature = 218,
            DroppedAtUtc = droppedAt,
            ReadyToWeighAtUtc = droppedAt.AddMinutes(5),
            RemainingCoolingSeconds = status == RoastEffectiveStatus.Cooling ? 120 : 0,
            Status = status,
            TotalSeconds = 665
        };

    private static RoastData Complete(string bean, DateTimeOffset droppedAt) => new()
    {
        Id = Guid.NewGuid(), BeanType = bean, BeanDisplaySnapshot = bean,
        RoastDate = droppedAt.LocalDateTime, DroppedAtUtc = droppedAt,
        BatchWeight = 240, FinalWeight = 206, Temperature = 218,
        RoastMinutes = 11, RoastSeconds = 5, CompletionStatus = RoastCompletionStatus.Complete,
        RoastLevelName = "Medium"
    };

    private static RoastData Unweighed(string bean, DateTimeOffset droppedAt)
    {
        RoastData roast = Complete(bean, droppedAt);
        roast.FinalWeight = null;
        roast.CompletionStatus = RoastCompletionStatus.Unweighed;
        roast.RoastLevelName = string.Empty;
        return roast;
    }
}
