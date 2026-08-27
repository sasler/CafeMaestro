using CafeMaestro.Layouts;
using FluentAssertions;

namespace CafeMaestro.Tests;

/// <summary>
/// The tablet content cap: phones must be untouched, wide screens must centre what is left.
/// </summary>
public class ResponsiveLayoutTests
{
    [Theory]
    [InlineData(360, 680)]
    [InlineData(600, 680)]
    [InlineData(680, 680)]
    public void ComputeSideInset_WhenTheScreenIsNarrowerThanTheCap_LeavesContentFullWidth(
        double availableWidth,
        double maxContentWidth)
    {
        ResponsiveLayout.ComputeSideInset(availableWidth, maxContentWidth).Should().Be(0);
    }

    [Fact]
    public void ComputeSideInset_OnATabletWidthPage_CentresTheContentColumn()
    {
        // A 1068 dp tablet in portrait: 388 dp of slack, split evenly.
        ResponsiveLayout.ComputeSideInset(1068, 680).Should().Be(194);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0)]
    [InlineData(-10)]
    public void ComputeSideInset_BeforeTheLayoutHasBeenMeasured_AddsNothing(double availableWidth)
    {
        ResponsiveLayout.ComputeSideInset(availableWidth, 680).Should().Be(0);
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    [InlineData(0)]
    public void ComputeSideInset_WithNoCapSet_AddsNothing(double maxContentWidth)
    {
        ResponsiveLayout.ComputeSideInset(1068, maxContentWidth).Should().Be(0);
    }

    [Theory]
    // A tablet in portrait is still under the cap, so the share decides.
    [InlineData(1068, 457.7)]
    // A tablet in landscape would give the list 686 dp; the cap stops it at 460.
    [InlineData(1600, 460)]
    [InlineData(3000, 460)]
    public void ComputeListPaneWidth_NeverLetsTheListGrowPastItsCap(
        double availableWidth,
        double expected)
    {
        ResponsiveLayout.ComputeListPaneWidth(availableWidth, 3d / 7d, 460)
            .Should().BeApproximately(expected, 0.1);
    }

    [Fact]
    public void ComputeListPaneWidth_AtTheSplitThreshold_StillUsesTheShare()
    {
        ResponsiveLayout.ComputeListPaneWidth(600, 3d / 7d, 460)
            .Should().BeApproximately(257.1, 0.1);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0)]
    public void ComputeListPaneWidth_BeforeTheLayoutHasBeenMeasured_IsZero(double availableWidth)
    {
        ResponsiveLayout.ComputeListPaneWidth(availableWidth, 3d / 7d, 460).Should().Be(0);
    }
}
