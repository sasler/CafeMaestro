using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Microsoft.Maui.Storage;
using Moq;

namespace CafeMaestro.Tests.Services;

/// <summary>Advances without sleeping, so every transition test is deterministic.</summary>
internal sealed class FakeClock(DateTimeOffset startUtc) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = startUtc;

    public void Advance(TimeSpan amount) => UtcNow += amount;

    public void AdvanceSeconds(double seconds) => Advance(TimeSpan.FromSeconds(seconds));
}

internal sealed class FakeRoastPreferencesService : IRoastPreferencesService
{
    public int CoolingDurationSeconds { get; set; } = RoastPreferenceDefaults.CoolingDurationSeconds;
    public bool FirstCrackEnabled { get; set; } = RoastPreferenceDefaults.FirstCrackEnabled;
    public bool CoolingNotificationsEnabled { get; set; } =
        RoastPreferenceDefaults.CoolingNotificationsEnabled;

    public Task<int> GetCoolingDurationSecondsAsync() => Task.FromResult(CoolingDurationSeconds);

    public Task<bool> SetCoolingDurationSecondsAsync(int seconds)
    {
        CoolingDurationSeconds = seconds;
        return Task.FromResult(true);
    }

    public Task<bool> GetFirstCrackEnabledAsync() => Task.FromResult(FirstCrackEnabled);

    public Task<bool> SetFirstCrackEnabledAsync(bool enabled)
    {
        FirstCrackEnabled = enabled;
        return Task.FromResult(true);
    }

    public Task<bool> GetCoolingNotificationsEnabledAsync() =>
        Task.FromResult(CoolingNotificationsEnabled);

    public Task<bool> SetCoolingNotificationsEnabledAsync(bool enabled)
    {
        CoolingNotificationsEnabled = enabled;
        return Task.FromResult(true);
    }
}

internal sealed class RecordingCoolingNotificationService : ICoolingNotificationService
{
    public List<Guid> Scheduled { get; } = [];
    public List<Guid> Cancelled { get; } = [];
    public bool ThrowOnSchedule { get; set; }

    public CoolingNotificationPermissionState PermissionState { get; set; } =
        CoolingNotificationPermissionState.Granted;

    public Task<CoolingNotificationPermissionState> GetPermissionStateAsync(
        CancellationToken cancellationToken = default) => Task.FromResult(PermissionState);

    public Task<CoolingNotificationPermissionState> RequestPermissionAsync(
        CancellationToken cancellationToken = default) => Task.FromResult(PermissionState);

    public Task ScheduleCoolingReadyAsync(
        Guid roastId,
        DateTimeOffset readyToWeighAtUtc,
        string beanDisplayName,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnSchedule)
        {
            throw new InvalidOperationException("Injected notification failure.");
        }

        Scheduled.Add(roastId);
        return Task.CompletedTask;
    }

    public Task CancelAsync(Guid roastId, CancellationToken cancellationToken = default)
    {
        Cancelled.Add(roastId);
        return Task.CompletedTask;
    }
}

internal sealed class RoastSessionPreferences : IPreferences
{
    private readonly Dictionary<string, object?> _values = [];

    public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey(key);

    public void Remove(string key, string? sharedName = null) => _values.Remove(key);

    public void Clear(string? sharedName = null) => _values.Clear();

    public void Set<T>(string key, T value, string? sharedName = null) => _values[key] = value;

    public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
        _values.TryGetValue(key, out object? value) && value is T typed ? typed : defaultValue;
}

/// <summary>
/// Wires the roast domain against the real persistence stack and a temporary data file, so
/// transitions exercise the same atomic mutation, validation, and event path the app uses.
/// </summary>
internal sealed class RoastSessionTestHarness : IDisposable
{
    private readonly bool _ownsDirectory;

    private RoastSessionTestHarness(
        string canonicalPath,
        bool ownsDirectory,
        ManagedAppDataService appDataService,
        FakeClock clock,
        FakeRoastPreferencesService preferences,
        RecordingCoolingNotificationService notifications,
        ICoolingNotificationWorkflow? notificationWorkflow)
    {
        CanonicalPath = canonicalPath;
        _ownsDirectory = ownsDirectory;
        AppDataService = appDataService;
        Clock = clock;
        Preferences = preferences;
        Notifications = notifications;
        RoastLevelService = new Mock<IRoastLevelService>();
        RoastLevelService
            .Setup(service => service.GetRoastLevelNameAsync(It.IsAny<double>()))
            .ReturnsAsync("Medium");
        NotificationWorkflow = notificationWorkflow ?? CreateNotificationWorkflow(
            appDataService, preferences, notifications);
        Session = new RoastSessionService(
            appDataService,
            RoastLevelService.Object,
            preferences,
            NotificationWorkflow,
            clock);
        Query = new RoastQueryService(appDataService, clock);
    }

    private static ICoolingNotificationWorkflow CreateNotificationWorkflow(
        IAppDataService appDataService,
        FakeRoastPreferencesService preferences,
        RecordingCoolingNotificationService notifications)
    {
        RoastSessionPreferences platformPreferences = new();
        platformPreferences.Set("CoolingNotificationFirstDropPromptSeen", true);
        return new CoolingNotificationWorkflow(
            appDataService,
            preferences,
            notifications,
            Mock.Of<IAlertService>(),
            platformPreferences);
    }

    public string CanonicalPath { get; }
    public ManagedAppDataService AppDataService { get; }
    public FakeClock Clock { get; }
    public FakeRoastPreferencesService Preferences { get; }
    public RecordingCoolingNotificationService Notifications { get; }
    public ICoolingNotificationWorkflow NotificationWorkflow { get; }
    public Mock<IRoastLevelService> RoastLevelService { get; }
    public RoastSessionService Session { get; }
    public RoastQueryService Query { get; }
    public AppData Current => AppDataService.CurrentData;

    public static Task<RoastSessionTestHarness> CreateAsync(
        DateTimeOffset? nowUtc = null,
        Func<AppData, CancellationToken, Task>? writeOverride = null,
        ICoolingNotificationWorkflow? notificationWorkflow = null)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "CafeMaestro.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return CreateCoreAsync(
            Path.Combine(directory, "roast-session.json"),
            ownsDirectory: true,
            nowUtc ?? new DateTimeOffset(2026, 3, 14, 9, 0, 0, TimeSpan.Zero),
            writeOverride,
            notificationWorkflow);
    }

    /// <summary>Rebuilds the in-memory services over the same file, as a cold launch would.</summary>
    public Task<RoastSessionTestHarness> RelaunchAsync(DateTimeOffset? nowUtc = null) =>
        CreateCoreAsync(CanonicalPath, ownsDirectory: false, nowUtc ?? Clock.UtcNow, null, null);

    /// <summary>A cold launch over an existing file whose writes can be made to fail.</summary>
    public static Task<RoastSessionTestHarness> ReopenAsync(
        string canonicalPath,
        DateTimeOffset nowUtc,
        Func<AppData, CancellationToken, Task>? writeOverride) =>
        CreateCoreAsync(canonicalPath, ownsDirectory: false, nowUtc, writeOverride, null);

    private static async Task<RoastSessionTestHarness> CreateCoreAsync(
        string canonicalPath,
        bool ownsDirectory,
        DateTimeOffset nowUtc,
        Func<AppData, CancellationToken, Task>? writeOverride,
        ICoolingNotificationWorkflow? notificationWorkflow)
    {
        var appDataService = new ManagedAppDataService(
            canonicalPath,
            () => "2.0.0",
            writeOverride);
        await appDataService.InitializeAsync(Mock.Of<IPreferencesService>());

        return new RoastSessionTestHarness(
            canonicalPath,
            ownsDirectory,
            appDataService,
            new FakeClock(nowUtc),
            new FakeRoastPreferencesService(),
            new RecordingCoolingNotificationService(),
            notificationWorkflow);
    }

    public async Task<BeanData> AddBeanAsync(
        string coffeeName = "Guji",
        double quantityKilograms = 1.0,
        string country = "Ethiopia",
        string variety = "Heirloom")
    {
        var bean = new BeanData
        {
            Country = country,
            CoffeeName = coffeeName,
            Variety = variety,
            Process = "Washed",
            Quantity = quantityKilograms,
            RemainingQuantity = quantityKilograms
        };

        (await AppDataService.UpdateAsync(data => data.Beans.Add(bean))).Should().BeTrue();
        return bean;
    }

    public double RemainingQuantityOf(Guid beanId) =>
        Current.Beans.Single(bean => bean.Id == beanId).RemainingQuantity;

    public void Dispose()
    {
        Session.Dispose();
        string? directory = Path.GetDirectoryName(CanonicalPath);
        if (_ownsDirectory && directory is not null && Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Temporary directory cleanup is best effort.
            }
        }
    }
}
