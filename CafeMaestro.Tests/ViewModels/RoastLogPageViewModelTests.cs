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
    public async Task RefreshAndSearchCommands_FilterRoasts()
    {
        var roasts = new List<RoastData>
        {
            new() { Id = Guid.NewGuid(), BeanType = "Brazil", RoastDate = new DateTime(2025, 1, 1), BatchWeight = 200, FinalWeight = 170, RoastMinutes = 10, RoastSeconds = 30, Temperature = 210 },
            new() { Id = Guid.NewGuid(), BeanType = "Ethiopia", RoastDate = new DateTime(2025, 2, 1), BatchWeight = 200, FinalWeight = 168, RoastMinutes = 11, RoastSeconds = 10, Temperature = 212 }
        };

        var roastService = new Mock<IRoastDataService>();
        roastService.Setup(service => service.GetAllRoastLogsAsync()).ReturnsAsync(roasts);

        var viewModel = CreateViewModel(roastService: roastService);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.Roasts.Select(roast => roast.BeanType).Should().ContainInOrder("Ethiopia", "Brazil");
        viewModel.RecordCount.Should().Be(2);

        viewModel.SearchText = "braz";
        await viewModel.SearchCommand.ExecuteAsync(null);

        viewModel.Roasts.Should().ContainSingle();
        viewModel.Roasts.Single().BeanType.Should().Be("Brazil");
    }

    [Fact]
    public async Task EditRoastCommand_NavigatesToRoastPageWithEditId()
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

        await viewModel.EditRoastCommand.ExecuteAsync(roast);

        navigationService.Verify(
            service => service.GoToAsync(
                Routes.Roast,
                It.Is<IDictionary<string, object>>(parameters => parameters["EditRoastId"].ToString() == roast.Id.ToString())),
            Times.Once);
    }

    [Fact]
    public async Task DeleteRoastCommand_RemovesRoastAndRefreshesList()
    {
        var roastToDelete = new RoastData
        {
            Id = Guid.NewGuid(),
            BeanType = "Delete Me",
            RoastDate = new DateTime(2025, 1, 1),
            BatchWeight = 200,
            FinalWeight = 170,
            RoastMinutes = 10,
            RoastSeconds = 0,
            Temperature = 210
        };

        var remainingRoast = new RoastData
        {
            Id = Guid.NewGuid(),
            BeanType = "Keep Me",
            RoastDate = new DateTime(2025, 1, 2),
            BatchWeight = 220,
            FinalWeight = 185,
            RoastMinutes = 11,
            RoastSeconds = 5,
            Temperature = 212
        };

        var roastService = new Mock<IRoastDataService>();
        roastService.SetupSequence(service => service.GetAllRoastLogsAsync())
            .ReturnsAsync([roastToDelete, remainingRoast])
            .ReturnsAsync([remainingRoast]);
        roastService.Setup(service => service.DeleteRoastLogAsync(roastToDelete.Id)).ReturnsAsync(true);

        var viewModel = CreateViewModel(roastService: roastService);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.DeleteRoastCommand.ExecuteAsync(roastToDelete);

        roastService.Verify(service => service.DeleteRoastLogAsync(roastToDelete.Id), Times.Once);
        viewModel.Roasts.Should().ContainSingle();
        viewModel.Roasts.Single().Id.Should().Be(remainingRoast.Id);
    }

    [Fact]
    public async Task OnAppearingAndDataChanged_UpdatesRoastsWhileVisible()
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

        var roastService = new Mock<IRoastDataService>();
        roastService.Setup(service => service.GetAllRoastLogsAsync()).ReturnsAsync([initialRoast]);

        var appDataService = CreateAppDataServiceMock();
        var viewModel = CreateViewModel(roastService: roastService, appDataService: appDataService);

        await viewModel.OnAppearingAsync();

        appDataService.Raise(service => service.DataChanged += null, appDataService.Object, updatedAppData);

        viewModel.Roasts.Should().ContainSingle();
        viewModel.Roasts.Single().BeanType.Should().Be("Updated");

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

        viewModel.Roasts.Single().BeanType.Should().Be("Updated");
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

    private static RoastLogPageViewModel CreateViewModel(
        Mock<IRoastDataService>? roastService = null,
        Mock<IAppDataService>? appDataService = null,
        Mock<IPreferencesService>? preferencesService = null,
        Mock<INavigationService>? navigationService = null)
    {
        roastService ??= new Mock<IRoastDataService>();
        appDataService ??= CreateAppDataServiceMock();
        preferencesService ??= new Mock<IPreferencesService>();
        navigationService ??= new Mock<INavigationService>();

        preferencesService.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync((string?)null);
        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);

        return new RoastLogPageViewModel(
            roastService.Object,
            appDataService.Object,
            preferencesService.Object,
            navigationService.Object);
    }

    private static Mock<IAppDataService> CreateAppDataServiceMock()
    {
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.DataFilePath).Returns(@"C:\data\cafemaestro_data.json");
        appDataService.Setup(service => service.SetCustomFilePathAsync(It.IsAny<string>()))
            .ReturnsAsync(new AppData { Beans = [], RoastLogs = [] });
        return appDataService;
    }
}
