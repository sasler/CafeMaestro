using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class BeanDataServiceTests
{
    [Fact]
    public async Task GetAllBeansAsync_PropagatesReadFailure()
    {
        Mock<IAppDataService> appData = CreateAppDataService();
        appData.Setup(service => service.LoadAppDataAsync()).ThrowsAsync(new IOException("Read failed"));
        BeanDataService service = new(appData.Object, Mock.Of<ICsvParserService>());

        Func<Task> act = () => service.GetAllBeansAsync();

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task GetSortedAvailableBeansAsync_KeepsOutOfStockBeansSelectableForSetup()
    {
        BeanData outOfStock = new()
        {
            Id = Guid.NewGuid(), Country = "Kenya", CoffeeName = "Nyeri", Variety = "SL28",
            Quantity = 1, RemainingQuantity = 0
        };
        Mock<IAppDataService> appData = CreateAppDataService();
        appData.Setup(service => service.LoadAppDataAsync()).ReturnsAsync(new AppData
        {
            Beans = [outOfStock],
            RoastLogs = []
        });
        BeanDataService service = new(appData.Object, Mock.Of<ICsvParserService>());

        List<BeanData> beans = await service.GetSortedAvailableBeansAsync();

        beans.Should().ContainSingle().Which.Should().BeSameAs(outOfStock);
    }

    private static Mock<IAppDataService> CreateAppDataService()
    {
        Mock<IAppDataService> appData = new();
        appData.SetupGet(service => service.DataFilePath).Returns(@"C:\data\cafemaestro_data.json");
        return appData;
    }
}
