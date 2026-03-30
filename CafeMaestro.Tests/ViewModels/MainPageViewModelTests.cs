using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class MainPageViewModelTests
{
    [Fact]
    public async Task OnAppearingAsync_LoadsFilePathAndStatistics()
    {
        var appData = CreateAppData(beanCount: 2, roastCount: 3);
        var appDataService = new Mock<IAppDataService>();
        var preferencesService = new Mock<IPreferencesService>();

        appDataService.SetupGet(service => service.DataFilePath).Returns(@"C:\data\default.json");
        appDataService.SetupGet(service => service.CurrentData).Returns(appData);
        appDataService.Setup(service => service.SetCustomFilePathAsync(@"C:\data\custom.json"))
            .ReturnsAsync(appData);
        preferencesService.Setup(service => service.GetAppDataFilePathAsync())
            .ReturnsAsync(@"C:\data\custom.json");

        var viewModel = new MainPageViewModel(appDataService.Object, preferencesService.Object);

        await viewModel.OnAppearingAsync();

        viewModel.DataFilePath.Should().Be(@"C:\data\custom.json");
        viewModel.DataFilePathDisplay.Should().Be("File: custom.json");
        viewModel.BeanCount.Should().Be(2);
        viewModel.RoastCount.Should().Be(3);
        viewModel.DataStatsDisplay.Should().Be("Beans: 2  |  Roasts: 3");
        appDataService.Verify(service => service.SetCustomFilePathAsync(@"C:\data\custom.json"), Times.Once);
    }

    [Fact]
    public async Task Commands_NavigateToExpectedRoutes()
    {
        var appDataService = CreateAppDataServiceMock(CreateAppData());
        var preferencesService = new Mock<IPreferencesService>();
        preferencesService.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync((string?)null);

        var viewModel = new TestableMainPageViewModel(appDataService.Object, preferencesService.Object);

        await viewModel.StartRoastingCommand.ExecuteAsync(null);
        await viewModel.GoToBeansCommand.ExecuteAsync(null);
        await viewModel.GoToRoastLogCommand.ExecuteAsync(null);
        await viewModel.GoToSettingsCommand.ExecuteAsync(null);

        viewModel.NavigatedRoutes.Should().ContainInOrder(
            "//RoastPage",
            "//BeanInventoryPage",
            "//RoastLogPage",
            "//SettingsPage");
    }

    [Fact]
    public async Task DataChangeSubscription_UpdatesPropertiesWhileVisible()
    {
        var initialAppData = CreateAppData(beanCount: 1, roastCount: 1);
        var updatedAppData = CreateAppData(beanCount: 4, roastCount: 6);
        var appDataService = CreateAppDataServiceMock(initialAppData);
        var preferencesService = new Mock<IPreferencesService>();
        preferencesService.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync((string?)null);

        var viewModel = new MainPageViewModel(appDataService.Object, preferencesService.Object);

        await viewModel.OnAppearingAsync();

        appDataService.Raise(service => service.DataFilePathChanged += null, appDataService.Object, @"C:\data\updated.json");
        appDataService.Raise(service => service.DataChanged += null, appDataService.Object, updatedAppData);

        viewModel.DataFilePath.Should().Be(@"C:\data\updated.json");
        viewModel.DataFilePathDisplay.Should().Be("File: updated.json");
        viewModel.BeanCount.Should().Be(4);
        viewModel.RoastCount.Should().Be(6);
        viewModel.DataStatsDisplay.Should().Be("Beans: 4  |  Roasts: 6");

        viewModel.OnDisappearing();
        appDataService.Raise(service => service.DataChanged += null, appDataService.Object, CreateAppData(beanCount: 9, roastCount: 9));

        viewModel.BeanCount.Should().Be(4);
        viewModel.RoastCount.Should().Be(6);
    }

    private static Mock<IAppDataService> CreateAppDataServiceMock(AppData appData)
    {
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.DataFilePath).Returns(@"C:\data\cafemaestro_data.json");
        appDataService.SetupGet(service => service.CurrentData).Returns(appData);
        appDataService.Setup(service => service.SetCustomFilePathAsync(It.IsAny<string>())).ReturnsAsync(appData);
        return appDataService;
    }

    private static AppData CreateAppData(int beanCount = 0, int roastCount = 0)
    {
        return new AppData
        {
            Beans = Enumerable.Range(1, beanCount)
                .Select(index => new BeanData { CoffeeName = $"Bean {index}" })
                .ToList(),
            RoastLogs = Enumerable.Range(1, roastCount)
                .Select(index => new RoastData { BeanType = $"Roast {index}" })
                .ToList()
        };
    }

    private sealed class TestableMainPageViewModel : MainPageViewModel
    {
        public TestableMainPageViewModel(IAppDataService appDataService, IPreferencesService preferencesService)
            : base(appDataService, preferencesService)
        {
        }

        public List<string> NavigatedRoutes { get; } = new();

        protected override Task NavigateToAsync(string route)
        {
            NavigatedRoutes.Add(route);
            return Task.CompletedTask;
        }
    }
}
