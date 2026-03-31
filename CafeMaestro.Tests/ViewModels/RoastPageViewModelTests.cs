using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class RoastPageViewModelTests
{
    [Fact]
    public async Task TimerCommands_UpdateTimerState()
    {
        BeanData bean = CreateBean();
        RoastPageViewModel viewModel = CreateViewModel(
            setup: mocks =>
            {
                mocks.BeanDataService.Setup(service => service.GetSortedAvailableBeansAsync())
                    .ReturnsAsync(new List<BeanData> { bean });
            });

        await viewModel.OnAppearingAsync();
        await viewModel.StartTimerCommand.ExecuteAsync(null);
        await viewModel.PauseTimerCommand.ExecuteAsync(null);
        await viewModel.StopTimerCommand.ExecuteAsync(null);
        await viewModel.ResetTimerCommand.ExecuteAsync(null);

        viewModel.TimerDisplay.Should().Be("00:00");
        viewModel.CanStartTimer.Should().BeTrue();
        viewModel.CanPauseTimer.Should().BeFalse();
        viewModel.CanStopTimer.Should().BeFalse();
        viewModel.IsTimeEntryEnabled.Should().BeTrue();
        viewModel.CanMarkFirstCrack.Should().BeFalse();
    }

    [Fact]
    public async Task SelectingBean_LoadsPreviousRoastData()
    {
        BeanData bean = CreateBean();
        RoastData previousRoast = new()
        {
            BeanType = bean.DisplayName,
            BatchWeight = 100,
            FinalWeight = 85,
            Temperature = 205,
            RoastMinutes = 11,
            RoastSeconds = 30,
            RoastDate = new DateTime(2025, 1, 10),
            RoastLevelName = "City",
            FirstCrackMinutes = 8,
            FirstCrackSeconds = 15
        };

        RoastPageViewModel viewModel = CreateViewModel(
            setup: mocks =>
            {
                mocks.BeanDataService.Setup(service => service.GetSortedAvailableBeansAsync())
                    .ReturnsAsync(new List<BeanData> { bean });
                mocks.RoastDataService.Setup(service => service.GetLastRoastForBeanTypeAsync(bean.DisplayName))
                    .ReturnsAsync(previousRoast);
            });

        await viewModel.OnAppearingAsync();
        await EventuallyAsync(() => viewModel.HasPreviousRoast);

        viewModel.SelectedBean.Should().Be(bean);
        viewModel.HasPreviousRoast.Should().BeTrue();
        viewModel.PreviousRoastSummary.Should().Contain("City roast");
        viewModel.PreviousRoastDetails.Should().Contain("First Crack: 08:15");
    }

    [Fact]
    public async Task WeightLossCalculation_UpdatesDisplay()
    {
        RoastPageViewModel viewModel = CreateViewModel(
            setup: mocks =>
            {
                mocks.RoastLevelService.Setup(service => service.GetRoastLevelNameAsync(15))
                    .ReturnsAsync("City");
            });

        viewModel.BatchWeightText = "100";
        viewModel.FinalWeightText = "85";

        await EventuallyAsync(() => viewModel.LossPercentLabel.Contains("15.0%"));

        viewModel.LossPercentLabel.Should().Be("Weight loss 15.0% (City roast)");
    }

    [Fact]
    public async Task SaveCommand_InvalidForm_ShowsValidationAlert()
    {
        MockBundle mocks = new();
        RoastPageViewModel viewModel = CreateViewModel(mocks);

        await viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.IsEditMode.Should().BeFalse();
        mocks.AlertService.Verify(
            service => service.ShowAlertAsync(
                "Validation Error",
                "Please select a bean type or add beans to your inventory.",
                "OK"),
            Times.Once);
        mocks.RoastDataService.Verify(service => service.SaveRoastDataAsync(It.IsAny<RoastData>()), Times.Never);
    }

    [Fact]
    public async Task SaveCommand_ValidNewRoast_UpdatesServices()
    {
        BeanData bean = CreateBean();
        RoastData? savedRoast = null;

        RoastPageViewModel viewModel = CreateViewModel(
            setup: mocks =>
            {
                mocks.BeanDataService.Setup(service => service.GetSortedAvailableBeansAsync())
                    .ReturnsAsync(new List<BeanData> { bean });
                mocks.BeanDataService.Setup(service => service.UpdateBeanQuantityAsync(bean.Id, 0.1))
                    .ReturnsAsync(true);
                mocks.RoastDataService.Setup(service => service.SaveRoastDataAsync(It.IsAny<RoastData>()))
                    .Callback<RoastData>(roast => savedRoast = roast)
                    .ReturnsAsync(true);
                mocks.RoastLevelService.Setup(service => service.GetRoastLevelNameAsync(15))
                    .ReturnsAsync("City");
            });

        await viewModel.OnAppearingAsync();
        viewModel.BatchWeightText = "100";
        viewModel.FinalWeightText = "85";
        viewModel.TemperatureText = "210";
        viewModel.Notes = "Sweet finish";
        viewModel.SetManualTimerDisplay("10:30");

        await EventuallyAsync(() => viewModel.LossPercentLabel.Contains("15.0%"));
        await viewModel.SaveCommand.ExecuteAsync(null);

        savedRoast.Should().NotBeNull();
        savedRoast!.BeanType.Should().Be(bean.DisplayName);
        savedRoast.BatchWeight.Should().Be(100);
        savedRoast.FinalWeight.Should().Be(85);
        savedRoast.RoastMinutes.Should().Be(10);
        savedRoast.RoastSeconds.Should().Be(30);
        savedRoast.Notes.Should().Be("Sweet finish");
    }

    [Fact]
    public async Task EditMode_LoadsRoastData()
    {
        BeanData bean = CreateBean();
        Guid roastId = Guid.NewGuid();
        RoastData roastToEdit = new()
        {
            Id = roastId,
            BeanType = bean.DisplayName,
            BatchWeight = 120,
            FinalWeight = 102,
            Temperature = 208,
            RoastMinutes = 12,
            RoastSeconds = 5,
            Notes = "Edit me",
            RoastDate = new DateTime(2025, 2, 1),
            FirstCrackMinutes = 8,
            FirstCrackSeconds = 40
        };

        RoastPageViewModel viewModel = CreateViewModel(
            setup: mocks =>
            {
                mocks.BeanDataService.Setup(service => service.GetSortedAvailableBeansAsync())
                    .ReturnsAsync(new List<BeanData> { bean });
                mocks.RoastDataService.Setup(service => service.GetRoastLogByIdAsync(roastId))
                    .ReturnsAsync(roastToEdit);
                mocks.RoastLevelService.Setup(service => service.GetRoastLevelNameAsync(15))
                    .ReturnsAsync("City");
            });

        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            ["EditRoastId"] = roastId.ToString()
        });

        await viewModel.OnAppearingAsync();
        await EventuallyAsync(() => viewModel.SelectedBean == bean);

        viewModel.IsEditMode.Should().BeTrue();
        viewModel.PageTitle.Should().Be("Edit Roast");
        viewModel.SelectedBean.Should().Be(bean);
        viewModel.TimerDisplay.Should().Be("12:05");
        viewModel.BatchWeightText.Should().Be("120.0");
        viewModel.FinalWeightText.Should().Be("102.0");
        viewModel.TemperatureText.Should().Be("208");
        viewModel.Notes.Should().Be("Edit me");
        viewModel.FirstCrackLabel.Should().Be("First Crack: 08:40");
    }

    [Fact]
    public async Task MarkFirstCrack_StoresCurrentTimerValue()
    {
        BeanData bean = CreateBean();
        RoastPageViewModel viewModel = CreateViewModel(
            setup: mocks =>
            {
                mocks.BeanDataService.Setup(service => service.GetSortedAvailableBeansAsync())
                    .ReturnsAsync(new List<BeanData> { bean });
                mocks.TimerService.Setup(service => service.GetElapsedTime())
                    .Returns(new TimeSpan(0, 3, 45));
            });

        await viewModel.OnAppearingAsync();
        await viewModel.StartTimerCommand.ExecuteAsync(null);
        await viewModel.MarkFirstCrackCommand.ExecuteAsync(null);

        viewModel.FirstCrackMinutes.Should().Be(3);
        viewModel.FirstCrackSeconds.Should().Be(45);
        viewModel.FirstCrackLabel.Should().Be("First Crack: 03:45");
        viewModel.CanMarkFirstCrack.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_InsufficientBeans_SavesSuccessfully()
    {
        BeanData bean = new()
        {
            Id = Guid.NewGuid(),
            Country = "Colombia",
            CoffeeName = "Supremo",
            Variety = "Caturra",
            RemainingQuantity = 0.05,
            Quantity = 1
        };
        RoastData? savedRoast = null;

        RoastPageViewModel viewModel = CreateViewModel(
            setup: mocks =>
            {
                mocks.BeanDataService.Setup(service => service.GetSortedAvailableBeansAsync())
                    .ReturnsAsync(new List<BeanData> { bean });
                mocks.BeanDataService.Setup(service => service.UpdateBeanQuantityAsync(bean.Id, 0.1))
                    .ReturnsAsync(true);
                mocks.RoastDataService.Setup(service => service.SaveRoastDataAsync(It.IsAny<RoastData>()))
                    .Callback<RoastData>(roast => savedRoast = roast)
                    .ReturnsAsync(true);
                mocks.RoastLevelService.Setup(service => service.GetRoastLevelNameAsync(15))
                    .ReturnsAsync("City");
            });

        await viewModel.OnAppearingAsync();
        viewModel.BatchWeightText = "100";
        viewModel.FinalWeightText = "85";
        viewModel.TemperatureText = "210";
        viewModel.SetManualTimerDisplay("10:30");

        await EventuallyAsync(() => viewModel.LossPercentLabel.Contains("15.0%"));
        viewModel.IsBatchWeightWarningVisible.Should().BeTrue();
        viewModel.CanStartTimer.Should().BeTrue();

        await viewModel.SaveCommand.ExecuteAsync(null);

        savedRoast.Should().NotBeNull();
        savedRoast!.BatchWeight.Should().Be(100);
        savedRoast.FinalWeight.Should().Be(85);
    }

    [Fact]
    public async Task SaveCommand_WithoutFinalWeight_SavesWithZeroFinalWeight()
    {
        BeanData bean = CreateBean();
        RoastData? savedRoast = null;

        RoastPageViewModel viewModel = CreateViewModel(
            setup: mocks =>
            {
                mocks.BeanDataService.Setup(service => service.GetSortedAvailableBeansAsync())
                    .ReturnsAsync(new List<BeanData> { bean });
                mocks.BeanDataService.Setup(service => service.UpdateBeanQuantityAsync(bean.Id, 0.1))
                    .ReturnsAsync(true);
                mocks.RoastDataService.Setup(service => service.SaveRoastDataAsync(It.IsAny<RoastData>()))
                    .Callback<RoastData>(roast => savedRoast = roast)
                    .ReturnsAsync(true);
            });

        await viewModel.OnAppearingAsync();
        viewModel.BatchWeightText = "100";
        viewModel.TemperatureText = "210";
        viewModel.SetManualTimerDisplay("10:30");
        // FinalWeightText left empty

        await viewModel.SaveCommand.ExecuteAsync(null);

        savedRoast.Should().NotBeNull();
        savedRoast!.BatchWeight.Should().Be(100);
        savedRoast.FinalWeight.Should().Be(0);
    }

    [Fact]
    public void RoastData_PendingRoast_ShowsPendingLevel()
    {
        RoastData roast = new()
        {
            BeanType = "Ethiopia Yirgacheffe",
            BatchWeight = 100,
            FinalWeight = 0,
            Temperature = 210,
            RoastMinutes = 10,
            RoastSeconds = 30,
            RoastLevelName = "Pending"
        };

        roast.HasFinalWeight.Should().BeFalse();
        roast.WeightLossPercentage.Should().Be(0);
        roast.WeightLossDisplay.Should().Be("Pending");
        roast.Summary.Should().Contain("Pending roast of");
    }

    [Fact]
    public void RoastData_CompletedRoast_ShowsNormalLevel()
    {
        RoastData roast = new()
        {
            BeanType = "Ethiopia Yirgacheffe",
            BatchWeight = 100,
            FinalWeight = 85,
            Temperature = 210,
            RoastMinutes = 10,
            RoastSeconds = 30,
            RoastLevelName = "City"
        };

        roast.HasFinalWeight.Should().BeTrue();
        roast.WeightLossPercentage.Should().Be(15);
        roast.WeightLossDisplay.Should().Be("15.0%");
        roast.Summary.Should().Contain("City roast of");
    }

    [Fact]
    public void RoastData_ZeroFinalWeight_PassesValidation()
    {
        RoastData roast = new()
        {
            BeanType = "Ethiopia Yirgacheffe",
            BatchWeight = 100,
            FinalWeight = 0,
            Temperature = 210,
            RoastMinutes = 10,
            RoastSeconds = 30
        };

        roast.Validate().Should().BeEmpty();
    }

    private static BeanData CreateBean()
    {
        return new BeanData
        {
            Id = Guid.NewGuid(),
            Country = "Ethiopia",
            CoffeeName = "Yirgacheffe",
            Variety = "Heirloom",
            RemainingQuantity = 1.5,
            Quantity = 2
        };
    }

    private static RoastPageViewModel CreateViewModel(Action<MockBundle>? setup = null)
    {
        MockBundle mocks = new();
        return CreateViewModel(mocks, setup);
    }

    private static RoastPageViewModel CreateViewModel(MockBundle mocks, Action<MockBundle>? setup = null)
    {

        mocks.PreferencesService.Setup(service => service.IsFirstRunAsync()).ReturnsAsync(false);
        mocks.PreferencesService.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync((string?)null);
        mocks.BeanDataService.Setup(service => service.GetSortedAvailableBeansAsync()).ReturnsAsync(new List<BeanData>());
        mocks.AlertService.Setup(service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        mocks.NavigationService.Setup(service => service.GoToAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mocks.NavigationService.Setup(service => service.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Returns(Task.CompletedTask);
        mocks.TimerService.Setup(service => service.GetElapsedTime()).Returns(TimeSpan.Zero);
        mocks.AppDataService.Setup(service => service.ResetToDefaultPathAsync()).ReturnsAsync(new AppData());
        mocks.AppDataService.Setup(service => service.SetCustomFilePathAsync(It.IsAny<string>())).ReturnsAsync(new AppData());
        mocks.RoastDataService.Setup(service => service.SaveRoastDataAsync(It.IsAny<RoastData>())).ReturnsAsync(true);
        mocks.RoastDataService.Setup(service => service.UpdateRoastLogAsync(It.IsAny<RoastData>())).ReturnsAsync(true);
        mocks.RoastLevelService.Setup(service => service.GetRoastLevelNameAsync(It.IsAny<double>())).ReturnsAsync("Unknown");
        mocks.BeanDataService.Setup(service => service.UpdateBeanQuantityAsync(It.IsAny<Guid>(), It.IsAny<double>())).ReturnsAsync(true);

        setup?.Invoke(mocks);

        return new RoastPageViewModel(
            mocks.TimerService.Object,
            mocks.RoastDataService.Object,
            mocks.BeanDataService.Object,
            mocks.AppDataService.Object,
            mocks.PreferencesService.Object,
            mocks.RoastLevelService.Object,
            mocks.NavigationService.Object,
            mocks.AlertService.Object);
    }

    private static async Task EventuallyAsync(Func<bool> condition, int timeoutMs = 500)
    {
        DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < end)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        condition().Should().BeTrue();
    }

    private sealed class MockBundle
    {
        public Mock<ITimerService> TimerService { get; } = new();
        public Mock<IRoastDataService> RoastDataService { get; } = new();
        public Mock<IBeanDataService> BeanDataService { get; } = new();
        public Mock<IAppDataService> AppDataService { get; } = new();
        public Mock<IPreferencesService> PreferencesService { get; } = new();
        public Mock<IRoastLevelService> RoastLevelService { get; } = new();
        public Mock<INavigationService> NavigationService { get; } = new();
        public Mock<IAlertService> AlertService { get; } = new();
    }
}
