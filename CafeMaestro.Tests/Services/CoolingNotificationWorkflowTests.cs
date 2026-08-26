using CafeMaestro.Models;
using CafeMaestro.Navigation;
using CafeMaestro.Services;
using FluentAssertions;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class CoolingNotificationWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReconcileAsync_WhenEnabled_ReplacesScheduleWithAwaitingRoastsOnly()
    {
        RoastData awaiting = Roast(RoastCompletionStatus.AwaitingWeight, Now.AddMinutes(5));
        RoastData completed = Roast(RoastCompletionStatus.Complete, Now.AddMinutes(3));
        AppData data = new() { RoastLogs = [awaiting, completed] };
        Mock<IAppDataService> appData = new();
        appData.SetupGet(service => service.CurrentData).Returns(data);
        Mock<IRoastPreferencesService> preferences = new();
        preferences.Setup(service => service.GetCoolingNotificationsEnabledAsync()).ReturnsAsync(true);
        Mock<ICoolingNotificationService> notifications = new();
        notifications.Setup(service => service.GetPermissionStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CoolingNotificationPermissionState.Granted);
        CoolingNotificationWorkflow workflow = new(
            appData.Object,
            preferences.Object,
            notifications.Object,
            Mock.Of<IAlertService>(),
            new MemoryPreferences());

        await workflow.ReconcileAsync();

        notifications.Verify(service => service.CancelAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(service => service.ScheduleCoolingReadyAsync(
            awaiting.Id,
            awaiting.ReadyToWeighAtUtc!.Value,
            awaiting.BeanDisplaySnapshot,
            awaiting.BatchNumber,
            It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(service => service.ScheduleCoolingReadyAsync(
            completed.Id,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AfterSuccessfulDrop_FirstTimeExplainsThenRequestsAndSchedules()
    {
        Mock<IRoastPreferencesService> preferences = new();
        preferences.Setup(service => service.GetCoolingNotificationsEnabledAsync()).ReturnsAsync(false);
        preferences.Setup(service => service.SetCoolingNotificationsEnabledAsync(true)).ReturnsAsync(true);
        Mock<ICoolingNotificationService> notifications = new();
        notifications.Setup(service => service.GetPermissionStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CoolingNotificationPermissionState.NotDetermined);
        notifications.Setup(service => service.RequestPermissionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CoolingNotificationPermissionState.Granted);
        Mock<IAlertService> alerts = new();
        alerts.Setup(service => service.ShowConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        CoolingNotificationWorkflow workflow = new(
            Mock.Of<IAppDataService>(), preferences.Object, notifications.Object,
            alerts.Object, new MemoryPreferences());
        RoastData dropped = Roast(RoastCompletionStatus.AwaitingWeight, Now.AddMinutes(5));

        (await workflow.HandleSuccessfulDropAsync(dropped)).Should().BeNull();
        (await workflow.HandleSuccessfulDropAsync(dropped)).Should().BeNull();

        alerts.Verify(service => service.ShowConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        notifications.Verify(service => service.RequestPermissionAsync(It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(service => service.ScheduleCoolingReadyAsync(
            dropped.Id, dropped.ReadyToWeighAtUtc!.Value, dropped.BeanDisplaySnapshot,
            dropped.BatchNumber,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivationHandler_RevalidatesReadyRoastBeforeOpeningWeighIn()
    {
        RoastData roast = Roast(RoastCompletionStatus.AwaitingWeight, Now.AddMinutes(-1));
        Mock<IAppDataService> appData = new();
        appData.SetupGet(service => service.CurrentData)
            .Returns(new AppData { RoastLogs = [roast] });
        Mock<INavigationService> navigation = new();
        Mock<IOverlayService> overlay = new();
        CoolingNotificationActivationHandler handler = new(
            appData.Object, navigation.Object, overlay.Object, new FixedClock(Now));

        await handler.HandleAsync(Payload(roast.Id));

        navigation.Verify(service => service.GoToAsync(Routes.RoastLog), Times.Once);
        overlay.Verify(service => service.ShowWeighInAsync(
            It.Is<WeighInRequest>(request => request.RoastId == roast.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(RoastCompletionStatus.Complete)]
    [InlineData(RoastCompletionStatus.Unweighed)]
    public async Task ActivationHandler_ResolvedRoastOpensDetailWithoutStaleWeighIn(
        RoastCompletionStatus status)
    {
        RoastData roast = Roast(status, Now.AddMinutes(-1));
        Mock<IAppDataService> appData = new();
        appData.SetupGet(service => service.CurrentData)
            .Returns(new AppData { RoastLogs = [roast] });
        Mock<INavigationService> navigation = new();
        Mock<IOverlayService> overlay = new();
        CoolingNotificationActivationHandler handler = new(
            appData.Object, navigation.Object, overlay.Object, new FixedClock(Now));

        await handler.HandleAsync(Payload(roast.Id));

        navigation.Verify(service => service.GoToAsync(
            Routes.RoastDetail,
            It.Is<IDictionary<string, object>>(values => values["RoastId"].ToString() == roast.Id.ToString())),
            Times.Once);
        overlay.Verify(service => service.ShowWeighInAsync(
            It.IsAny<WeighInRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AppActivationPayload Payload(Guid roastId) =>
        new("cooling-ready", new Dictionary<string, string> { ["roastId"] = roastId.ToString() });

    private static RoastData Roast(RoastCompletionStatus status, DateTimeOffset readyAt) => new()
    {
        Id = Guid.NewGuid(), BeanType = "Guji", BeanDisplaySnapshot = "Guji",
        Temperature = 218, BatchWeight = 240,
        FinalWeight = status == RoastCompletionStatus.Complete ? 205 : null,
        RoastMinutes = 10, RoastSeconds = 0, RoastDate = readyAt.UtcDateTime,
        DroppedAtUtc = readyAt.AddMinutes(-5), CoolingDurationSeconds = 300,
        CompletionStatus = status, SessionId = Guid.NewGuid(), BatchNumber = 1
    };

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class MemoryPreferences : IPreferences
    {
        private readonly Dictionary<string, object?> _values = [];

        public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);

        public void Remove(string key, string? sharedName = null) => _values.Remove(key);

        public void Clear(string? sharedName = null) => _values.Clear();

        public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value;

        public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
            _values.TryGetValue(key, out object? value) && value is T typed ? typed : defaultValue;
    }
}
