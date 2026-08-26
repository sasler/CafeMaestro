using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

/// <summary>
/// Covers the transition invariants that make the roast console durable: Start only becomes
/// visible once persisted, elapsed time derives from anchors, Drop applies exactly once, and a
/// cold launch restores from stored facts rather than guessing.
/// </summary>
public sealed class RoastSessionServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_PersistsDraftBeforeReportingRoasting()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();

        TransitionResult result = await harness.Session.StartAsync(
            new RoastSetup(bean.Id, 218, 240));

        result.Success.Should().BeTrue();
        result.Snapshot.ActiveRoast.Should().NotBeNull();
        result.Snapshot.ActiveRoast!.Phase.Should().Be(ActiveRoastPhase.Roasting);
        result.Snapshot.ActiveRoast.BatchNumber.Should().Be(1);
        result.Snapshot.RequiresRecovery.Should().BeFalse();

        AppData persisted = await harness.AppDataService.LoadAppDataAsync();
        persisted.ActiveRoastSession!.ActiveRoast!.Id.Should().Be(result.Snapshot.ActiveRoast.Id);
        persisted.ActiveRoastSession.ActiveRoast.BeanDisplaySnapshot.Should().Be(bean.DisplayName);
    }

    [Fact]
    public async Task StartAsync_WhenTheDraftCannotBePersisted_LeavesNoRoastAndNoInventoryChange()
    {
        // Bean creation must still succeed; only the write that carries a draft is rejected.
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(
            Start,
            (data, _) => data.ActiveRoastSession is null
                ? Task.CompletedTask
                : throw new IOException("Injected start failure."));
        BeanData bean = await harness.AddBeanAsync();

        TransitionResult result = await harness.Session.StartAsync(
            new RoastSetup(bean.Id, 218, 240));

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.PersistenceFailed);
        result.Snapshot.ActiveRoast.Should().BeNull();
        harness.Current.ActiveRoastSession.Should().BeNull();
        harness.RemainingQuantityOf(bean.Id).Should().Be(1.0);
    }

    [Fact]
    public async Task StartAsync_WhileABatchIsRoasting_IsRefused()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        TransitionResult second = await harness.Session.StartAsync(
            new RoastSetup(bean.Id, 218, 240));

        second.Success.Should().BeFalse();
        second.Error.Should().Be(RoastTransitionError.ActiveRoastAlreadyExists);
        harness.Current.ActiveRoastSession!.NextBatchNumber.Should().Be(1);
    }

    [Fact]
    public async Task PauseResume_AcrossMultipleIntervals_KeepsElapsedAndFirstCrackCorrect()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        harness.Preferences.FirstCrackEnabled = true;
        BeanData bean = await harness.AddBeanAsync();
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        harness.Clock.AdvanceSeconds(60);
        (await harness.Session.PauseAsync()).Snapshot.ActiveRoast!.ElapsedSeconds
            .Should().Be(60);

        // A pause holds the value no matter how long the app sits there.
        harness.Clock.AdvanceSeconds(600);
        (await harness.Session.GetSnapshotAsync()).ActiveRoast!.ElapsedSeconds.Should().Be(60);

        (await harness.Session.ResumeAsync()).Success.Should().BeTrue();
        harness.Clock.AdvanceSeconds(30);
        (await harness.Session.MarkFirstCrackAsync()).Success.Should().BeTrue();
        harness.Clock.AdvanceSeconds(45);

        ActiveRoastSnapshot active = (await harness.Session.GetSnapshotAsync()).ActiveRoast!;
        active.ElapsedSeconds.Should().Be(135);
        active.FirstCrackElapsedSeconds.Should().Be(90);
        active.DevelopmentSeconds.Should().Be(45);

        RoastSessionSnapshot dropped = (await harness.Session.DropAsync()).Snapshot;
        dropped.ActiveRoast.Should().BeNull();
        RoastData roast = harness.Current.RoastLogs.Single();
        roast.TotalSeconds.Should().Be(135);
        roast.FirstCrackMinutes.Should().Be(1);
        roast.FirstCrackSeconds.Should().Be(30);
    }

    [Fact]
    public async Task MarkFirstCrackAsync_WhenTheBatchStartedWithTrackingOff_IsRefused()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        // Enabling the preference mid-roast must not rewrite the snapshot taken at Start.
        harness.Preferences.FirstCrackEnabled = true;
        TransitionResult result = await harness.Session.MarkFirstCrackAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.FirstCrackUnavailable);
    }

    [Fact]
    public async Task ResetAsync_WhilePaused_ClearsElapsedAndFirstCrackWithoutReplacingDraft()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        harness.Preferences.FirstCrackEnabled = true;
        BeanData bean = await harness.AddBeanAsync();
        ActiveRoastSnapshot started = (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240)))
            .Snapshot.ActiveRoast!;
        harness.Clock.AdvanceSeconds(90);
        await harness.Session.MarkFirstCrackAsync();
        await harness.Session.PauseAsync();

        TransitionResult result = await harness.Session.ResetAsync();

        result.Success.Should().BeTrue();
        result.Snapshot.ActiveRoast!.Id.Should().Be(started.Id);
        result.Snapshot.ActiveRoast.ElapsedSeconds.Should().Be(0);
        result.Snapshot.ActiveRoast.FirstCrackElapsedSeconds.Should().BeNull();
        result.Snapshot.ActiveRoast.Phase.Should().Be(ActiveRoastPhase.Paused);
    }

    [Fact]
    public async Task DropAsync_AppliedTwice_ProducesOneRoastAndOneInventoryDecrement()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync(quantityKilograms: 1.0);
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        harness.Clock.AdvanceSeconds(665);

        TransitionResult first = await harness.Session.DropAsync();
        TransitionResult second = await harness.Session.DropAsync();

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.Snapshot.ActiveRoast.Should().BeNull();
        harness.Current.RoastLogs.Should().ContainSingle();
        harness.RemainingQuantityOf(bean.Id).Should().Be(0.76);

        RoastData roast = harness.Current.RoastLogs.Single();
        roast.CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
        roast.FinalWeight.Should().BeNull();
        roast.DroppedAtUtc.Should().Be(Start.AddSeconds(665));
        roast.CoolingDurationSeconds.Should().Be(RoastPreferenceDefaults.CoolingDurationSeconds);
    }

    [Fact]
    public async Task DropAsync_WithNothingRoasting_IsRefused()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        await harness.AddBeanAsync();

        TransitionResult result = await harness.Session.DropAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.NoActiveRoast);
        harness.Current.RoastLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task DropAsync_AfterAFailedWrite_RetriesWithTheSameIdAndDecrementsOnce()
    {
        int writeAttempts = 0;
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(
            Start,
            (data, _) =>
            {
                // Fail only the write that clears the draft, which is the drop commit.
                if (data.RoastLogs.Count == 1 && Interlocked.Increment(ref writeAttempts) == 1)
                {
                    throw new IOException("Injected drop failure.");
                }

                return Task.CompletedTask;
            });
        BeanData bean = await harness.AddBeanAsync(quantityKilograms: 1.0);
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        Guid draftId = harness.Current.ActiveRoastSession!.ActiveRoast!.Id;
        harness.Clock.AdvanceSeconds(600);

        TransitionResult failed = await harness.Session.DropAsync();

        failed.Success.Should().BeFalse();
        failed.Error.Should().Be(RoastTransitionError.PersistenceFailed);
        harness.Current.ActiveRoastSession!.ActiveRoast!.Id.Should().Be(draftId);
        harness.Current.RoastLogs.Should().BeEmpty();
        harness.RemainingQuantityOf(bean.Id).Should().Be(1.0);

        TransitionResult retried = await harness.Session.DropAsync();

        retried.Success.Should().BeTrue();
        harness.Current.RoastLogs.Should().ContainSingle(roast => roast.Id == draftId);
        harness.RemainingQuantityOf(bean.Id).Should().Be(0.76);
    }

    [Fact]
    public async Task CorrectDropAsync_ChangesDurationAndCoolingAnchorWithoutSecondInventoryUse()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync(quantityKilograms: 1.0);
        await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240));
        harness.Clock.AdvanceSeconds(665);
        RoastWorkItem dropped = (await harness.Session.DropAsync()).Snapshot.OpenWork.Single();
        harness.Clock.AdvanceSeconds(30);

        TransitionResult result = await harness.Session.CorrectDropAsync(
            dropped.RoastId,
            dropped.DroppedAtUtc.AddSeconds(-5));

        result.Success.Should().BeTrue();
        RoastData roast = harness.Current.RoastLogs.Single();
        roast.TotalSeconds.Should().Be(660);
        roast.DroppedAtUtc.Should().Be(dropped.DroppedAtUtc.AddSeconds(-5));
        harness.RemainingQuantityOf(bean.Id).Should().Be(0.76);
    }

    [Fact]
    public async Task BackToBackBatches_NumberSequentiallyAndCarryTheSameSetupForward()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync(quantityKilograms: 1.0);

        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        harness.Clock.AdvanceSeconds(660);
        (await harness.Session.DropAsync()).Success.Should().BeTrue();

        // Batch 2 copies the just-dropped batch's values even though it still needs a weight.
        RoastSetupSuggestion suggestion = await harness.Query.GetSetupSuggestionAsync(bean.Id);
        suggestion.Temperature.Should().Be(218);
        suggestion.BatchWeight.Should().Be(240);
        suggestion.LastCompletedRoast.Should().BeNull();
        suggestion.NewerAwaitingWeightCount.Should().Be(1);

        harness.Clock.AdvanceSeconds(120);
        TransitionResult batchTwo = await harness.Session.StartAsync(
            new RoastSetup(bean.Id, suggestion.Temperature!.Value, suggestion.BatchWeight!.Value));

        batchTwo.Snapshot.ActiveRoast!.BatchNumber.Should().Be(2);
        batchTwo.Snapshot.ActiveRoast.SessionId.Should().Be(batchTwo.Snapshot.SessionId!.Value);

        harness.Clock.AdvanceSeconds(645);
        (await harness.Session.DropAsync()).Success.Should().BeTrue();

        harness.Current.ActiveRoastSession!.NextBatchNumber.Should().Be(3);
        harness.Current.RoastLogs.Select(roast => roast.BatchNumber).Should().Equal(1, 2);
        harness.Current.RoastLogs.Select(roast => roast.SessionId).Distinct().Should().ContainSingle();
        harness.RemainingQuantityOf(bean.Id).Should().Be(0.52);
    }

    [Fact]
    public async Task OpenWork_ProjectsCoolingUntilReadinessAndNeedsWeightAfterIt()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        harness.Clock.AdvanceSeconds(600);
        (await harness.Session.DropAsync()).Success.Should().BeTrue();

        RoastWorkItem cooling = (await harness.Session.GetSnapshotAsync()).OpenWork.Single();
        cooling.Status.Should().Be(RoastEffectiveStatus.Cooling);
        cooling.RemainingCoolingSeconds.Should().Be(300);

        harness.Clock.AdvanceSeconds(300);

        RoastWorkItem ready = (await harness.Session.GetSnapshotAsync()).OpenWork.Single();
        ready.Status.Should().Be(RoastEffectiveStatus.NeedsWeight);
        ready.RemainingCoolingSeconds.Should().Be(0);
        // Readiness is a projection: no write was needed for the transition at zero.
        harness.Current.RoastLogs.Single().CompletionStatus
            .Should().Be(RoastCompletionStatus.AwaitingWeight);
    }

    [Fact]
    public async Task SaveFinalWeightAsync_ThenMarkUnweighedAsync_AcceptsOnlyTheFirstTransition()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        Guid roastId = await DropOneBatchAsync(harness);

        TransitionResult saved = await harness.Session.SaveFinalWeightAsync(roastId, 206.04);
        TransitionResult unweighed = await harness.Session.MarkUnweighedAsync(roastId);

        saved.Success.Should().BeTrue();
        unweighed.Success.Should().BeFalse();
        unweighed.Error.Should().Be(RoastTransitionError.RoastAlreadyResolved);

        RoastData roast = harness.Current.RoastLogs.Single();
        roast.CompletionStatus.Should().Be(RoastCompletionStatus.Complete);
        roast.FinalWeight.Should().Be(206.0);
        roast.RoastLevelName.Should().Be("Medium");
        harness.Notifications.Cancelled.Should().ContainSingle().Which.Should().Be(roastId);
    }

    [Fact]
    public async Task SaveFinalWeightAsync_OnCompletedRoast_UpdatesTheFocusedResult()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        Guid roastId = await DropOneBatchAsync(harness);
        (await harness.Session.SaveFinalWeightAsync(roastId, 206)).Success.Should().BeTrue();

        TransitionResult edited = await harness.Session.SaveFinalWeightAsync(roastId, 204.96);

        edited.Success.Should().BeTrue();
        RoastData roast = harness.Current.RoastLogs.Single();
        roast.FinalWeight.Should().Be(205.0);
        roast.CompletionStatus.Should().Be(RoastCompletionStatus.Complete);
        roast.RoastLevelName.Should().Be("Medium");
    }

    [Fact]
    public async Task MarkUnweighedAsync_ThenSaveFinalWeightAsync_AcceptsOnlyTheFirstTransition()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        Guid roastId = await DropOneBatchAsync(harness);

        TransitionResult unweighed = await harness.Session.MarkUnweighedAsync(roastId);
        TransitionResult saved = await harness.Session.SaveFinalWeightAsync(roastId, 206);

        unweighed.Success.Should().BeTrue();
        saved.Success.Should().BeFalse();
        saved.Error.Should().Be(RoastTransitionError.RoastAlreadyResolved);

        RoastData roast = harness.Current.RoastLogs.Single();
        roast.CompletionStatus.Should().Be(RoastCompletionStatus.Unweighed);
        roast.FinalWeight.Should().BeNull();
    }

    [Fact]
    public async Task SaveFinalWeightAsync_AboveTheBatchWeight_IsRefusedWithoutTouchingTheRoast()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        Guid roastId = await DropOneBatchAsync(harness);

        TransitionResult result = await harness.Session.SaveFinalWeightAsync(roastId, 246);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.InvalidWeight);
        harness.Current.RoastLogs.Single().CompletionStatus
            .Should().Be(RoastCompletionStatus.AwaitingWeight);
    }

    [Fact]
    public async Task DropAsync_WhenSchedulingTheReminderFails_KeepsTheRoastSaved()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        harness.Preferences.CoolingNotificationsEnabled = true;
        harness.Notifications.ThrowOnSchedule = true;
        BeanData bean = await harness.AddBeanAsync();
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        harness.Clock.AdvanceSeconds(600);

        TransitionResult result = await harness.Session.DropAsync();

        result.Success.Should().BeTrue();
        result.Warning.Should().NotBeNullOrWhiteSpace();
        harness.Current.RoastLogs.Should().ContainSingle();
        result.Snapshot.OpenWork.Should().ContainSingle();
    }

    [Fact]
    public async Task FinishSessionAsync_LeavesOpenWorkAndIsBlockedWhileABatchRoasts()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();
        await DropOneBatchAsync(harness, bean);
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        TransitionResult blocked = await harness.Session.FinishSessionAsync();
        blocked.Success.Should().BeFalse();
        blocked.Error.Should().Be(RoastTransitionError.ActiveRoastBlocksAction);

        (await harness.Session.DiscardAsync(beansWereUsed: false, keepLog: false)).Success
            .Should().BeTrue();
        TransitionResult finished = await harness.Session.FinishSessionAsync();

        finished.Success.Should().BeTrue();
        finished.Snapshot.HasSession.Should().BeFalse();
        // Finishing the sitting never resolves a cooling or needs-weight obligation.
        finished.Snapshot.OpenWork.Should().ContainSingle();
        harness.Current.ActiveRoastSession.Should().BeNull();
    }

    [Fact]
    public async Task DiscardAsync_WithoutUsedBeans_ClearsTheDraftAndLeavesInventoryIntact()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync(quantityKilograms: 1.0);
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        harness.Clock.AdvanceSeconds(120);

        TransitionResult result = await harness.Session.DiscardAsync(
            beansWereUsed: false,
            keepLog: false);

        result.Success.Should().BeTrue();
        result.Snapshot.ActiveRoast.Should().BeNull();
        harness.Current.RoastLogs.Should().BeEmpty();
        harness.RemainingQuantityOf(bean.Id).Should().Be(1.0);
    }

    [Fact]
    public async Task DiscardAsync_LoggingAFailedRoast_RecordsItWithoutOwingAWeight()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync(quantityKilograms: 1.0);
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        harness.Clock.AdvanceSeconds(90);

        TransitionResult result = await harness.Session.DiscardAsync(
            beansWereUsed: true,
            keepLog: true);

        result.Success.Should().BeTrue();
        result.Snapshot.OpenWork.Should().BeEmpty();
        RoastData roast = harness.Current.RoastLogs.Single();
        roast.CompletionStatus.Should().Be(RoastCompletionStatus.Discarded);
        roast.FinalWeight.Should().BeNull();
        roast.TotalSeconds.Should().Be(90);
        harness.RemainingQuantityOf(bean.Id).Should().Be(0.76);
    }

    [Fact]
    public async Task ColdLaunchWithALiveRoast_RequiresRecoveryAndKeepsElapsedDerivedFromAnchors()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(Start.AddSeconds(751));
        RoastSessionSnapshot snapshot = await relaunched.Session.GetSnapshotAsync();

        snapshot.RequiresRecovery.Should().BeTrue();
        snapshot.ActiveRoast!.ElapsedSeconds.Should().Be(751);

        TransitionResult kept = await relaunched.Session.RecoverAsync(
            RecoveryDecision.KeepRoasting());

        kept.Success.Should().BeTrue();
        kept.Snapshot.RequiresRecovery.Should().BeFalse();
        kept.Snapshot.ActiveRoast!.ElapsedSeconds.Should().Be(751);
        relaunched.Clock.AdvanceSeconds(30);
        (await relaunched.Session.GetSnapshotAsync()).ActiveRoast!.ElapsedSeconds
            .Should().Be(781);
    }

    [Fact]
    public async Task RecoverAsync_AfterTheDeviceClockRolledBack_NeverProducesNegativeElapsedTime()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        original.Clock.AdvanceSeconds(300);
        (await original.Session.PauseAsync()).Success.Should().BeTrue();
        (await original.Session.ResumeAsync()).Success.Should().BeTrue();

        // The device clock jumps two hours backwards while the app is closed.
        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(Start.AddSeconds(300).AddHours(-2));
        RoastSessionSnapshot snapshot = await relaunched.Session.GetSnapshotAsync();

        snapshot.ActiveRoast!.ElapsedSeconds.Should().Be(300);
        snapshot.ActiveRoast.IsElapsedImplausible.Should().BeTrue();
        snapshot.ActiveRoast.RequiresCorrectedElapsed.Should().BeTrue();
        snapshot.RequiresRecovery.Should().BeTrue();

        TransitionResult kept = await relaunched.Session.RecoverAsync(
            RecoveryDecision.KeepRoasting(300));

        kept.Success.Should().BeTrue();
        kept.Snapshot.ActiveRoast!.ElapsedSeconds.Should().Be(300);
        kept.Snapshot.ActiveRoast.IsElapsedImplausible.Should().BeFalse();
        relaunched.Clock.AdvanceSeconds(60);
        (await relaunched.Session.GetSnapshotAsync()).ActiveRoast!.ElapsedSeconds
            .Should().Be(360);
    }

    [Fact]
    public async Task RecoverAsync_WhenANeverPausedClockRollsBack_RequiresExplicitElapsed()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(Start.AddMinutes(-30));

        TransitionResult refused = await relaunched.Session.RecoverAsync(
            RecoveryDecision.KeepRoasting());

        refused.Success.Should().BeFalse();
        refused.Error.Should().Be(RoastTransitionError.CorrectedElapsedRequired);
        refused.Snapshot.RequiresRecovery.Should().BeTrue();

        TransitionResult corrected = await relaunched.Session.RecoverAsync(
            RecoveryDecision.KeepRoasting(420));

        corrected.Success.Should().BeTrue();
        corrected.Snapshot.RequiresRecovery.Should().BeFalse();
        corrected.Snapshot.ActiveRoast!.ElapsedSeconds.Should().Be(420);
        corrected.Snapshot.ActiveRoast.RequiresCorrectedElapsed.Should().BeFalse();
        relaunched.Clock.AdvanceSeconds(30);
        (await relaunched.Session.GetSnapshotAsync()).ActiveRoast!.ElapsedSeconds.Should().Be(450);
    }

    [Fact]
    public async Task RecoverAsync_WhenKeepRoastingWriteFails_LeavesRecoveryOutstanding()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        using RoastSessionTestHarness relaunched = await RoastSessionTestHarness.ReopenAsync(
            original.CanonicalPath,
            Start.AddSeconds(600),
            (data, _) => data.ActiveRoastSession?.ActiveRoast is not null
                ? throw new IOException("Injected keep-roasting failure.")
                : Task.CompletedTask);

        TransitionResult result = await relaunched.Session.RecoverAsync(
            RecoveryDecision.KeepRoasting());

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.PersistenceFailed);
        result.Snapshot.RequiresRecovery.Should().BeTrue();
        (await relaunched.Session.GetSnapshotAsync()).RequiresRecovery.Should().BeTrue();
    }

    [Fact]
    public async Task RecoverAsync_WithACorrectedEndTime_RecordsTheDropAtThatMoment()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync(quantityKilograms: 1.0);
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(Start.AddSeconds(3600));
        DateTimeOffset endedAt = Start.AddSeconds(672);

        TransitionResult result = await relaunched.Session.RecoverAsync(
            RecoveryDecision.EndedAt(endedAt));

        result.Success.Should().BeTrue();
        result.Snapshot.ActiveRoast.Should().BeNull();
        result.Snapshot.RequiresRecovery.Should().BeFalse();

        RoastData roast = relaunched.Current.RoastLogs.Single();
        roast.DroppedAtUtc.Should().Be(endedAt);
        roast.TotalSeconds.Should().Be(672);
        roast.CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
        relaunched.RemainingQuantityOf(bean.Id).Should().Be(0.76);
    }

    [Fact]
    public async Task RecoverAsync_WithAnEndTimeBeforeTheRoastStarted_IsRefused()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(Start.AddSeconds(900));

        TransitionResult result = await relaunched.Session.RecoverAsync(
            RecoveryDecision.EndedAt(Start.AddSeconds(-10)));

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.CorrectedElapsedRequired);
        relaunched.Current.RoastLogs.Should().BeEmpty();
        relaunched.Current.ActiveRoastSession!.ActiveRoast.Should().NotBeNull();
        // A refused answer must leave recovery outstanding, or the roast becomes unreachable.
        result.Snapshot.RequiresRecovery.Should().BeTrue();
        (await relaunched.Session.GetSnapshotAsync()).RequiresRecovery.Should().BeTrue();
    }

    [Fact]
    public async Task RecoverAsync_WithRollbackTimeline_UsesExplicitElapsedForTheRoastDuration()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        DateTimeOffset rolledBackNow = Start.AddMinutes(-30);
        DateTimeOffset correctedEnd = rolledBackNow.AddSeconds(-15);
        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(rolledBackNow);

        TransitionResult result = await relaunched.Session.RecoverAsync(
            RecoveryDecision.EndedAt(correctedEnd, 515));

        result.Success.Should().BeTrue();
        RoastData roast = relaunched.Current.RoastLogs.Single();
        roast.DroppedAtUtc.Should().Be(correctedEnd);
        roast.TotalSeconds.Should().Be(515);
    }

    [Fact]
    public async Task RecoverAsync_WhenAccumulatedElapsedExtendsPastCorrectedEnd_IsRefused()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        original.Clock.AdvanceSeconds(300);
        (await original.Session.PauseAsync()).Success.Should().BeTrue();

        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(Start.AddSeconds(900));

        TransitionResult result = await relaunched.Session.RecoverAsync(
            RecoveryDecision.EndedAt(Start.AddSeconds(200)));

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.InvalidDropTime);
        result.Snapshot.RequiresRecovery.Should().BeTrue();
        relaunched.Current.RoastLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task RecoverAsync_WhenTheCorrectedDropCannotBeSaved_LeavesRecoveryOutstanding()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        (await original.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();

        // Relaunch onto a data service whose next write — the drop commit — fails.
        using var relaunched = await RoastSessionTestHarness.ReopenAsync(
            original.CanonicalPath,
            Start.AddSeconds(900),
            (data, _) => data.RoastLogs.Count == 1
                ? throw new IOException("Injected recovery drop failure.")
                : Task.CompletedTask);

        TransitionResult result = await relaunched.Session.RecoverAsync(
            RecoveryDecision.EndedAt(Start.AddSeconds(700)));

        result.Success.Should().BeFalse();
        result.Snapshot.RequiresRecovery.Should().BeTrue();
        (await relaunched.Session.GetSnapshotAsync()).RequiresRecovery.Should().BeTrue();
        relaunched.Current.ActiveRoastSession!.ActiveRoast.Should().NotBeNull();
    }

    [Fact]
    public async Task RecoverAsync_UsesThePostPersistenceCoordinatorWithBatchIdentity()
    {
        Mock<ICoolingNotificationWorkflow> workflow = new();
        workflow.Setup(service => service.HandleSuccessfulDropAsync(
                It.IsAny<RoastData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(
            Start,
            notificationWorkflow: workflow.Object);
        BeanData bean = await harness.AddBeanAsync();
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        Guid draftId = harness.Current.ActiveRoastSession!.ActiveRoast!.Id;
        harness.Clock.AdvanceSeconds(900);

        TransitionResult result = await harness.Session.RecoverAsync(
            RecoveryDecision.EndedAt(Start.AddSeconds(700)));

        result.Success.Should().BeTrue();
        workflow.Verify(service => service.HandleSuccessfulDropAsync(
                It.Is<RoastData>(roast =>
                    roast.Id == draftId &&
                    roast.BatchNumber == 1 &&
                    roast.CompletionStatus == RoastCompletionStatus.AwaitingWeight),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_PublishesASnapshotEventThatAgreesWithTheCommandResult()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();
        var published = new List<RoastSessionSnapshot>();
        harness.Session.SnapshotChanged += (_, snapshot) => published.Add(snapshot);

        TransitionResult result = await harness.Session.StartAsync(
            new RoastSetup(bean.Id, 218, 240));

        result.Snapshot.RequiresRecovery.Should().BeFalse();
        published.Should().ContainSingle();
        published[0].ActiveRoast!.Id.Should().Be(result.Snapshot.ActiveRoast!.Id);
        published[0].RequiresRecovery.Should().BeFalse();
    }

    [Fact]
    public async Task ADifferentDraftArrivingAfterAnAcknowledgement_StillRequiresRecovery()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync();
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        harness.Clock.AdvanceSeconds(600);
        (await harness.Session.DropAsync()).Success.Should().BeTrue();

        // A restore replaces the session without restarting the process. The draft it carries
        // was never confirmed by this user, so it must not inherit the acknowledgement.
        var restoredSessionId = Guid.NewGuid();
        (await harness.AppDataService.UpdateAsync(data => data.ActiveRoastSession =
            new RoastSessionData
            {
                Id = restoredSessionId,
                StartedAtUtc = Start,
                NextBatchNumber = 1,
                ActiveRoast = new ActiveRoastDraft
                {
                    Id = Guid.NewGuid(),
                    SessionId = restoredSessionId,
                    BatchNumber = 1,
                    BeanId = bean.Id,
                    BeanDisplaySnapshot = bean.DisplayName,
                    Temperature = 210,
                    BatchWeight = 200,
                    Phase = ActiveRoastPhase.Roasting,
                    StartedAtUtc = Start,
                    RunningSinceUtc = Start
                }
            })).Should().BeTrue();

        (await harness.Session.GetSnapshotAsync()).RequiresRecovery.Should().BeTrue();
    }

    [Fact]
    public async Task ColdLaunchAfterADrop_RestoresCoolingFromTheStoredTimestamps()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await original.AddBeanAsync();
        Guid roastId = await DropOneBatchAsync(original, bean);

        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(original.Clock.UtcNow.AddSeconds(200));
        RoastSessionSnapshot snapshot = await relaunched.Session.GetSnapshotAsync();

        snapshot.RequiresRecovery.Should().BeFalse();
        RoastWorkItem item = snapshot.OpenWork.Single();
        item.RoastId.Should().Be(roastId);
        item.Status.Should().Be(RoastEffectiveStatus.Cooling);
        item.RemainingCoolingSeconds.Should().Be(100);
    }

    [Fact]
    public async Task CompleteCoolingAsync_MovesOnlyThatBatchToNeedsWeightWithNoFinalWeight()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        BeanData bean = await harness.AddBeanAsync(quantityKilograms: 2.0);
        // A cooling window long enough that both batches are still counting down when the
        // first one is released, which is the case the action exists for.
        harness.Preferences.CoolingDurationSeconds = 3600;
        Guid first = await DropOneBatchAsync(harness, bean);
        harness.Clock.AdvanceSeconds(60);
        Guid second = await DropOneBatchAsync(harness, bean);

        TransitionResult result = await harness.Session.CompleteCoolingAsync(first);

        result.Success.Should().BeTrue();
        IReadOnlyList<RoastWorkItem> work = result.Snapshot.OpenWork;
        work.Single(item => item.RoastId == first).Status
            .Should().Be(RoastEffectiveStatus.NeedsWeight);
        work.Single(item => item.RoastId == first).RemainingCoolingSeconds.Should().Be(0);
        // The second batch keeps its own countdown; readiness is per batch, never per session.
        work.Single(item => item.RoastId == second).Status
            .Should().Be(RoastEffectiveStatus.Cooling);
        work.Single(item => item.RoastId == second).RemainingCoolingSeconds.Should().Be(3600);

        RoastData released = harness.Current.RoastLogs.Single(roast => roast.Id == first);
        released.CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
        released.FinalWeight.Should().BeNull();
        released.HasFinalWeight.Should().BeFalse();
        harness.Notifications.Cancelled.Should().ContainSingle().Which.Should().Be(first);
    }

    [Fact]
    public async Task CompleteCoolingAsync_SurvivesAColdLaunch()
    {
        using RoastSessionTestHarness original = await RoastSessionTestHarness.CreateAsync(Start);
        Guid roastId = await DropOneBatchAsync(original);
        (await original.Session.CompleteCoolingAsync(roastId)).Success.Should().BeTrue();

        using RoastSessionTestHarness relaunched =
            await original.RelaunchAsync(original.Clock.UtcNow.AddSeconds(5));
        RoastWorkItem item = (await relaunched.Session.GetSnapshotAsync()).OpenWork.Single();

        item.RoastId.Should().Be(roastId);
        item.Status.Should().Be(RoastEffectiveStatus.NeedsWeight);
        item.IsReadyToWeigh.Should().BeTrue();
        relaunched.Current.RoastLogs.Single().FinalWeight.Should().BeNull();
    }

    [Fact]
    public async Task CompleteCoolingAsync_OnABatchThatIsAlreadyReady_IsANoOp()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        Guid roastId = await DropOneBatchAsync(harness);
        harness.Clock.AdvanceSeconds(RoastPreferenceDefaults.CoolingDurationSeconds);
        int storedDuration = harness.Current.RoastLogs.Single().CoolingDurationSeconds!.Value;

        TransitionResult result = await harness.Session.CompleteCoolingAsync(roastId);

        result.Success.Should().BeTrue();
        result.Snapshot.OpenWork.Single().Status.Should().Be(RoastEffectiveStatus.NeedsWeight);
        harness.Current.RoastLogs.Single().CoolingDurationSeconds.Should().Be(storedDuration);
    }

    [Fact]
    public async Task CompleteCoolingAsync_OnAResolvedBatch_IsRejected()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        Guid roastId = await DropOneBatchAsync(harness);
        (await harness.Session.MarkUnweighedAsync(roastId)).Success.Should().BeTrue();

        TransitionResult result = await harness.Session.CompleteCoolingAsync(roastId);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.RoastAlreadyResolved);
        harness.Current.RoastLogs.Single().CompletionStatus
            .Should().Be(RoastCompletionStatus.Unweighed);
    }

    [Fact]
    public async Task CompleteCoolingAsync_OnAnUnknownRoast_IsRejected()
    {
        using RoastSessionTestHarness harness = await RoastSessionTestHarness.CreateAsync(Start);
        await DropOneBatchAsync(harness);

        TransitionResult result = await harness.Session.CompleteCoolingAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Be(RoastTransitionError.RoastNotFound);
        harness.Notifications.Cancelled.Should().BeEmpty();
    }

    private static async Task<Guid> DropOneBatchAsync(
        RoastSessionTestHarness harness,
        BeanData? existingBean = null)
    {
        BeanData bean = existingBean ?? await harness.AddBeanAsync();
        (await harness.Session.StartAsync(new RoastSetup(bean.Id, 218, 240))).Success
            .Should().BeTrue();
        harness.Clock.AdvanceSeconds(660);
        TransitionResult dropped = await harness.Session.DropAsync();
        dropped.Success.Should().BeTrue();
        return harness.Current.RoastLogs.Last().Id;
    }
}
