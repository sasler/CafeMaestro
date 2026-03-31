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
        Mock<INavigationService>? navigationService = null)
    {
        beanService ??= new Mock<IBeanDataService>();
        appDataService ??= CreateAppDataServiceMock();
        preferencesService ??= new Mock<IPreferencesService>();
        navigationService ??= new Mock<INavigationService>();

        preferencesService.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync((string?)null);
        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        navigationService.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);

        return new BeanInventoryPageViewModel(
            beanService.Object,
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
