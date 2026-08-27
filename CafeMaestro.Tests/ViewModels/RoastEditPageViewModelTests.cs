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
        harness.ViewModel.BatchWeightText = "250";
        harness.ViewModel.FinalWeightText = "205";
        harness.ViewModel.Notes = "  sweeter  ";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.Is<RoastData>(roast =>
            roast.Id == harness.Roast.Id &&
            roast.BeanId == harness.Bean.Id &&
            roast.BeanType == harness.Bean.DisplayName &&
            roast.BatchWeight == 250 &&
            roast.FinalWeight == 205 &&
            roast.Notes == "sweeter" &&
            roast.CompletionStatus == RoastCompletionStatus.Complete)), Times.Once);
        harness.Navigation.Verify(service => service.GoBackAsync(), Times.Once);
    }

    [Fact]
    public async Task Save_AllowsClearingFinalWeightToReturnRoastToWeightQueue()
    {
        Harness harness = new();
        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.FinalWeightText = string.Empty;

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.Is<RoastData>(roast =>
            roast.FinalWeight == null && roast.BatchWeight == harness.Roast.BatchWeight)), Times.Once);
        harness.Navigation.Verify(service => service.GoBackAsync(), Times.Once);
    }

    [Fact]
    public async Task Save_InvalidWeightsShowInlineErrorsAndDoNotPersist()
    {
        Harness harness = new();
        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.BatchWeightText = "240";
        harness.ViewModel.FinalWeightText = "250";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.ViewModel.BatchWeightError.Should().BeEmpty();
        harness.ViewModel.FinalWeightError.Should().Contain("batch weight");
        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.IsAny<RoastData>()), Times.Never);
        harness.Navigation.Verify(service => service.GoBackAsync(), Times.Never);
    }

    [Fact]
    public async Task Save_InvalidTotalTimeDoesNotReportFirstCrackWithinRoastError()
    {
        Harness harness = new();
        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.RoastTimeText = "bad:42";
        harness.ViewModel.FirstCrackTimeText = "10:00";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.ViewModel.RoastTimeError.Should().Be("Enter roast time as mm:ss.");
        harness.ViewModel.FirstCrackError.Should().BeEmpty();
        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.IsAny<RoastData>()), Times.Never);
    }

    [Fact]
    public async Task Save_ValidTotalTimeStillValidatesFirstCrackWithinRoast()
    {
        Harness harness = new();
        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.RoastTimeText = "10:00";
        harness.ViewModel.FirstCrackTimeText = "10:01";

        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.ViewModel.RoastTimeError.Should().BeEmpty();
        harness.ViewModel.FirstCrackError.Should().Contain("within the total roast time");
        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.IsAny<RoastData>()), Times.Never);
    }

    [Fact]
    public async Task Save_WithRenamedCurrentBean_PreservesHistoricalIdentitySnapshot()
    {
        Harness harness = new();
        string historicalBeanType = harness.Roast.BeanType;
        string historicalSnapshot = harness.Roast.BeanDisplaySnapshot;
        BeanData renamedBean = new()
        {
            Id = harness.Bean.Id, Country = harness.Bean.Country, CoffeeName = "Renamed Guji",
            Variety = harness.Bean.Variety, Quantity = 1, RemainingQuantity = 1
        };
        harness.Beans.Setup(service => service.GetSortedAvailableBeansAsync())
            .ReturnsAsync([renamedBean]);

        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.SelectedBean.Should().BeSameAs(renamedBean);
        harness.ViewModel.Notes = "  note correction  ";
        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.Is<RoastData>(roast =>
            roast.BeanId == harness.Bean.Id &&
            roast.BeanType == historicalBeanType &&
            roast.BeanDisplaySnapshot == historicalSnapshot &&
            roast.Notes == "note correction")), Times.Once);
    }

    [Fact]
    public async Task Save_WithExplicitDifferentBeanSelection_UpdatesIdentity()
    {
        Harness harness = new();
        BeanData replacement = new()
        {
            Id = Guid.NewGuid(), Country = "Colombia", CoffeeName = "Huila",
            Variety = "Caturra", Quantity = 1, RemainingQuantity = 1
        };
        harness.Beans.Setup(service => service.GetSortedAvailableBeansAsync())
            .ReturnsAsync([harness.Bean, replacement]);

        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.SelectedBean = replacement;
        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.Is<RoastData>(roast =>
            roast.BeanId == replacement.Id &&
            roast.BeanType == replacement.DisplayName &&
            roast.BeanDisplaySnapshot == replacement.DisplayName)), Times.Once);
    }

    [Fact]
    public async Task Save_WithDeletedBean_PreservesHistoricalIdentity()
    {
        Harness harness = new();
        Guid deletedBeanId = Guid.NewGuid();
        harness.Roast.BeanId = deletedBeanId;
        harness.Roast.BeanType = "Deleted Guji";
        harness.Roast.BeanDisplaySnapshot = "Deleted Guji";

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.SelectedBean.Should().BeNull();
        harness.ViewModel.Notes = "  corrected  ";
        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.Is<RoastData>(roast =>
            roast.BeanId == deletedBeanId &&
            roast.BeanType == "Deleted Guji" &&
            roast.BeanDisplaySnapshot == "Deleted Guji" &&
            roast.Notes == "corrected")), Times.Once);
        harness.Navigation.Verify(service => service.GoBackAsync(), Times.Once);
    }

    [Fact]
    public async Task Save_WithAmbiguousLegacyIdentity_PreservesHistoricalIdentity()
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
        harness.ViewModel.Notes = "  legacy correction  ";
        await harness.ViewModel.SaveCommand.ExecuteAsync(null);

        harness.Roasts.Verify(service => service.UpdateRoastLogAsync(It.Is<RoastData>(roast =>
            roast.BeanId == null &&
            roast.BeanType == harness.Bean.DisplayName &&
            roast.BeanDisplaySnapshot == harness.Bean.DisplayName &&
            roast.Notes == "legacy correction")), Times.Once);
        harness.Navigation.Verify(service => service.GoBackAsync(), Times.Once);
    }

    [Fact]
    public async Task OnAppearing_LoadsEditableBatchAndFinalWeights()
    {
        Harness harness = new();

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.BatchWeightText.Should().Be("240");
        harness.ViewModel.FinalWeightText.Should().Be("206");
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
            ViewModel = new RoastEditPageViewModel(
                Roasts.Object,
                Beans.Object,
                Navigation.Object,
                Mock.Of<IAlertService>());
            ViewModel.EditRoastId = Roast.Id.ToString();
        }
    }
}
