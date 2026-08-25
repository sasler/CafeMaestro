using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

/// <summary>
/// The roast-level behavior that shipped on the old settings page, verified against the
/// focused page that now owns it.
/// </summary>
public sealed class RoastLevelSettingsPageViewModelTests
{
    [Fact]
    public async Task ResetRoastLevelsToDefaultsCommand_WhenConfirmed_SavesDefaultLevels()
    {
        var roastLevels = new Mock<IRoastLevelService>();
        roastLevels
            .Setup(service => service.SaveRoastLevelsAsync(It.IsAny<List<RoastLevelData>>()))
            .ReturnsAsync(true);
        roastLevels.Setup(service => service.GetRoastLevelsAsync()).ReturnsAsync([]);
        var alerts = new Mock<IAlertService>();
        alerts
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);
        var viewModel = new RoastLevelSettingsPageViewModel(roastLevels.Object, alerts.Object);

        await viewModel.ResetRoastLevelsToDefaultsCommand.ExecuteAsync(null);

        roastLevels.Verify(
            service => service.SaveRoastLevelsAsync(
                It.Is<List<RoastLevelData>>(levels => levels.Count == 7)),
            Times.Once);
    }

    [Fact]
    public async Task SaveRoastLevelCommand_WithInvalidRange_DoesNotPersist()
    {
        var roastLevels = new Mock<IRoastLevelService>();
        roastLevels.Setup(service => service.GetRoastLevelsAsync()).ReturnsAsync([]);
        var alerts = new Mock<IAlertService>();
        var viewModel = new RoastLevelSettingsPageViewModel(roastLevels.Object, alerts.Object);
        viewModel.AddRoastLevelCommand.Execute(null);
        viewModel.RoastLevelName = "Invalid";
        viewModel.MinWeightLossText = "15.0";
        viewModel.MaxWeightLossText = "10.0";

        await viewModel.SaveRoastLevelCommand.ExecuteAsync(null);

        roastLevels.Verify(
            service => service.AddRoastLevelAsync(It.IsAny<RoastLevelData>()),
            Times.Never);
        alerts.Verify(
            service => service.ShowAlertAsync("Invalid Roast Level", It.IsAny<string>(), "OK"),
            Times.Once);
    }

    [Fact]
    public async Task OnAppearing_OrdersLevelsByTheirLowerBoundAndSummarisesTheCount()
    {
        var roastLevels = new Mock<IRoastLevelService>();
        roastLevels.Setup(service => service.GetRoastLevelsAsync()).ReturnsAsync(
        [
            new RoastLevelData { Name = "Dark", MinWeightLossPercentage = 16, MaxWeightLossPercentage = 20 },
            new RoastLevelData { Name = "Light", MinWeightLossPercentage = 10, MaxWeightLossPercentage = 13 }
        ]);
        var viewModel = new RoastLevelSettingsPageViewModel(
            roastLevels.Object,
            Mock.Of<IAlertService>());

        await viewModel.OnAppearingAsync();

        viewModel.RoastLevels.Select(level => level.Name).Should().ContainInOrder("Light", "Dark");
        viewModel.RoastLevelSummary.Should().Be("2 configured");
        viewModel.HasNoRoastLevels.Should().BeFalse();
    }
}
