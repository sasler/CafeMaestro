using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class BeanDetailPageViewModelTests
{
    [Fact]
    public async Task Load_SelectsNewestCompleteAndSeparatesRecentIncompleteWork()
    {
        BeanData bean = CreateBean();
        RoastData olderComplete = CreateRoast(bean, RoastCompletionStatus.Complete, new DateTime(2026, 8, 20), 205);
        RoastData awaitingWeight = CreateRoast(bean, RoastCompletionStatus.AwaitingWeight, new DateTime(2026, 8, 22));
        RoastData newestComplete = CreateRoast(bean, RoastCompletionStatus.Complete, new DateTime(2026, 8, 23), 206);
        RoastData unweighed = CreateRoast(bean, RoastCompletionStatus.Unweighed, new DateTime(2026, 8, 24));

        Mock<IBeanDataService> beans = new();
        beans.Setup(service => service.GetBeanByIdAsync(bean.Id)).ReturnsAsync(bean);

        Mock<IRoastQueryService> roasts = new();
        roasts.Setup(service => service.GetRoastsForBeanAsync(bean.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([unweighed, newestComplete, awaitingWeight, olderComplete]);

        BeanDetailPageViewModel viewModel = CreateViewModel(beans, roasts);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["BeanId"] = bean.Id.ToString() });

        await viewModel.OnAppearingAsync();

        viewModel.Bean.Should().BeSameAs(bean);
        viewModel.LatestCompletedRoast.Should().BeSameAs(newestComplete);
        viewModel.RecentIncompleteRoasts.Should().Equal(unweighed, awaitingWeight);
    }

    [Fact]
    public async Task Load_RequestsHistoryByStableBeanIdWhenDisplayNamesMatch()
    {
        BeanData selected = CreateBean();
        BeanData duplicate = CreateBean();
        RoastData selectedRoast = CreateRoast(selected, RoastCompletionStatus.Complete, new DateTime(2026, 8, 23), 206);
        RoastData duplicateRoast = CreateRoast(duplicate, RoastCompletionStatus.Complete, new DateTime(2026, 8, 24), 218);

        Mock<IBeanDataService> beans = new();
        beans.Setup(service => service.GetBeanByIdAsync(selected.Id)).ReturnsAsync(selected);

        Mock<IRoastQueryService> roasts = new();
        roasts.Setup(service => service.GetRoastsForBeanAsync(selected.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([selectedRoast]);

        BeanDetailPageViewModel viewModel = CreateViewModel(beans, roasts);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["BeanId"] = selected.Id });

        await viewModel.OnAppearingAsync();

        viewModel.LatestCompletedRoast.Should().BeSameAs(selectedRoast);
        viewModel.LatestCompletedRoast.Should().NotBeSameAs(duplicateRoast);
        roasts.Verify(service => service.GetRoastsForBeanAsync(
            selected.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        roasts.Verify(service => service.GetRoastsForBeanAsync(
            duplicate.Id,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartRoast_NavigatesWithStableBeanIdIntoConfirmationFlow()
    {
        BeanData bean = CreateBean();
        bean.RemainingQuantity = 0;
        Mock<IBeanDataService> beans = new();
        beans.Setup(service => service.GetBeanByIdAsync(bean.Id)).ReturnsAsync(bean);

        Mock<IRoastQueryService> roasts = new();
        roasts.Setup(service => service.GetRoastsForBeanAsync(bean.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<INavigationService> navigation = new();
        navigation.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);

        BeanDetailPageViewModel viewModel = CreateViewModel(beans, roasts, navigation);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["BeanId"] = bean.Id });
        await viewModel.OnAppearingAsync();

        viewModel.CanStartRoast.Should().BeTrue();
        await viewModel.StartRoastCommand.ExecuteAsync(null);

        navigation.Verify(service => service.GoToAsync(
            Routes.Roast,
            It.Is<IDictionary<string, object>>(parameters =>
                parameters["BeanId"].ToString() == bean.Id.ToString() &&
                parameters["NewRoast"].ToString() == bool.TrueString)), Times.Once);
    }

    private static BeanDetailPageViewModel CreateViewModel(
        Mock<IBeanDataService> beans,
        Mock<IRoastQueryService> roasts,
        Mock<INavigationService>? navigation = null)
    {
        navigation ??= new Mock<INavigationService>();
        Mock<IAppDataService> appData = new();
        Mock<IAlertService> alerts = new();
        alerts.Setup(service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return new BeanDetailPageViewModel(
            beans.Object,
            roasts.Object,
            appData.Object,
            navigation.Object,
            alerts.Object);
    }

    private static BeanData CreateBean() => new()
    {
        Id = Guid.NewGuid(),
        Country = "Ethiopia",
        CoffeeName = "Guji",
        Variety = "Heirloom",
        Quantity = 2,
        RemainingQuantity = 1.5
    };

    private static RoastData CreateRoast(
        BeanData bean,
        RoastCompletionStatus status,
        DateTime date,
        double? finalWeight = null) => new()
        {
            Id = Guid.NewGuid(),
            BeanId = bean.Id,
            BeanDisplaySnapshot = bean.DisplayName,
            BeanType = bean.DisplayName,
            CompletionStatus = status,
            RoastDate = date,
            DroppedAtUtc = new DateTimeOffset(date, TimeSpan.Zero),
            Temperature = 218,
            BatchWeight = 240,
            FinalWeight = finalWeight,
            RoastMinutes = 11,
            RoastSeconds = 5,
            RoastLevelName = status == RoastCompletionStatus.Complete ? "Medium" : "Pending"
        };
}
