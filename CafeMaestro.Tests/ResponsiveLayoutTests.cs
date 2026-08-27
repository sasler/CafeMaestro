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
}
