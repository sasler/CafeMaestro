using CafeMaestro.Drawing;
using FluentAssertions;

namespace CafeMaestro.Tests.Drawing;

public class RoastInstrumentGeometryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 0.25)]
    [InlineData(59, 59d / 60d)]
    [InlineData(60, 0)]
    [InlineData(75, 0.25)]
    public void ElapsedSweep_RepeatsEverySixtySeconds(double seconds, double expected) =>
        RoastInstrumentGeometry.ElapsedSweep(seconds).Should().BeApproximately(expected, 0.000001);

    [Theory]
    [InlineData(300, 300, 1)]
    [InlineData(150, 300, 0.5)]
    [InlineData(0, 300, 0)]
    [InlineData(-5, 300, 0)]
    [InlineData(400, 300, 1)]
    public void CoolingProgress_ClampsAtReadyAndFull(double remaining, double duration, double expected) =>
        RoastInstrumentGeometry.CoolingProgress(remaining, duration).Should().Be(expected);
}
