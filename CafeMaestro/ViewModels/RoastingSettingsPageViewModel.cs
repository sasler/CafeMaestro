using System.Collections.ObjectModel;
using CafeMaestro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeMaestro.ViewModels;

/// <summary>
/// The few preferences that change how a future roast behaves. Every value is written
/// through <see cref="IRoastPreferencesService"/>, so an active draft keeps the snapshot it
/// started with and only subsequent roasts see a change.
/// </summary>
public partial class RoastingSettingsPageViewModel : ObservableObject
{
    private static readonly int[] CoolingOptions = [0, 1, 2, 3, 5, 7, 10, 15, 20, 30];

    /// <summary>Selectable cooling windows in minutes. Zero means no cooling countdown.</summary>
    public static IReadOnlyList<int> CoolingDurationMinuteOptions => CoolingOptions;

    private readonly IRoastPreferencesService _roastPreferences;
    private readonly ICoolingNotificationService _notifications;
    private readonly IAlertService _alertService;
    private readonly ICoolingNotificationWorkflow _notificationWorkflow;
    private bool _isLoading;

    public RoastingSettingsPageViewModel(
        IRoastPreferencesService roastPreferences,
        ICoolingNotificationService notifications,
        IAlertService alertService,
        ICoolingNotificationWorkflow notificationWorkflow)
    {
        _roastPreferences = roastPreferences ?? throw new ArgumentNullException(nameof(roastPreferences));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _notificationWorkflow = notificationWorkflow ?? throw new ArgumentNullException(nameof(notificationWorkflow));

        CoolingDurationChoices = new ObservableCollection<string>(
            CoolingDurationMinuteOptions.Select(FormatCoolingMinutes));
    }

    public ObservableCollection<string> CoolingDurationChoices { get; }

    [ObservableProperty]
    public partial bool FirstCrackEnabled { get; set; } = RoastPreferenceDefaults.FirstCrackEnabled;

    [ObservableProperty]
    public partial int SelectedCoolingDurationIndex { get; set; } =
        IndexOfMinutes(RoastPreferenceDefaults.CoolingDurationSeconds / 60);

    [ObservableProperty]
    public partial bool CoolingNotificationsEnabled { get; set; } =
        RoastPreferenceDefaults.CoolingNotificationsEnabled;

    [ObservableProperty]
    public partial CoolingNotificationPermissionState NotificationPermissionState { get; set; } =
        CoolingNotificationPermissionState.Unavailable;

    /// <summary>The 0.1 g capture precision is a fixed contract, shown so it is not a surprise.</summary>
    public string WeightPrecisionDisplay =>
        $"{RoastPreferenceDefaults.WeightPrecisionGrams.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture)} g";

    public int CoolingDurationMinutes => MinutesAt(SelectedCoolingDurationIndex);

    public string CoolingDurationDisplay => FormatCoolingMinutes(CoolingDurationMinutes);

    public string FirstCrackSummary => FirstCrackEnabled
        ? "Mark 1C, development time, and DTR appear on future roasts."
        : "Future roasts stay on the simple timer.";

    /// <summary>
    /// The OS side of the story, kept apart from the app preference above it so a switched-on
    /// reminder that cannot be delivered still says why.
    /// </summary>
    public string NotificationStatusMessage => NotificationPermissionState switch
    {
        CoolingNotificationPermissionState.Unavailable =>
            "Reminders are not available on this device.",
        CoolingNotificationPermissionState.Denied =>
            "Notifications are turned off for CafeMaestro in system settings.",
        CoolingNotificationPermissionState.NotDetermined => CoolingNotificationsEnabled
            ? "CafeMaestro will ask for notification permission after your first drop."
            : "Turning this on will ask for notification permission.",
        _ => CoolingNotificationsEnabled
            ? "CafeMaestro will notify you when a batch is ready to weigh."
            : "The cooling countdown still runs inside the app."
    };

    public bool CanChangeNotificationPreference =>
        NotificationPermissionState != CoolingNotificationPermissionState.Unavailable;

    /// <summary>True when the preference is on but the OS will not deliver the reminder.</summary>
    public bool HasNotificationConflict =>
        CoolingNotificationsEnabled &&
        NotificationPermissionState == CoolingNotificationPermissionState.Denied;

    public async Task OnAppearingAsync()
    {
        _isLoading = true;
        try
        {
            FirstCrackEnabled = await _roastPreferences.GetFirstCrackEnabledAsync();
            SelectedCoolingDurationIndex = IndexOfMinutes(
                await _roastPreferences.GetCoolingDurationSecondsAsync() / 60);
            CoolingNotificationsEnabled = await _roastPreferences.GetCoolingNotificationsEnabledAsync();
            NotificationPermissionState = await SafeGetPermissionStateAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task RequestNotificationPermissionAsync()
    {
        try
        {
            NotificationPermissionState = await _notifications.RequestPermissionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification permission request failed: {ex.Message}");
        }
    }

    partial void OnFirstCrackEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(FirstCrackSummary));
        if (_isLoading)
        {
            return;
        }

        _ = PersistAsync(
            () => _roastPreferences.SetFirstCrackEnabledAsync(value),
            revert: () => FirstCrackEnabled = !value,
            "First Crack tracking could not be saved.");
    }

    partial void OnSelectedCoolingDurationIndexChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(CoolingDurationMinutes));
        OnPropertyChanged(nameof(CoolingDurationDisplay));
        if (_isLoading)
        {
            return;
        }

        _ = PersistAsync(
            () => _roastPreferences.SetCoolingDurationSecondsAsync(MinutesAt(newValue) * 60),
            revert: () => SelectedCoolingDurationIndex = oldValue,
            "The cooling duration could not be saved.");
    }

    partial void OnCoolingNotificationsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(NotificationStatusMessage));
        OnPropertyChanged(nameof(HasNotificationConflict));
        if (_isLoading)
        {
            return;
        }

        _ = UpdateNotificationPreferenceAsync(value);
    }

    partial void OnNotificationPermissionStateChanged(CoolingNotificationPermissionState value)
    {
        OnPropertyChanged(nameof(NotificationStatusMessage));
        OnPropertyChanged(nameof(CanChangeNotificationPreference));
        OnPropertyChanged(nameof(HasNotificationConflict));
    }

    private async Task UpdateNotificationPreferenceAsync(bool enabled)
    {
        if (!await _roastPreferences.SetCoolingNotificationsEnabledAsync(enabled))
        {
            _isLoading = true;
            CoolingNotificationsEnabled = !enabled;
            _isLoading = false;
            await _alertService.ShowAlertAsync(
                "Preference Not Saved",
                "The cooling notification preference could not be saved.",
                "OK");
            return;
        }

        // Asking only when the preference is switched on keeps the OS prompt tied to an
        // explicit request rather than app launch. A denial leaves the preference intact.
        if (enabled && NotificationPermissionState == CoolingNotificationPermissionState.NotDetermined)
        {
            await RequestNotificationPermissionAsync();
        }

        await _notificationWorkflow.ReconcileAsync();
    }

    private async Task PersistAsync(Func<Task<bool>> write, Action revert, string failureMessage)
    {
        if (await write())
        {
            return;
        }

        _isLoading = true;
        revert();
        _isLoading = false;
        await _alertService.ShowAlertAsync("Preference Not Saved", failureMessage, "OK");
    }

    private async Task<CoolingNotificationPermissionState> SafeGetPermissionStateAsync()
    {
        try
        {
            return await _notifications.GetPermissionStateAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification permission state unavailable: {ex.Message}");
            return CoolingNotificationPermissionState.Unavailable;
        }
    }

    private static int MinutesAt(int index) =>
        index >= 0 && index < CoolingOptions.Length
            ? CoolingOptions[index]
            : RoastPreferenceDefaults.CoolingDurationSeconds / 60;

    /// <summary>
    /// Falls back to the default window when a stored value is not one of the offered
    /// choices, so an out-of-band preference never leaves the picker blank.
    /// </summary>
    private static int IndexOfMinutes(int minutes)
    {
        int index = Array.IndexOf(CoolingOptions, minutes);
        return index >= 0
            ? index
            : Array.IndexOf(CoolingOptions, RoastPreferenceDefaults.CoolingDurationSeconds / 60);
    }

    private static string FormatCoolingMinutes(int minutes) =>
        minutes == 0 ? "Off" : $"{minutes} min";
}
