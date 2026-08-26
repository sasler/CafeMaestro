using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public sealed class RoastingSettingsPageViewModelTests
{
    [Fact]
    public async Task OnAppearing_WithNothingStored_ShowsFiveMinuteCoolingFirstCrackOffAndTenthGram()
    {
        var preferences = new Mock<IRoastPreferencesService>();
        preferences
            .Setup(service => service.GetCoolingDurationSecondsAsync())
            .ReturnsAsync(RoastPreferenceDefaults.CoolingDurationSeconds);
        preferences
            .Setup(service => service.GetFirstCrackEnabledAsync())
            .ReturnsAsync(RoastPreferenceDefaults.FirstCrackEnabled);
        preferences
            .Setup(service => service.GetCoolingNotificationsEnabledAsync())
            .ReturnsAsync(RoastPreferenceDefaults.CoolingNotificationsEnabled);
        RoastingSettingsPageViewModel viewModel = CreateViewModel(preferences);

        await viewModel.OnAppearingAsync();

        viewModel.CoolingDurationMinutes.Should().Be(5);
        viewModel.CoolingDurationDisplay.Should().Be("5 min");
        viewModel.FirstCrackEnabled.Should().BeFalse();
        viewModel.WeightPrecisionDisplay.Should().StartWith("0");
        viewModel.WeightPrecisionDisplay.Should().EndWith(" g");
    }

    [Fact]
    public async Task ChangingCoolingDuration_PersistsTheChoiceInSeconds()
    {
        var preferences = CreatePreferences();
        preferences
            .Setup(service => service.SetCoolingDurationSecondsAsync(It.IsAny<int>()))
            .ReturnsAsync(true);
        RoastingSettingsPageViewModel viewModel = CreateViewModel(preferences);
        await viewModel.OnAppearingAsync();

        viewModel.SelectedCoolingDurationIndex =
            RoastingSettingsPageViewModel.CoolingDurationMinuteOptions.ToList().IndexOf(10);

        preferences.Verify(service => service.SetCoolingDurationSecondsAsync(600), Times.Once);
        viewModel.CoolingDurationMinutes.Should().Be(10);
    }

    [Fact]
    public async Task WhenAPreferenceCannotBeStored_TheToggleReturnsToItsPreviousValue()
    {
        var preferences = CreatePreferences();
        preferences
            .Setup(service => service.SetFirstCrackEnabledAsync(It.IsAny<bool>()))
            .ReturnsAsync(false);
        var alerts = new Mock<IAlertService>();
        RoastingSettingsPageViewModel viewModel = CreateViewModel(preferences, alerts: alerts);
        await viewModel.OnAppearingAsync();

        viewModel.FirstCrackEnabled = true;
        await Task.Yield();

        viewModel.FirstCrackEnabled.Should().BeFalse();
        alerts.Verify(
            service => service.ShowAlertAsync("Preference Not Saved", It.IsAny<string>(), "OK"),
            Times.Once);
    }

    [Fact]
    public async Task OnAnUnsupportedPlatform_TheNotificationToggleIsLockedAndSaysWhy()
    {
        var notifications = new Mock<ICoolingNotificationService>();
        notifications
            .Setup(service => service.GetPermissionStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CoolingNotificationPermissionState.Unavailable);
        RoastingSettingsPageViewModel viewModel = CreateViewModel(
            CreatePreferences(),
            notifications);

        await viewModel.OnAppearingAsync();

        viewModel.CanChangeNotificationPreference.Should().BeFalse();
        viewModel.NotificationStatusMessage.Should().Contain("not available");
    }

    [Fact]
    public async Task EnablingNotifications_KeepsThePreferenceWhenTheOsPermissionIsDeclined()
    {
        var preferences = CreatePreferences();
        preferences
            .Setup(service => service.SetCoolingNotificationsEnabledAsync(It.IsAny<bool>()))
            .ReturnsAsync(true);
        var notifications = new Mock<ICoolingNotificationService>();
        notifications
            .Setup(service => service.GetPermissionStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CoolingNotificationPermissionState.NotDetermined);
        notifications
            .Setup(service => service.RequestPermissionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CoolingNotificationPermissionState.Denied);
        RoastingSettingsPageViewModel viewModel = CreateViewModel(preferences, notifications);
        await viewModel.OnAppearingAsync();

        viewModel.CoolingNotificationsEnabled = true;
        await Task.Yield();
        await Task.Yield();

        // The app preference and the OS decision are separate facts; a denial changes only
        // the second, and the user is told so rather than silently switched off.
        viewModel.CoolingNotificationsEnabled.Should().BeTrue();
        viewModel.NotificationPermissionState.Should().Be(CoolingNotificationPermissionState.Denied);
        viewModel.HasNotificationConflict.Should().BeTrue();
        preferences.Verify(service => service.SetCoolingNotificationsEnabledAsync(true), Times.Once);
    }

    private static Mock<IRoastPreferencesService> CreatePreferences()
    {
        var preferences = new Mock<IRoastPreferencesService>();
        preferences
            .Setup(service => service.GetCoolingDurationSecondsAsync())
            .ReturnsAsync(RoastPreferenceDefaults.CoolingDurationSeconds);
        preferences
            .Setup(service => service.GetFirstCrackEnabledAsync())
            .ReturnsAsync(RoastPreferenceDefaults.FirstCrackEnabled);
        preferences
            .Setup(service => service.GetCoolingNotificationsEnabledAsync())
            .ReturnsAsync(RoastPreferenceDefaults.CoolingNotificationsEnabled);
        return preferences;
    }

    private static RoastingSettingsPageViewModel CreateViewModel(
        Mock<IRoastPreferencesService> preferences,
        Mock<ICoolingNotificationService>? notifications = null,
        Mock<IAlertService>? alerts = null)
    {
        if (notifications is null)
        {
            notifications = new Mock<ICoolingNotificationService>();
            notifications
                .Setup(service => service.GetPermissionStateAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CoolingNotificationPermissionState.Granted);
        }

        return new RoastingSettingsPageViewModel(
            preferences.Object,
            notifications.Object,
            alerts?.Object ?? Mock.Of<IAlertService>(),
            Mock.Of<ICoolingNotificationWorkflow>());
    }
}
