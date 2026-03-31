using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class BeanEditPageViewModelTests
{
    [Fact]
    public async Task ApplyQueryAttributes_LoadsExistingBeanForEditMode()
    {
        var beanId = Guid.NewGuid();
        var bean = new BeanData
        {
            Id = beanId,
            CoffeeName = "Yirgacheffe",
            Country = "Ethiopia",
            Variety = "Heirloom",
            Process = "Washed",
            Notes = "Floral",
            Quantity = 2.5,
            RemainingQuantity = 1.5,
            Price = 18.25m,
            Link = "https://example.test/bean",
            PurchaseDate = new DateTime(2025, 4, 12)
        };

        var beanService = new Mock<IBeanDataService>();
        beanService.Setup(service => service.GetBeanByIdAsync(beanId)).ReturnsAsync(bean);

        var viewModel = CreateViewModel(beanService: beanService);

        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["BeanId"] = beanId.ToString() });
        await viewModel.OnAppearingAsync();

        viewModel.PageTitle.Should().Be("Edit Bean");
        viewModel.PageHeading.Should().Be("Edit Bean");
        viewModel.CoffeeName.Should().Be(bean.CoffeeName);
        viewModel.Country.Should().Be(bean.Country);
        viewModel.Variety.Should().Be(bean.Variety);
        viewModel.Process.Should().Be(bean.Process);
        viewModel.Quantity.Should().Be("2.50");
        viewModel.RemainingQuantity.Should().Be("1.50");
        viewModel.Price.Should().Be("18.25");
        viewModel.Link.Should().Be(bean.Link);
        viewModel.PurchaseDate.Should().Be(bean.PurchaseDate);
    }

    [Fact]
    public async Task SaveCommand_AddsNewBeanWhenInputIsValid()
    {
        var beanService = new Mock<IBeanDataService>();
        beanService.Setup(service => service.AddBeanAsync(It.IsAny<BeanData>())).ReturnsAsync(true);

        var navigationService = new Mock<INavigationService>();
        navigationService.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);

        var alerts = new List<string>();
        var viewModel = CreateViewModel(beanService: beanService, navigationService: navigationService);
        viewModel.AlertAsync = (title, message, cancel) =>
        {
            alerts.Add($"{title}:{message}");
            return Task.CompletedTask;
        };

        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["IsNewBean"] = true });
        await viewModel.OnAppearingAsync();

        viewModel.CoffeeName = "Bensa";
        viewModel.Country = "Ethiopia";
        viewModel.Variety = "74158";
        viewModel.Process = "Natural";
        viewModel.Quantity = "5.50";
        viewModel.RemainingQuantity = "5.50";
        viewModel.Price = "34.99";
        viewModel.Link = "https://example.test";
        viewModel.Notes = "Berry forward";

        await viewModel.SaveCommand.ExecuteAsync(null);

        beanService.Verify(
            service => service.AddBeanAsync(It.Is<BeanData>(bean =>
                bean.CoffeeName == "Bensa" &&
                bean.Country == "Ethiopia" &&
                bean.Variety == "74158" &&
                bean.Process == "Natural" &&
                bean.Quantity == 5.5 &&
                bean.RemainingQuantity == 5.5 &&
                bean.Price == 34.99m)),
            Times.Once);

        navigationService.Verify(service => service.GoBackAsync(), Times.Once);
        alerts.Should().ContainSingle(message => message.StartsWith("Success:"));
    }

    [Fact]
    public async Task SaveCommand_ShowsValidationMessageAndSkipsSaveWhenInvalid()
    {
        var beanService = new Mock<IBeanDataService>();
        var alerts = new List<string>();
        var viewModel = CreateViewModel(beanService: beanService);
        viewModel.AlertAsync = (title, message, cancel) =>
        {
            alerts.Add($"{title}:{message}");
            return Task.CompletedTask;
        };

        viewModel.CoffeeName = string.Empty;
        viewModel.Country = "Colombia";
        viewModel.Quantity = "2.00";
        viewModel.RemainingQuantity = "1.00";

        await viewModel.SaveCommand.ExecuteAsync(null);

        beanService.Verify(service => service.AddBeanAsync(It.IsAny<BeanData>()), Times.Never);
        beanService.Verify(service => service.UpdateBeanAsync(It.IsAny<BeanData>()), Times.Never);
        alerts.Should().ContainSingle(message => message == "Validation Error:Please enter a coffee name");
    }

    [Fact]
    public void FieldProperties_RaisePropertyChangedNotifications()
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

        viewModel.CoffeeName = "Kenya AA";

        changedProperties.Should().Contain(nameof(BeanEditPageViewModel.CoffeeName));
    }

    private static BeanEditPageViewModel CreateViewModel(
        Mock<IBeanDataService>? beanService = null,
        Mock<INavigationService>? navigationService = null)
    {
        beanService ??= new Mock<IBeanDataService>();
        navigationService ??= new Mock<INavigationService>();
        navigationService.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);
        return new BeanEditPageViewModel(beanService.Object, navigationService.Object);
    }
}
