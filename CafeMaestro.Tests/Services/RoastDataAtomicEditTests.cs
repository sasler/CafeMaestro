using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class RoastDataAtomicEditTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public RoastDataAtomicEditTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task UpdateRoastLogAsync_ClearingFinalWeight_AtomicallyBecomesAwaitingWeight()
    {
        (ManagedAppDataService appData, RoastDataService roasts, RoastData original) =
            await CreateServiceAsync();
        RoastData edited = CreateEditedCopy(original, finalWeight: null);

        bool updated = await roasts.UpdateRoastLogAsync(edited);

        updated.Should().BeTrue();
        RoastData cached = appData.CurrentData.RoastLogs.Should().ContainSingle().Subject;
        cached.FinalWeight.Should().BeNull();
        cached.CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
        cached.RoastLevelName.Should().Be("Pending");
        (await appData.LoadAppDataAsync()).Should().BeEquivalentTo(appData.CurrentData);
    }

    [Fact]
    public async Task UpdateRoastLogAsync_WhenRemindersAreDisabled_CancelsInsteadOfReschedulingAwaitingRoast()
    {
        (ManagedAppDataService appData, _, RoastData original) = await CreateServiceAsync();
        await appData.UpdateAsync(data =>
        {
            RoastData stored = data.RoastLogs.Single();
            stored.FinalWeight = null;
            stored.CompletionStatus = RoastCompletionStatus.AwaitingWeight;
            stored.RoastLevelName = "Pending";
        });

        Mock<ICoolingNotificationService> notifications = new();
        Mock<IRoastPreferencesService> preferences = new();
        preferences.Setup(service => service.GetCoolingNotificationsEnabledAsync())
            .ReturnsAsync(false);
        var levels = new Mock<IRoastLevelService>();
        levels.Setup(service => service.GetRoastLevelNameAsync(It.IsAny<double>()))
            .ReturnsAsync("Medium");
        RoastDataService roasts = new(
            appData,
            levels.Object,
            notifications.Object,
            preferences.Object);

        RoastData edited = CreateEditedCopy(original, finalWeight: null);
        edited.CompletionStatus = RoastCompletionStatus.AwaitingWeight;

        (await roasts.UpdateRoastLogAsync(edited)).Should().BeTrue();

        notifications.Verify(service => service.ScheduleCoolingReadyAsync(
            It.IsAny<Guid>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        notifications.Verify(service => service.CancelAsync(
            original.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(RoastCompletionStatus.Unweighed, "Unweighed")]
    [InlineData(RoastCompletionStatus.Discarded, "Discarded")]
    public async Task UpdateRoastLogAsync_GenericEdit_PreservesResolvedNullWeightStatus(
        RoastCompletionStatus status,
        string levelName)
    {
        (ManagedAppDataService appData, RoastDataService roasts, RoastData original) =
            await CreateServiceAsync();
        await appData.UpdateAsync(data =>
        {
            RoastData stored = data.RoastLogs.Single();
            stored.FinalWeight = null;
            stored.CompletionStatus = status;
            stored.RoastLevelName = levelName;
        });
        RoastData edited = CreateEditedCopy(original, finalWeight: null);
        edited.CompletionStatus = status;
        edited.Notes = "Updated without reopening physical work";

        (await roasts.UpdateRoastLogAsync(edited)).Should().BeTrue();

        RoastData saved = appData.CurrentData.RoastLogs.Single();
        saved.CompletionStatus.Should().Be(status);
        saved.RoastLevelName.Should().Be(levelName);
        saved.Notes.Should().Be("Updated without reopening physical work");
        (await appData.LoadAppDataAsync()).Should().BeEquivalentTo(appData.CurrentData);
    }

    [Fact]
    public async Task UpdateRoastLogAsync_InvalidEdit_RollsBackCacheAndDisk()
    {
        (ManagedAppDataService appData, RoastDataService roasts, RoastData original) =
            await CreateServiceAsync();
        string originalJson = await File.ReadAllTextAsync(appData.DataFilePath);
        RoastData edited = CreateEditedCopy(original, finalWeight: 180);
        edited.Temperature = 600;

        bool updated = await roasts.UpdateRoastLogAsync(edited);

        updated.Should().BeFalse();
        appData.CurrentData.RoastLogs.Single().Temperature.Should().Be(205);
        appData.CurrentData.RoastLogs.Single().CompletionStatus
            .Should().Be(RoastCompletionStatus.Complete);
        (await File.ReadAllTextAsync(appData.DataFilePath)).Should().Be(originalJson);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task UpdateRoastLogAsync_InvalidFinalWeight_DoesNotClearCommittedWeight(
        double invalidFinalWeight)
    {
        (ManagedAppDataService appData, RoastDataService roasts, RoastData original) =
            await CreateServiceAsync();
        string originalJson = await File.ReadAllTextAsync(appData.DataFilePath);
        int eventCount = 0;
        appData.DataChanged += (_, _) => eventCount++;
        RoastData edited = CreateEditedCopy(original, invalidFinalWeight);

        bool updated = await roasts.UpdateRoastLogAsync(edited);

        updated.Should().BeFalse();
        eventCount.Should().Be(0);
        RoastData cached = appData.CurrentData.RoastLogs.Single();
        cached.FinalWeight.Should().Be(180);
        cached.CompletionStatus.Should().Be(RoastCompletionStatus.Complete);
        (await File.ReadAllTextAsync(appData.DataFilePath)).Should().Be(originalJson);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task SaveRoastDataAsync_InvalidFinalWeight_DoesNotLookupOrWrite(
        double invalidFinalWeight)
    {
        string path = Path.Combine(_testDirectory, $"new-invalid-{Guid.NewGuid():N}.json");
        var appData = new ManagedAppDataService(path, () => "1.5.0");
        await appData.InitializeAsync(Mock.Of<IPreferencesService>());
        var levels = new Mock<IRoastLevelService>(MockBehavior.Strict);
        var roasts = new RoastDataService(
            appData,
            levels.Object);
        string originalJson = await File.ReadAllTextAsync(path);
        int eventCount = 0;
        appData.DataChanged += (_, _) => eventCount++;
        RoastData roast = new()
        {
            BeanType = "Ethiopia",
            BeanDisplaySnapshot = "Ethiopia",
            Temperature = 205,
            BatchWeight = 200,
            FinalWeight = invalidFinalWeight,
            RoastDate = DateTime.UtcNow
        };

        bool saved = await roasts.SaveRoastDataAsync(roast);

        saved.Should().BeFalse();
        eventCount.Should().Be(0);
        appData.CurrentData.RoastLogs.Should().BeEmpty();
        (await File.ReadAllTextAsync(path)).Should().Be(originalJson);
        levels.Verify(
            service => service.GetRoastLevelNameAsync(It.IsAny<double>()),
            Times.Never);
    }

    [Fact]
    public async Task AddRoastAsync_NonFiniteFinalWeight_DoesNotWrite()
    {
        string path = Path.Combine(_testDirectory, "add-invalid.json");
        var appData = new ManagedAppDataService(path, () => "1.5.0");
        await appData.InitializeAsync(Mock.Of<IPreferencesService>());
        var roasts = new RoastDataService(
            appData,
            Mock.Of<IRoastLevelService>());
        string originalJson = await File.ReadAllTextAsync(path);
        RoastData roast = new()
        {
            BeanType = "Ethiopia",
            BeanDisplaySnapshot = "Ethiopia",
            Temperature = 205,
            BatchWeight = 200,
            FinalWeight = double.NaN,
            RoastDate = DateTime.UtcNow
        };

        (await roasts.AddRoastAsync(roast)).Should().BeFalse();
        appData.CurrentData.RoastLogs.Should().BeEmpty();
        (await File.ReadAllTextAsync(path)).Should().Be(originalJson);
    }

    [Fact]
    public async Task UpdateRoastLogAsync_AfterBeanRename_PreservesHistoricalSnapshot()
    {
        (ManagedAppDataService appData, RoastDataService roasts, RoastData original) =
            await CreateServiceAsync();
        RoastData edited = CreateEditedCopy(original, finalWeight: 180);
        edited.BeanType = "Ethiopia - Renamed (Heirloom)";
        edited.BeanId = original.BeanId;

        (await roasts.UpdateRoastLogAsync(edited)).Should().BeTrue();

        RoastData persisted = appData.CurrentData.RoastLogs.Single();
        persisted.BeanType.Should().Be("Ethiopia - Renamed (Heirloom)");
        persisted.BeanDisplaySnapshot.Should().Be("Ethiopia");
    }

    [Fact]
    public async Task UpdateRoastLogAsync_ChangingBean_UpdatesLinkAndSnapshotTogether()
    {
        (ManagedAppDataService appData, RoastDataService roasts, RoastData original) =
            await CreateServiceAsync();
        RoastData edited = CreateEditedCopy(original, finalWeight: 180);
        Guid newBeanId = Guid.NewGuid();
        edited.BeanId = newBeanId;
        edited.BeanType = "Kenya - Nyeri (SL28)";

        (await roasts.UpdateRoastLogAsync(edited)).Should().BeTrue();

        RoastData persisted = appData.CurrentData.RoastLogs.Single();
        persisted.BeanId.Should().Be(newBeanId);
        persisted.BeanDisplaySnapshot.Should().Be("Kenya - Nyeri (SL28)");
    }

    [Fact]
    public async Task UpdateRoastLogAsync_MissingId_DoesNotWriteOrRaiseEvent()
    {
        (ManagedAppDataService appData, RoastDataService roasts, RoastData original) =
            await CreateServiceAsync();
        RoastData missing = CreateEditedCopy(original, finalWeight: 180);
        missing.Id = Guid.NewGuid();
        string originalJson = await File.ReadAllTextAsync(appData.DataFilePath);
        int eventCount = 0;
        appData.DataChanged += (_, _) => eventCount++;

        (await roasts.UpdateRoastLogAsync(missing)).Should().BeFalse();

        eventCount.Should().Be(0);
        (await File.ReadAllTextAsync(appData.DataFilePath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task UpdateRoastLogAsync_MissingIdOnColdLegacyFile_DoesNotMigrateOrBackUp()
    {
        Guid roastId = Guid.NewGuid();
        string canonicalPath = Path.Combine(_testDirectory, "cold-legacy.json");
        string legacyJson = $$"""
            {
              "Beans": [],
              "RoastLogs": [{
                "Id": "{{roastId}}",
                "BeanType": "Legacy",
                "Temperature": 205,
                "BatchWeight": 200,
                "FinalWeight": 170,
                "RoastMinutes": 10,
                "RoastSeconds": 0,
                "RoastDate": "2025-01-01T12:00:00Z"
              }]
            }
            """;
        await File.WriteAllTextAsync(canonicalPath, legacyJson);
        var appData = new ManagedAppDataService(canonicalPath, () => "1.5.0");
        var roasts = new RoastDataService(
            appData,
            Mock.Of<IRoastLevelService>());
        int eventCount = 0;
        appData.DataChanged += (_, _) => eventCount++;
        RoastData missing = new()
        {
            Id = Guid.NewGuid(),
            BeanType = "Missing",
            Temperature = 205,
            BatchWeight = 200,
            FinalWeight = 170,
            RoastMinutes = 10,
            RoastDate = DateTime.UtcNow
        };

        (await roasts.UpdateRoastLogAsync(missing)).Should().BeFalse();

        eventCount.Should().Be(0);
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(legacyJson);
        Directory.Exists(Path.Combine(_testDirectory, "Backups")).Should().BeFalse();
    }

    private async Task<(ManagedAppDataService, RoastDataService, RoastData)> CreateServiceAsync()
    {
        string path = Path.Combine(_testDirectory, "cafemaestro_data.json");
        var appData = new ManagedAppDataService(path, () => "1.5.0");
        await appData.InitializeAsync(Mock.Of<IPreferencesService>());
        var original = new RoastData
        {
            BeanType = "Ethiopia",
            BeanDisplaySnapshot = "Ethiopia",
            BeanId = Guid.NewGuid(),
            Temperature = 205,
            BatchWeight = 200,
            FinalWeight = 180,
            RoastMinutes = 10,
            RoastDate = DateTime.UtcNow,
            DroppedAtUtc = DateTimeOffset.UtcNow,
            CoolingDurationSeconds = 300,
            CompletionStatus = RoastCompletionStatus.Complete,
            RoastLevelName = "Medium"
        };
        (await appData.UpdateAsync(data => data.RoastLogs.Add(original))).Should().BeTrue();
        var levels = new Mock<IRoastLevelService>();
        levels.Setup(service => service.GetRoastLevelNameAsync(It.IsAny<double>()))
            .ReturnsAsync("Medium");
        return (
            appData,
            new RoastDataService(appData, levels.Object),
            original);
    }

    private static RoastData CreateEditedCopy(RoastData original, double? finalWeight) => new()
    {
        Id = original.Id,
        BeanType = original.BeanType,
        Temperature = original.Temperature,
        BatchWeight = original.BatchWeight,
        FinalWeight = finalWeight,
        RoastMinutes = original.RoastMinutes,
        RoastSeconds = original.RoastSeconds,
        RoastDate = original.RoastDate,
        Notes = original.Notes
    };

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
