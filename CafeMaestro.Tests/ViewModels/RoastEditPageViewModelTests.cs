using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class RoastEditPageViewModelTests
{
    [Fact]
    public async Task OnAppearing_LoadsExistingRoastForEditing()
    {
        Harness harness = new();

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.SelectedBean.Should().Be(harness.Bean);
        harness.ViewModel.TemperatureText.Should().Be("218");
        harness.ViewModel.RoastTimeText.Should().Be("11:05");
        harness.ViewModel.Notes.Should().Be("caramel");
    }

    [Fact]
    public async Task Save_UpdatesExistingRecordAndReturnsToLog()
    {
        Harness harness = new();
        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.FinalWeightText = "205"; // Generic edit must ignore final-weight changes.
        harness.ViewModel.Notes = "  sweeter  ";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.Is<RoastData>(roast =>
            roast.Id == harness.Roast.Id &&
            roast.FinalWeight == 206 &&
            roast.Notes == "sweeter" &&
            roast.CompletionStatus == RoastCompletionStatus.Complete)), Times.Once);
        harness.Navigation.Verify(service => service.GoBackAsync(), Times.Once);
    }

    [Fact]
    public async Task OnAppearing_DoesNotGuessAnAmbiguousLegacyBeanByDisplayName()
    {
        Harness harness = new();
        BeanData duplicate = new()
        {
            Id = Guid.NewGuid(), Country = harness.Bean.Country, CoffeeName = harness.Bean.CoffeeName,
            Variety = harness.Bean.Variety, Quantity = 1, RemainingQuantity = 1
        };
        harness.Beans.Setup(service => service.GetSortedAvailableBeansAsync())
            .ReturnsAsync([harness.Bean, duplicate]);
        harness.Roast.BeanId = null;

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.SelectedBean.Should().BeNull();
    }

    private sealed class Harness
    {
        public BeanData Bean { get; } = new()
        {
            Id = Guid.NewGuid(), CoffeeName = "Guji", Country = "Ethiopia", Quantity = 1, RemainingQuantity = 1
        };

        public RoastData Roast { get; }
        public Mock<IRoastDataService> Roasts { get; } = new();
        public Mock<IBeanDataService> Beans { get; } = new();
        public Mock<INavigationService> Navigation { get; } = new();
        public RoastEditPageViewModel ViewModel { get; }

        public Harness()
        {
            Roast = new RoastData
            {
                Id = Guid.NewGuid(), BeanId = Bean.Id, BeanType = Bean.DisplayName,
                BeanDisplaySnapshot = Bean.DisplayName, Temperature = 218, BatchWeight = 240,
                FinalWeight = 206, RoastMinutes = 11, RoastSeconds = 5,
                RoastDate = new DateTime(2026, 8, 25), Notes = "caramel",
                CompletionStatus = RoastCompletionStatus.Complete, RoastLevelName = "Medium"
            };

            Beans.Setup(service => service.GetSortedAvailableBeansAsync()).ReturnsAsync([Bean]);
            Roasts.Setup(service => service.GetRoastLogByIdAsync(Roast.Id)).ReturnsAsync(Roast);
            Roasts.Setup(service => service.UpdateRoastLogAsync(It.IsAny<RoastData>())).ReturnsAsync(true);
            Navigation.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);
            var levels = new Mock<IRoastLevelService>();
            levels.Setup(service => service.GetRoastLevelNameAsync(It.IsAny<double>())).ReturnsAsync("Medium");

            ViewModel = new RoastEditPageViewModel(
                Roasts.Object,
                Beans.Object,
                levels.Object,
                Navigation.Object,
                Mock.Of<IAlertService>());
            ViewModel.EditRoastId = Roast.Id.ToString();
        }
    }
}
