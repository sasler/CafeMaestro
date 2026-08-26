using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels.Popups;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class WeighInViewModelTests
{
    [Fact]
    public void OpenBatch_WeighsInAndCanStillBeRecordedAsNeverWeighed()
    {
        var viewModel = CreateViewModel();

        viewModel.Request = Request(initialFinalWeight: null);

        viewModel.ActionTitle.Should().Be("WEIGH IN");
        viewModel.CanMarkUnweighed.Should().BeTrue();
        viewModel.FinalWeightText.Should().BeEmpty();
    }

    [Fact]
    public void CompletedRoast_PrefillsTheResultAndHidesTheNeverWeighedEscape()
    {
        var viewModel = CreateViewModel();

        viewModel.Request = Request(initialFinalWeight: 206);

        viewModel.ActionTitle.Should().Be("EDIT FINAL WEIGHT");
        viewModel.CanMarkUnweighed.Should().BeFalse();
        viewModel.FinalWeightText.Should().Be(206d.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture));
        viewModel.CanSave.Should().BeTrue();
    }

    private static WeighInViewModel CreateViewModel() =>
        new(Mock.Of<IRoastSessionService>(), Mock.Of<IOverlayService>());

    private static WeighInRequest Request(double? initialFinalWeight) => new()
    {
        RoastId = Guid.NewGuid(),
        BatchNumber = 1,
        BeanDisplaySnapshot = "Ethiopia - Guji",
        BatchWeight = 240,
        DroppedAtUtc = new DateTimeOffset(2026, 8, 25, 14, 35, 0, TimeSpan.Zero),
        TotalSeconds = 644,
        InitialFinalWeight = initialFinalWeight
    };
}
