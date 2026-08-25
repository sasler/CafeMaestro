using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class RoastDetailPageViewModelTests
{
    [Fact]
    public async Task CompleteRoast_UsesWeighInForFinalWeight_AndEditRouteForOtherAllowedValues()
    {
        RoastData roast = new()
        {
            Id = Guid.NewGuid(), BeanType = "Guji", BeanDisplaySnapshot = "Guji",
            BatchWeight = 240, FinalWeight = 206, Temperature = 218,
            RoastMinutes = 11, RoastSeconds = 5, RoastDate = DateTime.Today,
            DroppedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CompletionStatus = RoastCompletionStatus.Complete, RoastLevelName = "Medium"
        };
        var query = new Mock<IRoastQueryService>();
        query.Setup(service => service.GetRoastAsync(roast.Id, It.IsAny<CancellationToken>())).ReturnsAsync(roast);
        query.Setup(service => service.GetOpenWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);
        var overlay = new Mock<IOverlayService>();
        overlay.Setup(service => service.ShowWeighInAsync(It.IsAny<WeighInRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WeighInOutcome.Cancelled);
        var viewModel = new RoastDetailPageViewModel(
            query.Object, new Mock<IAppDataService>().Object, navigation.Object,
            overlay.Object, new Mock<IRoastDataService>().Object, new Mock<IAlertService>().Object);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["RoastId"] = roast.Id.ToString() });
        await viewModel.OnAppearingAsync();

        await viewModel.EditFinalWeightCommand.ExecuteAsync(null);
        await viewModel.EditRoastCommand.ExecuteAsync(null);

        overlay.Verify(service => service.ShowWeighInAsync(
            It.Is<WeighInRequest>(request => request.RoastId == roast.Id && request.InitialFinalWeight == 206),
            It.IsAny<CancellationToken>()), Times.Once);
        navigation.Verify(service => service.GoToAsync(
            Routes.RoastEdit,
            It.Is<IDictionary<string, object>>(parameters => parameters["EditRoastId"].ToString() == roast.Id.ToString())),
            Times.Once);
        viewModel.Card!.OutputDisplay.Should().Be("206.0 g");
    }
}
