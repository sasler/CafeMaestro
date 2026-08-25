using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class BeanInventoryPageViewModelTests
{
    [Fact]
    public async Task RootBackRoute_ReturnsToRoastTab()
    {
        Mock<INavigationService> navigation = new();
        navigation.Setup(service => service.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        BeanInventoryPageViewModel viewModel = CreateViewModel(navigationService: navigation);

        await viewModel.NavigateToRoastAsync();

        navigation.Verify(service => service.GoToAsync(Routes.Roast), Times.Once);
    }

    [Fact]
    public async Task RefreshCommand_LoadsBeansAndFiltersBySearchText()
    {
        var beans = new List<BeanData>
        {
            new() { Id = Guid.NewGuid(), Country = "Brazil", CoffeeName = "Yellow Bourbon", Variety = "Bourbon", PurchaseDate = new DateTime(2025, 1, 1) },
            new() { Id = Guid.NewGuid(), Country = "Ethiopia", CoffeeName = "Yirgacheffe", Variety = "Heirloom", PurchaseDate = new DateTime(2025, 2, 1) }
        };

        var beanService = new Mock<IBeanDataService>();
        beanService.Setup(service => service.GetAllBeansAsync()).ReturnsAsync(beans);

        var viewModel = CreateViewModel(beanService: beanService);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.Beans.Select(bean => bean.Country).Should().ContainInOrder("Ethiopia", "Brazil");
        viewModel.RecordCount.Should().Be(2);

        viewModel.SearchText = "ethi";
        await viewModel.SearchCommand.ExecuteAsync(null);

        viewModel.Beans.Should().ContainSingle();
        viewModel.Beans.Single().Country.Should().Be("Ethiopia");
        viewModel.RecordCount.Should().Be(1);
    }

    [Fact]
    public async Task InventoryFilter_SeparatesLowAndOutOfStockWithoutChangingSourceOrder()
    {
        BeanData available = new() { Id = Guid.NewGuid(), Country = "Brazil", CoffeeName = "Catuai", Quantity = 2, RemainingQuantity = 1.2, PurchaseDate = new DateTime(2025, 3, 1) };
        BeanData low = new() { Id = Guid.NewGuid(), Country = "Kenya", CoffeeName = "Nyeri", Quantity = 1, RemainingQuantity = 0.18, PurchaseDate = new DateTime(2025, 2, 1) };
        BeanData outOfStock = new() { Id = Guid.NewGuid(), Country = "Colombia", CoffeeName = "Huila", Quantity = 1, RemainingQuantity = 0, PurchaseDate = new DateTime(2025, 1, 1) };

        Mock<IBeanDataService> beanService = new();
        beanService.Setup(service => service.GetAllBeansAsync()).ReturnsAsync([outOfStock, low, available]);
        BeanInventoryPageViewModel viewModel = CreateViewModel(beanService: beanService);

        await viewModel.RefreshCommand.ExecuteAsync(null);
        await viewModel.SelectFilterCommand.ExecuteAsync(BeanInventoryFilter.Low);
        viewModel.Beans.Should().ContainSingle().Which.Should().BeSameAs(low);

        await viewModel.SelectFilterCommand.ExecuteAsync(BeanInventoryFilter.OutOfStock);
        viewModel.Beans.Should().ContainSingle().Which.Should().BeSameAs(outOfStock);

        await viewModel.SelectFilterCommand.ExecuteAsync(BeanInventoryFilter.All);
        viewModel.Beans.Should().Equal(available, low, outOfStock);
    }

    [Fact]
    public async Task DeleteBeanCommand_DeletesBeanAndRefreshesCollection()
    {
        var beanToDelete = new BeanData
        {
            Id = Guid.NewGuid(),
            Country = "Colombia",
            CoffeeName = "Huila",
            Variety = "Caturra",
            PurchaseDate = new DateTime(2025, 1, 2)
        };

        var remainingBean = new BeanData
        {
            Id = Guid.NewGuid(),
            Country = "Kenya",
            CoffeeName = "AA",
            Variety = "SL28",
            PurchaseDate = new DateTime(2025, 1, 3)
        };

        var beanService = new Mock<IBeanDataService>();
        beanService.SetupSequence(service => service.GetAllBeansAsync())
            .ReturnsAsync([beanToDelete, remainingBean])
            .ReturnsAsync([remainingBean]);
        beanService.Setup(service => service.DeleteBeanAsync(beanToDelete.Id)).ReturnsAsync(true);

        var viewModel = CreateViewModel(beanService: beanService);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.DeleteBeanCommand.ExecuteAsync(beanToDelete);

        beanService.Verify(service => service.DeleteBeanAsync(beanToDelete.Id), Times.Once);
        viewModel.Beans.Should().ContainSingle();
        viewModel.Beans.Single().Id.Should().Be(remainingBean.Id);
    }

    [Fact]
    public async Task EditAndAddCommands_NavigateToExpectedRoutes()
    {
        var bean = new BeanData
        {
            Id = Guid.NewGuid(),
            Country = "Guatemala",
            CoffeeName = "Antigua",
            Variety = "Bourbon"
        };

        var beanService = new Mock<IBeanDataService>();
        beanService.Setup(service => service.GetBeanByIdAsync(bean.Id)).ReturnsAsync(bean);

        var navigationService = new Mock<INavigationService>();
        var viewModel = CreateViewModel(beanService: beanService, navigationService: navigationService);

        await viewModel.AddBeanCommand.ExecuteAsync(null);
        await viewModel.EditBeanCommand.ExecuteAsync(bean);

        navigationService.Verify(
            service => service.GoToAsync(
                Routes.BeanEdit,
                It.Is<IDictionary<string, object>>(parameters => parameters.ContainsKey("IsNewBean") && (bool)parameters["IsNewBean"])),
            Times.Once);

        navigationService.Verify(
            service => service.GoToAsync(
                Routes.BeanEdit,
                It.Is<IDictionary<string, object>>(parameters =>
                    parameters.ContainsKey("BeanId") &&
                    parameters["BeanId"] != null &&
                    parameters["BeanId"].ToString() == bean.Id.ToString())),
            Times.Once);
    }

    [Fact]
    public async Task OnAppearingAndDataChanged_RefreshCollectionWhileVisible()
    {
        var initialBeans = new List<BeanData>
        {
            new() { Id = Guid.NewGuid(), Country = "Brazil", CoffeeName = "Initial", Variety = "Catuai", PurchaseDate = new DateTime(2025, 1, 1) }
        };

        var updatedAppData = new AppData
        {
            Beans =
            [
                new BeanData { Id = Guid.NewGuid(), Country = "Rwanda", CoffeeName = "Updated", Variety = "Red Bourbon", PurchaseDate = new DateTime(2025, 3, 1) }
            ],
            RoastLogs = []
        };

        var beanService = new Mock<IBeanDataService>();
        beanService.Setup(service => service.GetAllBeansAsync()).ReturnsAsync(initialBeans);

        var appDataService = CreateAppDataServiceMock();
        var viewModel = CreateViewModel(beanService: beanService, appDataService: appDataService);

        await viewModel.OnAppearingAsync();

        appDataService.Raise(service => service.DataChanged += null, appDataService.Object, updatedAppData);

        viewModel.Beans.Should().ContainSingle();
        viewModel.Beans.Single().Country.Should().Be("Rwanda");

        viewModel.OnDisappearing();
        appDataService.Raise(service => service.DataChanged += null, appDataService.Object, new AppData
        {
            Beans = [new BeanData { Id = Guid.NewGuid(), Country = "Ignored", CoffeeName = "Ignored", Variety = "Ignored" }],
            RoastLogs = []
        });

        viewModel.Beans.Single().Country.Should().Be("Rwanda");
    }

    [Fact]
    public async Task DataChanged_InvalidatesOlderRefreshResult()
    {
        BeanData staleBean = new() { Id = Guid.NewGuid(), Country = "Brazil", CoffeeName = "Stale", Variety = "Catuai" };
        BeanData currentBean = new() { Id = Guid.NewGuid(), Country = "Rwanda", CoffeeName = "Current", Variety = "Bourbon" };
        TaskCompletionSource<List<BeanData>> pendingRefresh = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Mock<IBeanDataService> beanService = new();
        beanService.Setup(service => service.GetAllBeansAsync()).Returns(pendingRefresh.Task);
        Mock<IAppDataService> appDataService = CreateAppDataServiceMock();
        BeanInventoryPageViewModel viewModel = CreateViewModel(beanService: beanService, appDataService: appDataService);

        Task refresh = viewModel.OnAppearingAsync();
        appDataService.Raise(service => service.DataChanged += null, appDataService.Object, new AppData
        {
            Beans = [currentBean],
            RoastLogs = []
        });
        pendingRefresh.SetResult([staleBean]);
        await refresh;

        viewModel.Beans.Should().ContainSingle().Which.Should().BeSameAs(currentBean);
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshFailure_RetainsRowsAndRetryCanApplyGenuineEmptySuccess()
    {
        BeanData cachedBean = new() { Id = Guid.NewGuid(), Country = "Kenya", CoffeeName = "Cached", Variety = "SL28" };
        Mock<IBeanDataService> beanService = new();
        beanService.SetupSequence(service => service.GetAllBeansAsync())
            .ReturnsAsync([cachedBean])
            .ThrowsAsync(new IOException("Read failed"))
            .ReturnsAsync([]);
        BeanInventoryPageViewModel viewModel = CreateViewModel(beanService: beanService);

        await viewModel.RefreshCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.Beans.Should().ContainSingle().Which.Should().BeSameAs(cachedBean);
        viewModel.HasLoadError.Should().BeTrue();
        viewModel.IsEmptyInventory.Should().BeFalse();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.Beans.Should().BeEmpty();
        viewModel.HasLoadError.Should().BeFalse();
        viewModel.IsEmptyInventory.Should().BeTrue();
    }

    [Fact]
    public async Task WideDetail_StartRoast_AllowsOutOfStockBeanByStableId()
    {
        BeanData bean = new()
        {
            Id = Guid.NewGuid(), Country = "Colombia", CoffeeName = "Huila", Variety = "Caturra",
            Quantity = 1, RemainingQuantity = 0
        };
        Mock<IBeanDataService> beanService = new();
        beanService.Setup(service => service.GetAllBeansAsync()).ReturnsAsync([bean]);
        Mock<INavigationService> navigation = new();
        navigation.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);
        BeanInventoryPageViewModel viewModel = CreateViewModel(beanService: beanService, navigationService: navigation);
        viewModel.SetWideLayout(true);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        await viewModel.OpenBeanCommand.ExecuteAsync(bean);

        await viewModel.StartSelectedRoastCommand.ExecuteAsync(null);

        navigation.Verify(service => service.GoToAsync(
            Routes.Roast,
            It.Is<IDictionary<string, object>>(parameters => parameters["BeanId"].ToString() == bean.Id.ToString())),
            Times.Once);
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

        viewModel.SearchText = "Kenya";

        changedProperties.Should().Contain(nameof(BeanInventoryPageViewModel.SearchText));
    }

    private static BeanInventoryPageViewModel CreateViewModel(
        Mock<IBeanDataService>? beanService = null,
        Mock<IAppDataService>? appDataService = null,
        Mock<IPreferencesService>? preferencesService = null,
        Mock<INavigationService>? navigationService = null,
        Mock<IAlertService>? alertService = null,
        Mock<IRoastQueryService>? roastQueryService = null)
    {
        beanService ??= new Mock<IBeanDataService>();
        appDataService ??= CreateAppDataServiceMock();
        preferencesService ??= new Mock<IPreferencesService>();
        navigationService ??= new Mock<INavigationService>();
        alertService ??= new Mock<IAlertService>();
        roastQueryService ??= new Mock<IRoastQueryService>();

        preferencesService.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync((string?)null);
        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);
        alertService.Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        alertService.Setup(service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        roastQueryService.Setup(service => service.GetRoastsForBeanAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new BeanInventoryPageViewModel(
            beanService.Object,
            appDataService.Object,
            preferencesService.Object,
            navigationService.Object,
            alertService.Object,
            roastQueryService.Object);
    }

    private static Mock<IAppDataService> CreateAppDataServiceMock()
    {
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.DataFilePath).Returns(@"C:\data\cafemaestro_data.json");
        return appDataService;
    }
}
