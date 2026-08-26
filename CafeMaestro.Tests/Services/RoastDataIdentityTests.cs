using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class RoastDataIdentityTests
{
    [Fact]
    public async Task GetLastRoastForBeanAsync_UsesStableBeanIdWhenDisplayNamesMatch()
    {
        BeanData first = Bean();
        BeanData second = Bean();
        RoastData firstRoast = Roast(first, new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero), 210);
        RoastData secondRoast = Roast(second, new DateTimeOffset(2026, 8, 25, 8, 0, 0, TimeSpan.Zero), 225);
        var data = new AppData { Beans = [first, second], RoastLogs = [firstRoast, secondRoast] };
        var appData = new Mock<IAppDataService>();
        appData.SetupGet(service => service.DataFilePath).Returns("cafemaestro_data.json");
        appData.Setup(service => service.LoadAppDataAsync()).ReturnsAsync(data);
        using var service = new RoastDataService(
            appData.Object,
            Mock.Of<IRoastLevelService>(),
            Mock.Of<ICoolingNotificationService>(),
            Mock.Of<IRoastPreferencesService>());

        (await service.GetLastRoastForBeanAsync(first.Id)).Should().BeSameAs(firstRoast);
        (await service.GetLastRoastForBeanAsync(second.Id)).Should().BeSameAs(secondRoast);
    }

    private static BeanData Bean() => new()
    {
        Id = Guid.NewGuid(), Country = "Ethiopia", CoffeeName = "Guji", Variety = "Heirloom",
        Quantity = 1, RemainingQuantity = 1
    };

    private static RoastData Roast(BeanData bean, DateTimeOffset droppedAt, double temperature) => new()
    {
        Id = Guid.NewGuid(), BeanId = bean.Id, BeanType = bean.DisplayName,
        BeanDisplaySnapshot = bean.DisplayName, Temperature = temperature, BatchWeight = 240,
        FinalWeight = 205, RoastMinutes = 11, RoastSeconds = 5, RoastDate = droppedAt.UtcDateTime,
        DroppedAtUtc = droppedAt, CompletionStatus = RoastCompletionStatus.Complete
    };
}
