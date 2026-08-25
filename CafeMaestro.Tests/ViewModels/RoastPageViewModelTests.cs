using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class RoastPageViewModelTests
{
    [Fact]
    public async Task OnAppearing_WithNoActiveDraft_ShowsSimpleSetup()
    {
        Harness harness = new();

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.PresentationState.Should().Be(RoastPresentationState.Setup);
        harness.ViewModel.IsFirstCrackVisible.Should().BeFalse();
        harness.ViewModel.AvailableBeans.Should().ContainSingle();
    }

    [Fact]
    public async Task BeanIdQuery_SelectsStableDepletedBeanFromFullStore()
    {
        Harness harness = new();
        harness.Bean.RemainingQuantity = 0;
        harness.Beans.Setup(service => service.GetSortedAvailableBeansAsync()).ReturnsAsync([]);
        harness.ViewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            ["BeanId"] = harness.Bean.Id.ToString()
        });

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.SelectedBean.Should().BeSameAs(harness.Bean);
        harness.ViewModel.BatchWeightText.Should().Be("240");
        harness.ViewModel.InventoryWarning.Should().Contain("you can still start");
    }

    [Fact]
    public async Task Start_ThenPause_ProjectsPersistedSnapshotWithoutOwningTimerTruth()
    {
        Harness harness = new();
        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.SelectedBean = harness.Bean;
        await harness.ViewModel.SelectBeanAsync(harness.Bean);
        harness.ViewModel.TemperatureText = "218";
        harness.ViewModel.BatchWeightText = "240";

        await harness.ViewModel.StartAsync();
        harness.SetSnapshot(harness.ActiveSnapshot(ActiveRoastPhase.Roasting, elapsed: 71));
        await harness.ViewModel.RefreshAsync();
        harness.ViewModel.PresentationState.Should().Be(RoastPresentationState.Active);
        harness.ViewModel.ElapsedDisplay.Should().Be("01:11");

        harness.SetSnapshot(harness.ActiveSnapshot(ActiveRoastPhase.Paused, elapsed: 71));
        await harness.ViewModel.PauseOrResumeAsync();

        harness.ViewModel.IsPaused.Should().BeTrue();
        harness.Wake.Verify(service => service.SetKeepScreenOnAsync(false), Times.AtLeastOnce);
    }

    [Fact]
    public async Task FirstDrop_PrioritizesCopiedBatchTwoSetup()
    {
        Harness harness = new();
        RoastWorkItem batchOne = harness.Work(batch: 1, remaining: 240);
        harness.SetSnapshot(harness.HandoffSnapshot(nextBatch: 2, batchOne));

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.PresentationState.Should().Be(RoastPresentationState.Handoff);
        harness.ViewModel.PrimaryActionText.Should().Be("SET UP BATCH 2");
        await harness.ViewModel.PrimaryHandoffActionAsync();
        harness.ViewModel.PresentationState.Should().Be(RoastPresentationState.Setup);
        harness.ViewModel.BatchWeightText.Should().Be("240");
        harness.ViewModel.TemperatureText.Should().Be("218");
    }

    [Fact]
    public async Task SecondDrop_WhenBatchOneReady_PrioritizesWeighIn()
    {
        Harness harness = new();
        RoastWorkItem batchOne = harness.Work(batch: 1, remaining: 0);
        RoastWorkItem batchTwo = harness.Work(batch: 2, remaining: 300);
        harness.SetSnapshot(harness.HandoffSnapshot(nextBatch: 3, batchOne, batchTwo));

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.PrimaryActionText.Should().Be("WEIGH BATCH 1");
        harness.ViewModel.Channels.First().IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task ImplausiblePersistedDraft_UsesIsolatedRecoveryPresentation()
    {
        Harness harness = new();
        RoastSessionSnapshot snapshot = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, elapsed: 0) with
        {
            RequiresRecovery = true,
            ActiveRoast = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, elapsed: 0).ActiveRoast! with
            {
                IsElapsedImplausible = true,
                RequiresCorrectedElapsed = true
            }
        };
        harness.SetSnapshot(snapshot);

        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.PresentationState.Should().Be(RoastPresentationState.Recovery);
        harness.ViewModel.RecoveryRequiresCorrectedTime.Should().BeTrue();
    }

    [Fact]
    public async Task ChannelThatBecameReadySinceSnapshot_OpensWeighIn()
    {
        Harness harness = new();
        RoastWorkItem item = harness.Work(batch: 1, remaining: 0) with
        {
            Status = RoastEffectiveStatus.Cooling
        };
        harness.SetSnapshot(harness.HandoffSnapshot(nextBatch: 2, item));
        await harness.ViewModel.OnAppearingAsync();

        await harness.ViewModel.WeighChannelAsync(harness.ViewModel.Channels.Single());

        harness.Overlay.Verify(service => service.ShowWeighInAsync(
            It.Is<WeighInRequest>(request => request.RoastId == item.RoastId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tick_AdvancesDevelopmentTimeAfterFirstCrack()
    {
        Harness harness = new();
        RoastSessionSnapshot snapshot = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, elapsed: 120) with
        {
            ActiveRoast = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, elapsed: 120).ActiveRoast! with
            {
                FirstCrackElapsedSeconds = 60
            }
        };
        harness.SetSnapshot(snapshot);
        await harness.ViewModel.OnAppearingAsync();

        harness.Clock.Advance(TimeSpan.FromSeconds(10));
        harness.ViewModel.Tick();

        harness.ViewModel.DevelopmentDisplay.Should().Be("01:10");
        harness.ViewModel.DtrDisplay.Should().Be("53.8%");
    }

    [Fact]
    public async Task BatchTwoSetup_IgnoresOlderSessionsOpenWork()
    {
        Harness harness = new();
        Guid sessionId = Guid.NewGuid();
        RoastWorkItem older = harness.Work(batch: 1, remaining: 0) with
        {
            SessionId = Guid.NewGuid(), BeanId = Guid.NewGuid(), BatchWeight = 999
        };
        RoastWorkItem current = harness.Work(batch: 1, remaining: 200) with { SessionId = sessionId };
        harness.SetSnapshot(new RoastSessionSnapshot
        {
            AsOfUtc = FixedClock.Now,
            SessionId = sessionId,
            NextBatchNumber = 2,
            RequiresRecovery = false,
            OpenWork = [older, current]
        });
        await harness.ViewModel.OnAppearingAsync();

        await harness.ViewModel.PrimaryHandoffActionAsync();

        harness.ViewModel.BatchWeightText.Should().Be("240");
    }

    [Fact]
    public async Task FailedDrop_RetryReusesCapturedTimestampAndElapsed()
    {
        Harness harness = new();
        RoastSessionSnapshot active = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, elapsed: 90);
        RoastSessionSnapshot dropped = harness.HandoffSnapshot(nextBatch: 2, harness.Work(1, 300));
        harness.SetSnapshot(active);
        var proposals = new List<DropProposal>();
        harness.Session.Setup(service => service.DropAsync(
                It.IsAny<DropProposal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DropProposal proposal, CancellationToken _) =>
            {
                proposals.Add(proposal);
                return proposals.Count == 1
                    ? TransitionResult.Fail(RoastTransitionError.PersistenceFailed, "write failed", active)
                    : TransitionResult.Ok(dropped);
            });
        await harness.ViewModel.OnAppearingAsync();

        await harness.ViewModel.DropAsync();
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        await harness.ViewModel.RetryAsync();

        proposals.Should().HaveCount(2);
        proposals[1].Should().Be(proposals[0]);
        proposals[0].ElapsedSeconds.Should().Be(90);
    }

    [Fact]
    public async Task ActiveBatchTwo_ShowsOnlyCurrentSessionChannelsAndAllowsReadyWeighIn()
    {
        Harness harness = new();
        Guid sessionId = Guid.NewGuid();
        RoastWorkItem current = harness.Work(1, 0) with { SessionId = sessionId };
        RoastWorkItem older = harness.Work(9, 0) with { SessionId = Guid.NewGuid() };
        RoastSessionSnapshot snapshot = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, 20) with
        {
            SessionId = sessionId,
            ActiveRoast = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, 20).ActiveRoast! with
            {
                SessionId = sessionId,
                BatchNumber = 2
            },
            OpenWork = [older, current]
        };
        harness.SetSnapshot(snapshot);
        await harness.ViewModel.OnAppearingAsync();

        harness.ViewModel.Channels.Should().ContainSingle(channel => channel.RoastId == current.RoastId);
        await harness.ViewModel.WeighChannelAsync(harness.ViewModel.Channels.Single());
        harness.Overlay.Verify(service => service.ShowWeighInAsync(
            It.Is<WeighInRequest>(request => request.RoastId == current.RoastId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandoffTick_ReprojectsPrimaryActionWhenCoolingExpires()
    {
        Harness harness = new();
        RoastWorkItem cooling = harness.Work(1, 1) with { Status = RoastEffectiveStatus.Cooling };
        harness.SetSnapshot(harness.HandoffSnapshot(3, cooling));
        await harness.ViewModel.OnAppearingAsync();
        harness.ViewModel.PrimaryActionText.Should().Be("FINISH SESSION");

        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        harness.ViewModel.Tick();

        harness.ViewModel.PrimaryActionText.Should().Be("WEIGH BATCH 1");
    }

    [Fact]
    public async Task BatchTwoSetup_ResolvesDepletedBeanFromFullStoreAndKeepsStartNonBlocking()
    {
        Harness harness = new();
        harness.Bean.RemainingQuantity = 0;
        harness.Beans.Setup(service => service.GetSortedAvailableBeansAsync()).ReturnsAsync([]);
        harness.SetSnapshot(harness.HandoffSnapshot(2, harness.Work(1, 200)));
        await harness.ViewModel.OnAppearingAsync();

        await harness.ViewModel.PrimaryHandoffActionAsync();

        harness.ViewModel.SelectedBean.Should().BeSameAs(harness.Bean);
        harness.ViewModel.AvailableBeans.Should().Contain(harness.Bean);
        harness.ViewModel.InventoryWarning.Should().Contain("you can still start");
        harness.ViewModel.CanStart.Should().BeTrue();
    }

    [Fact]
    public async Task RecoveryBack_OffersConfirmedDiscardAndStaysWhenCancelled()
    {
        Harness harness = new();
        RoastSessionSnapshot recovery = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, 45) with
        {
            RequiresRecovery = true
        };
        RoastSessionSnapshot setup = harness.HandoffSnapshot(1);
        harness.SetSnapshot(recovery);
        harness.Overlay.SetupSequence(service => service.ShowDiscardAsync(
                It.IsAny<DiscardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DiscardOutcome.Cancelled)
            .ReturnsAsync(new DiscardOutcome(DiscardOutcomeKind.Discard));
        harness.Session.Setup(service => service.DiscardAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Ok(setup));
        await harness.ViewModel.OnAppearingAsync();

        (await harness.ViewModel.HandleBackNavigationAsync()).Should().BeTrue();
        harness.ViewModel.PresentationState.Should().Be(RoastPresentationState.Recovery);
        await harness.ViewModel.DiscardRecoveryAsync();
        harness.ViewModel.PresentationState.Should().Be(RoastPresentationState.Setup);
    }

    [Fact]
    public async Task PersistenceError_GuardsBackAndDataSettingsEndsRetainedRetry()
    {
        Harness harness = new();
        RoastSessionSnapshot active = harness.ActiveSnapshot(ActiveRoastPhase.Roasting, 30);
        harness.SetSnapshot(active);
        harness.Session.Setup(service => service.DropAsync(
                It.IsAny<DropProposal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransitionResult.Fail(RoastTransitionError.PersistenceFailed, "write failed", active));
        await harness.ViewModel.OnAppearingAsync();
        await harness.ViewModel.DropAsync();

        (await harness.ViewModel.HandleBackNavigationAsync()).Should().BeTrue();
        await harness.ViewModel.OnWindowResumedAsync();
        await harness.ViewModel.RetryAsync();
        await harness.ViewModel.OpenDataSettingsAsync();
        await harness.ViewModel.RetryAsync();

        harness.Navigation.Verify(service => service.GoToAsync(CafeMaestro.Navigation.Routes.Settings), Times.Once);
        harness.Session.Verify(service => service.DropAsync(
            It.IsAny<DropProposal>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task WindowLifecycle_ReleasesWakeThenRefreshesAndRestoresIt()
    {
        Harness harness = new();
        harness.SetSnapshot(harness.ActiveSnapshot(ActiveRoastPhase.Roasting, 62));
        await harness.ViewModel.OnAppearingAsync();

        await harness.ViewModel.OnWindowStoppedAsync();
        await harness.ViewModel.OnWindowResumedAsync();

        harness.Wake.Verify(service => service.SetKeepScreenOnAsync(false), Times.Once);
        harness.Wake.Verify(service => service.SetKeepScreenOnAsync(true), Times.Exactly(2));
        harness.ViewModel.ActiveTimerSemanticDescription.Should().Be("Roasting, 1 minute 2 seconds");
    }

    private sealed class Harness
    {
        private RoastSessionSnapshot _snapshot;
        public BeanData Bean { get; } = new()
        {
            Id = Guid.NewGuid(), Country = "Ethiopia", CoffeeName = "Guji", Variety = "Heirloom",
            Quantity = 1, RemainingQuantity = 1
        };

        public Mock<IDisplayWakeService> Wake { get; } = new();
        public Mock<IOverlayService> Overlay { get; } = new();
        public Mock<INavigationService> Navigation { get; } = new();
        public FixedClock Clock { get; } = new();
        public RoastPageViewModel ViewModel { get; }

        public Mock<IRoastSessionService> Session { get; } = new();
        public Mock<IBeanDataService> Beans { get; } = new();
        private readonly Mock<IRoastQueryService> _query = new();

        public Harness()
        {
            _snapshot = HandoffSnapshot(nextBatch: 1);
            Session.Setup(service => service.GetSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _snapshot);
            Session.Setup(service => service.StartAsync(It.IsAny<RoastSetup>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => TransitionResult.Ok(_snapshot));
            Session.Setup(service => service.PauseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => TransitionResult.Ok(_snapshot));
            Session.Setup(service => service.ResumeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => TransitionResult.Ok(_snapshot));

            Beans.Setup(service => service.GetSortedAvailableBeansAsync()).ReturnsAsync([Bean]);
            Beans.Setup(service => service.GetBeanByIdAsync(Bean.Id)).ReturnsAsync(Bean);
            _query.Setup(service => service.GetSetupSuggestionAsync(Bean.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RoastSetupSuggestion
                {
                    BeanId = Bean.Id, Temperature = 218, BatchWeight = 240,
                    LastCompletedRoast = CompletedRoast(), NewerAwaitingWeightCount = 0
                });
            _query.Setup(service => service.GetRoastsForBeanAsync(Bean.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([CompletedRoast(), DroppedRoast()]);
            Overlay.Setup(service => service.ShowWeighInAsync(
                    It.IsAny<WeighInRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WeighInOutcome.Cancelled);

            ViewModel = new RoastPageViewModel(
                Session.Object,
                _query.Object,
                Beans.Object,
                Overlay.Object,
                Wake.Object,
                Mock.Of<IRoastRecoveryAdapter>(),
                Navigation.Object,
                Mock.Of<IAlertService>(),
                Clock);
        }

        public void SetSnapshot(RoastSessionSnapshot snapshot) => _snapshot = snapshot;

        public RoastSessionSnapshot ActiveSnapshot(ActiveRoastPhase phase, double elapsed) => new()
        {
            AsOfUtc = FixedClock.Now,
            SessionId = Guid.NewGuid(),
            NextBatchNumber = 2,
            RequiresRecovery = false,
            OpenWork = [],
            ActiveRoast = new ActiveRoastSnapshot
            {
                Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), BatchNumber = 1,
                BeanId = Bean.Id, BeanDisplaySnapshot = Bean.DisplayName,
                Temperature = 218, BatchWeight = 240, Phase = phase,
                StartedAtUtc = FixedClock.Now.AddSeconds(-elapsed), ElapsedSeconds = elapsed,
                FirstCrackEnabled = false, CoolingDurationSeconds = 300,
                IsElapsedImplausible = false,
                RequiresCorrectedElapsed = false
            }
        };

        public RoastSessionSnapshot HandoffSnapshot(int nextBatch, params RoastWorkItem[] work)
        {
            Guid? sessionId = nextBatch > 1 ? Guid.NewGuid() : null;
            return new RoastSessionSnapshot
            {
                AsOfUtc = FixedClock.Now,
                SessionId = sessionId,
                NextBatchNumber = nextBatch,
                RequiresRecovery = false,
                OpenWork = work.Select(item => item with { SessionId = sessionId }).ToList(),
                ActiveRoast = null
            };
        }

        public RoastWorkItem Work(int batch, double remaining) => new()
        {
            RoastId = Guid.NewGuid(), SessionId = Guid.NewGuid(), BatchNumber = batch,
            BeanId = Bean.Id, BeanDisplaySnapshot = Bean.DisplayName, Temperature = 218, BatchWeight = 240,
            DroppedAtUtc = FixedClock.Now.AddSeconds(-(300 - remaining)),
            ReadyToWeighAtUtc = FixedClock.Now.AddSeconds(remaining),
            RemainingCoolingSeconds = remaining,
            Status = remaining <= 0 ? RoastEffectiveStatus.NeedsWeight : RoastEffectiveStatus.Cooling,
            TotalSeconds = 665
        };

        private RoastData CompletedRoast() => new()
        {
            Id = Guid.NewGuid(), BeanId = Bean.Id, BeanType = Bean.DisplayName,
            BeanDisplaySnapshot = Bean.DisplayName, Temperature = 218, BatchWeight = 240,
            FinalWeight = 206, RoastMinutes = 11, RoastSeconds = 5, RoastDate = FixedClock.Now.UtcDateTime,
            DroppedAtUtc = FixedClock.Now.AddDays(-1), CompletionStatus = RoastCompletionStatus.Complete,
            RoastLevelName = "Medium"
        };

        private RoastData DroppedRoast() => new()
        {
            Id = Guid.NewGuid(), BeanId = Bean.Id, BeanType = Bean.DisplayName,
            BeanDisplaySnapshot = Bean.DisplayName, Temperature = 218, BatchWeight = 240,
            RoastMinutes = 11, RoastSeconds = 5, RoastDate = FixedClock.Now.UtcDateTime,
            DroppedAtUtc = FixedClock.Now, CompletionStatus = RoastCompletionStatus.AwaitingWeight,
            CoolingDurationSeconds = 300, BatchNumber = 1, SessionId = Guid.NewGuid()
        };
    }

    private sealed class FixedClock : IClock
    {
        public static readonly DateTimeOffset Now = new(2026, 8, 25, 8, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow { get; private set; } = Now;

        public void Advance(TimeSpan duration) => UtcNow += duration;
    }
}
