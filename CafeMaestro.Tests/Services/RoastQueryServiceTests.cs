using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;

namespace CafeMaestro.Tests.Services;

/// <summary>
/// Covers the projections the setup screen depends on: which roast supplies carry-forward
/// values, which one is the honest reference result, and how legacy rows attach to a bean.
/// </summary>
public sealed class RoastQueryServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetSetupSuggestion_UsesTheNewestCompleteRoastAsTheReferenceResult()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();
        await AddRoastsAsync(
            harness,
            Completed(bean, Start.AddDays(-3), temperature: 214, batchWeight: 200, finalWeight: 172),
            Completed(bean, Start.AddDays(-2), temperature: 218, batchWeight: 240, finalWeight: 206),
            AwaitingWeight(bean, Start.AddHours(-1), temperature: 220, batchWeight: 250),
            Unweighed(bean, Start.AddMinutes(-30), temperature: 222, batchWeight: 260),
            Discarded(bean, Start.AddMinutes(-10), temperature: 240, batchWeight: 300));

        RoastSetupSuggestion suggestion = await harness.Query.GetSetupSuggestionAsync(bean.Id);

        // The newest non-discarded roast supplies the numbers to pre-fill...
        suggestion.Temperature.Should().Be(222);
        suggestion.BatchWeight.Should().Be(260);
        // ...but only a completed roast may present itself as the last usable result.
        suggestion.LastCompletedRoast!.FinalWeight.Should().Be(206);
        suggestion.LastCompletedRoast.Temperature.Should().Be(218);
        suggestion.NewerAwaitingWeightCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSetupSuggestion_WithNoHistory_OffersNoInventedDefaults()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();

        RoastSetupSuggestion suggestion = await harness.Query.GetSetupSuggestionAsync(bean.Id);

        suggestion.HasHistory.Should().BeFalse();
        suggestion.Temperature.Should().BeNull();
        suggestion.BatchWeight.Should().BeNull();
        suggestion.LastCompletedRoast.Should().BeNull();
    }

    [Fact]
    public async Task GetRoastsForBean_MatchesALegacyRowByItsExactUniqueDisplaySnapshot()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync(coffeeName: "Guji");
        RoastData legacy = Completed(
            bean,
            Start.AddDays(-1),
            temperature: 216,
            batchWeight: 230,
            finalWeight: 198);
        legacy.BeanId = null;
        await AddRoastsAsync(harness, legacy);

        IReadOnlyList<RoastData> roasts = await harness.Query.GetRoastsForBeanAsync(bean.Id);

        roasts.Should().ContainSingle().Which.Id.Should().Be(legacy.Id);
        (await harness.Query.GetSetupSuggestionAsync(bean.Id)).Temperature.Should().Be(216);
    }

    [Fact]
    public async Task GetRoastsForBean_LeavesAnAmbiguousLegacyRowUnlinked()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData first = await harness.AddBeanAsync(coffeeName: "Guji");
        BeanData second = await harness.AddBeanAsync(coffeeName: "Guji");
        first.DisplayName.Should().Be(second.DisplayName);
        RoastData legacy = Completed(
            first,
            Start.AddDays(-1),
            temperature: 216,
            batchWeight: 230,
            finalWeight: 198);
        legacy.BeanId = null;
        await AddRoastsAsync(harness, legacy);

        (await harness.Query.GetRoastsForBeanAsync(first.Id)).Should().BeEmpty();
        (await harness.Query.GetRoastsForBeanAsync(second.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoastsForBean_IgnoresARenameBecauseSnapshotsAreNeverRewritten()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync(coffeeName: "Guji");
        await AddRoastsAsync(
            harness,
            Completed(bean, Start.AddDays(-1), temperature: 218, batchWeight: 240, finalWeight: 206));

        (await harness.AppDataService.UpdateAsync(data =>
            data.Beans.Single(candidate => candidate.Id == bean.Id).CoffeeName = "Guji Lot 12"))
            .Should().BeTrue();

        IReadOnlyList<RoastData> roasts = await harness.Query.GetRoastsForBeanAsync(bean.Id);

        roasts.Should().ContainSingle();
        roasts[0].BeanDisplaySnapshot.Should().Be("Ethiopia - Guji (Heirloom)");
        harness.Current.Beans.Single(candidate => candidate.Id == bean.Id).DisplayName
            .Should().Be("Ethiopia - Guji Lot 12 (Heirloom)");
    }

    [Fact]
    public async Task GetOpenWork_OrdersTheQueueOldestDropFirst()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();
        RoastData older = AwaitingWeight(bean, Start.AddMinutes(-20), 218, 240);
        RoastData newer = AwaitingWeight(bean, Start.AddMinutes(-2), 218, 240);
        await AddRoastsAsync(
            harness,
            newer,
            older,
            Completed(bean, Start.AddDays(-1), 218, 240, 206));

        IReadOnlyList<RoastWorkItem> queue = await harness.Query.GetOpenWorkAsync();

        queue.Select(item => item.RoastId).Should().Equal(older.Id, newer.Id);
        queue[0].Status.Should().Be(RoastEffectiveStatus.NeedsWeight);
        queue[1].Status.Should().Be(RoastEffectiveStatus.Cooling);
    }

    private static async Task AddRoastsAsync(
        RoastSessionTestHarness harness,
        params RoastData[] roasts)
    {
        (await harness.AppDataService.UpdateAsync(data => data.RoastLogs.AddRange(roasts)))
            .Should().BeTrue();
    }

    private static RoastData CreateRoast(
        BeanData bean,
        DateTimeOffset droppedAtUtc,
        double temperature,
        double batchWeight) => new()
        {
            Id = Guid.NewGuid(),
            BeanId = bean.Id,
            BeanDisplaySnapshot = bean.DisplayName,
            BeanType = bean.DisplayName,
            Temperature = temperature,
            BatchWeight = batchWeight,
            RoastMinutes = 11,
            RoastSeconds = 5,
            RoastDate = droppedAtUtc.ToLocalTime().DateTime,
            DroppedAtUtc = droppedAtUtc
        };

    private static RoastData Completed(
        BeanData bean,
        DateTimeOffset droppedAtUtc,
        double temperature,
        double batchWeight,
        double finalWeight)
    {
        RoastData roast = CreateRoast(bean, droppedAtUtc, temperature, batchWeight);
        roast.FinalWeight = finalWeight;
        roast.CompletionStatus = RoastCompletionStatus.Complete;
        roast.RoastLevelName = "Medium";
        return roast;
    }

    private static RoastData AwaitingWeight(
        BeanData bean,
        DateTimeOffset droppedAtUtc,
        double temperature,
        double batchWeight)
    {
        RoastData roast = CreateRoast(bean, droppedAtUtc, temperature, batchWeight);
        roast.CompletionStatus = RoastCompletionStatus.AwaitingWeight;
        roast.CoolingDurationSeconds = RoastPreferenceDefaults.CoolingDurationSeconds;
        roast.RoastLevelName = "Pending";
        return roast;
    }

    private static RoastData Unweighed(
        BeanData bean,
        DateTimeOffset droppedAtUtc,
        double temperature,
        double batchWeight)
    {
        RoastData roast = CreateRoast(bean, droppedAtUtc, temperature, batchWeight);
        roast.CompletionStatus = RoastCompletionStatus.Unweighed;
        return roast;
    }

    private static RoastData Discarded(
        BeanData bean,
        DateTimeOffset droppedAtUtc,
        double temperature,
        double batchWeight)
    {
        RoastData roast = CreateRoast(bean, droppedAtUtc, temperature, batchWeight);
        roast.CompletionStatus = RoastCompletionStatus.Discarded;
        return roast;
    }
}
