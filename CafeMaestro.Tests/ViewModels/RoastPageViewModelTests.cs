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

    private sealed class Harness
    {
        private RoastSessionSnapshot _snapshot;
        public BeanData Bean { get; } = new()
        {
            Id = Guid.NewGuid(), Country = "Ethiopia", CoffeeName = "Guji", Variety = "Heirloom",
            Quantity = 1, RemainingQuantity = 1
        };

        public Mock<IDisplayWakeService> Wake { get; } = new();
        public RoastPageViewModel ViewModel { get; }

        private readonly Mock<IRoastSessionService> _session = new();
        private readonly Mock<IRoastQueryService> _query = new();

        public Harness()
        {
            _snapshot = HandoffSnapshot(nextBatch: 1);
            _session.Setup(service => service.GetSnapshotAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _snapshot);
            _session.Setup(service => service.StartAsync(It.IsAny<RoastSetup>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => TransitionResult.Ok(_snapshot));
            _session.Setup(service => service.PauseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => TransitionResult.Ok(_snapshot));
            _session.Setup(service => service.ResumeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => TransitionResult.Ok(_snapshot));

            Mock<IBeanDataService> beans = new();
            beans.Setup(service => service.GetSortedAvailableBeansAsync()).ReturnsAsync([Bean]);
            _query.Setup(service => service.GetSetupSuggestionAsync(Bean.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RoastSetupSuggestion
                {
                    BeanId = Bean.Id, Temperature = 218, BatchWeight = 240,
                    LastCompletedRoast = CompletedRoast(), NewerAwaitingWeightCount = 0
                });
            _query.Setup(service => service.GetRoastsForBeanAsync(Bean.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([CompletedRoast(), DroppedRoast()]);

            ViewModel = new RoastPageViewModel(
                _session.Object,
                _query.Object,
                beans.Object,
                Mock.Of<IOverlayService>(),
                Wake.Object,
                Mock.Of<IRoastRecoveryAdapter>(),
                new FixedClock());
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

        public RoastSessionSnapshot HandoffSnapshot(int nextBatch, params RoastWorkItem[] work) => new()
        {
            AsOfUtc = FixedClock.Now,
            SessionId = nextBatch > 1 ? Guid.NewGuid() : null,
            NextBatchNumber = nextBatch,
            RequiresRecovery = false,
            OpenWork = work,
            ActiveRoast = null
        };

        public RoastWorkItem Work(int batch, double remaining) => new()
        {
            RoastId = Guid.NewGuid(), SessionId = Guid.NewGuid(), BatchNumber = batch,
            BeanId = Bean.Id, BeanDisplaySnapshot = Bean.DisplayName, BatchWeight = 240,
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
        public DateTimeOffset UtcNow => Now;
    }
}
