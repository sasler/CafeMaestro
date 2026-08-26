using System.Globalization;
using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;

namespace CafeMaestro.Tests.ViewModels;

public sealed class RoastLogCardTests
{
    [Fact]
    public void BeanHistoryEntry_DefaultLegacyDate_UsesCanonicalDropDateProjection()
    {
        RoastData legacy = new()
        {
            BeanType = "Legacy Guji",
            BeanDisplaySnapshot = "Legacy Guji",
            Temperature = 218,
            BatchWeight = 240,
            FinalWeight = 205,
            RoastDate = default,
            DroppedAtUtc = null,
            CompletionStatus = RoastCompletionStatus.Complete,
            RoastMinutes = 10,
            RoastSeconds = 42
        };

        Action project = () => BeanRoastHistoryEntry.FromHistory(legacy);

        project.Should().NotThrow();
        BeanRoastHistoryEntry entry = BeanRoastHistoryEntry.FromHistory(legacy);
        entry.DateDisplay.Should().Be(
            RoastProjection.DroppedAtUtc(legacy)
                .ToLocalTime()
                .ToString("d MMM yyyy · HH:mm", CultureInfo.CurrentCulture));
    }

    [Fact]
    public void BeanHistoryEntry_CompleteRoast_SplitsSettingsFromResultWithoutRepeatingTheStatus()
    {
        BeanRoastHistoryEntry entry = BeanRoastHistoryEntry.FromHistory(new RoastData
        {
            BeanType = "Guji", BeanDisplaySnapshot = "Guji", Temperature = 218,
            BatchWeight = 240, FinalWeight = 205, BatchNumber = 1,
            RoastMinutes = 10, RoastSeconds = 42, RoastDate = new DateTime(2026, 3, 12, 9, 0, 0),
            DroppedAtUtc = new DateTimeOffset(2026, 3, 12, 9, 0, 0, TimeSpan.Zero),
            CompletionStatus = RoastCompletionStatus.Complete, RoastLevelName = "Medium"
        });

        // Line one is what a roaster would reuse; line two is what it produced.
        entry.SettingsDisplay.Should().Be("218 °C · 240 g in · Batch 1");
        entry.ResultDisplay.Should().Be("205 g out · 14.6% loss · Medium");

        // A loss and a level already say the roast completed; repeating it wastes a line.
        entry.ResultDisplay.Should().NotContain("Complete");
        entry.IsMuted.Should().BeFalse();
    }

    [Theory]
    [InlineData(RoastCompletionStatus.AwaitingWeight, "Needs weight")]
    [InlineData(RoastCompletionStatus.Unweighed, "Unweighed")]
    [InlineData(RoastCompletionStatus.Discarded, "Discarded")]
    public void BeanHistoryEntry_WithoutAResult_ShowsWhyOnLineTwoAndDimsTheWholeRow(
        RoastCompletionStatus status,
        string expectedResult)
    {
        BeanRoastHistoryEntry entry = BeanRoastHistoryEntry.FromHistory(new RoastData
        {
            BeanType = "Guji", BeanDisplaySnapshot = "Guji", Temperature = 218,
            BatchWeight = 240, FinalWeight = null, BatchNumber = 2,
            RoastMinutes = 10, RoastSeconds = 42, RoastDate = new DateTime(2026, 3, 12, 9, 0, 0),
            DroppedAtUtc = new DateTimeOffset(2026, 3, 12, 9, 0, 0, TimeSpan.Zero),
            CompletionStatus = status
        });

        entry.SettingsDisplay.Should().Be("218 °C · 240 g in · Batch 2");
        entry.ResultDisplay.Should().Be(expectedResult);
        entry.IsMuted.Should().BeTrue();

        // The settings are still reusable even when the batch produced no result.
        entry.Temperature.Should().Be(218);
        entry.BatchWeight.Should().Be(240);
    }
}
