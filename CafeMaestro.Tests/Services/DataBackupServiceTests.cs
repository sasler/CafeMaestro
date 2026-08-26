using System.Text;
using System.Text.Json;
using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class DataBackupServiceTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public DataBackupServiceTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task RestoreExternalBackupAsync_ReplacesCurrentDataWithoutChangingSource()
    {
        string sourcePath = Path.Combine(_testDirectory, "source.json");
        var restoredData = CreateData("Restored", beanCount: 2, roastCount: 1);
        string originalJson = JsonSerializer.Serialize(restoredData);
        await File.WriteAllTextAsync(sourcePath, originalJson);

        var appDataService = CreateAppDataService(CreateData("Current", 1, 0));
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        DataBackupSummary preview = await service.PreviewExternalBackupAsync(sourcePath);
        await service.RestoreExternalBackupAsync(sourcePath);

        preview.BeanCount.Should().Be(2);
        preview.RoastCount.Should().Be(1);
        appDataService.Verify(
            candidate => candidate.SaveAppDataAsync(
                It.Is<AppData>(data => data.Beans.Count == 2 && data.RoastLogs.Count == 1)),
            Times.Once);
        (await File.ReadAllTextAsync(sourcePath)).Should().Be(originalJson);
        (await service.GetSafetyBackupsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task RestoreExternalBackupAsync_InvalidJsonDoesNotReplaceOrBackUpCurrentData()
    {
        string sourcePath = Path.Combine(_testDirectory, "invalid.json");
        await File.WriteAllTextAsync(sourcePath, "{not-json");

        var appDataService = CreateAppDataService(CreateData("Current", 1, 0));
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        Func<Task> action = () => service.RestoreExternalBackupAsync(sourcePath);

        await action.Should().ThrowAsync<InvalidDataException>();
        appDataService.Verify(
            candidate => candidate.SaveAppDataAsync(It.IsAny<AppData>()),
            Times.Never);
        (await service.GetSafetyBackupsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task StartNewDataAsync_KeepsOnlyFiveNewestSafetyBackups()
    {
        var appDataService = CreateAppDataService(CreateData("Current", 1, 1));
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        for (int index = 0; index < 6; index++)
        {
            await service.StartNewDataAsync();
            await Task.Delay(5);
        }

        IReadOnlyList<DataBackupSummary> backups = await service.GetSafetyBackupsAsync();
        backups.Should().HaveCount(5);
        backups.Should().BeInDescendingOrder(backup => backup.CreatedAt);
        backups.Should().OnlyContain(backup => backup.CreatedAt.Kind == DateTimeKind.Local);
    }

    [Fact]
    public async Task StartNewDataAsync_ColdForwardCanonical_PreservesRawAndReplacesDeliberately()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        string originalJson = $$"""
            {
              "DataSchemaVersion": {{AppDataSchema.CurrentVersion + 1}},
              "Beans": [],
              "RoastLogs": [],
              "RoastLevels": []
            }
            """;
        await File.WriteAllTextAsync(canonicalPath, originalJson);
        var appData = new ManagedAppDataService(canonicalPath, () => "1.5.0");
        var service = new DataBackupService(
            appData,
            Path.Combine(_testDirectory, "Backups"));

        AppData replacement = await service.StartNewDataAsync();

        replacement.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
        replacement.Beans.Should().BeEmpty();
        appData.IsRecoveryRequired.Should().BeFalse();
        AppData persisted = JsonSerializer.Deserialize<AppData>(
            await File.ReadAllTextAsync(canonicalPath))!;
        persisted.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
        string rawBackup = Directory.EnumerateFiles(
            Path.Combine(_testDirectory, "Backups"),
            SafetyBackupFile.SearchPattern).Should().ContainSingle().Subject;
        (await File.ReadAllTextAsync(rawBackup)).Should().Be(originalJson);
    }

    [Fact]
    public async Task StartNewDataAsync_ConcurrentCommitAfterBackup_UsesRevisionGuardAndPreservesCommit()
    {
        string canonicalPath = Path.Combine(_testDirectory, "concurrent-replacement.json");
        var appData = new ManagedAppDataService(canonicalPath, () => "1.5.0");
        await appData.InitializeAsync(Mock.Of<IPreferencesService>());
        var backupCaptured = new TaskCompletionSource<AppData>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBackup = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new DataBackupService(
            appData,
            Path.Combine(_testDirectory, "Backups"),
            async (snapshot, cancellationToken) =>
            {
                backupCaptured.SetResult(snapshot);
                await releaseBackup.Task.WaitAsync(cancellationToken);
            });
        Task<AppData> replacement = service.StartNewDataAsync();
        AppData safetySnapshot = await backupCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2));

        (await appData.UpdateAsync(data => data.Beans.Add(new BeanData
        {
            Country = "Test",
            CoffeeName = "Concurrent",
            Quantity = 1,
            RemainingQuantity = 1
        }))).Should().BeTrue();
        releaseBackup.SetResult();

        Func<Task> action = async () => await replacement;
        await action.Should().ThrowAsync<IOException>();
        safetySnapshot.Beans.Should().BeEmpty();
        appData.CurrentData.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Concurrent");
        (await appData.LoadAppDataAsync()).Beans
            .Should().ContainSingle(bean => bean.CoffeeName == "Concurrent");
    }

    [Fact]
    public async Task StartNewDataAsync_SuccessReturnsCommittedGraphThatCanBeSavedAgain()
    {
        string canonicalPath = Path.Combine(_testDirectory, "committed-replacement.json");
        var appData = new ManagedAppDataService(canonicalPath, () => "1.5.0");
        await appData.InitializeAsync(Mock.Of<IPreferencesService>());
        var service = new DataBackupService(
            appData,
            Path.Combine(_testDirectory, "Backups"));

        AppData replacement = await service.StartNewDataAsync();

        replacement.PersistenceRevision.Should().Be(appData.CurrentData.PersistenceRevision);
        replacement.LastModified.Should().Be(appData.CurrentData.LastModified);
        replacement.AppVersion.Should().Be("1.5.0");
        replacement.Beans.Add(new BeanData
        {
            Country = "Test",
            CoffeeName = "Reusable",
            Quantity = 1,
            RemainingQuantity = 1
        });
        (await appData.SaveAppDataAsync(replacement)).Should().BeTrue();
    }

    [Fact]
    public async Task CreateExportStreamAsync_ReturnsCurrentDataAsJson()
    {
        var appDataService = CreateAppDataService(CreateData("Current", 2, 1));
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        await using Stream stream = await service.CreateExportStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        AppData? exported = JsonSerializer.Deserialize<AppData>(await reader.ReadToEndAsync());

        exported.Should().NotBeNull();
        exported!.Beans.Should().HaveCount(2);
        exported.RoastLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateExportStreamAsync_PreservesStableBeanIdsForDuplicateDisplayNames()
    {
        BeanData first = new()
        {
            Id = Guid.NewGuid(), Country = "Ethiopia", CoffeeName = "Guji", Variety = "Heirloom",
            Quantity = 1, RemainingQuantity = 1
        };
        BeanData second = new()
        {
            Id = Guid.NewGuid(), Country = first.Country, CoffeeName = first.CoffeeName,
            Variety = first.Variety, Quantity = 1, RemainingQuantity = 1
        };
        RoastData firstRoast = new()
        {
            Id = Guid.NewGuid(), BeanId = first.Id, BeanType = first.DisplayName,
            BeanDisplaySnapshot = first.DisplayName, BatchWeight = 220, Temperature = 210,
            RoastDate = new DateTime(2026, 8, 24), CompletionStatus = RoastCompletionStatus.Complete
        };
        RoastData secondRoast = new()
        {
            Id = Guid.NewGuid(), BeanId = second.Id, BeanType = second.DisplayName,
            BeanDisplaySnapshot = second.DisplayName, BatchWeight = 240, Temperature = 225,
            RoastDate = new DateTime(2026, 8, 25), CompletionStatus = RoastCompletionStatus.Complete
        };
        var appDataService = CreateAppDataService(new AppData
        {
            Beans = [first, second],
            RoastLogs = [firstRoast, secondRoast]
        });
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        await using Stream stream = await service.CreateExportStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        AppData exported = JsonSerializer.Deserialize<AppData>(await reader.ReadToEndAsync())!;

        exported.Beans.Select(bean => bean.Id).Should().Equal(first.Id, second.Id);
        exported.RoastLogs.Select(roast => roast.BeanId).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task CreateExportStreamAsync_ColdForwardCanonical_RejectsFallbackExport()
    {
        string canonicalPath = Path.Combine(_testDirectory, "forward-export.json");
        string originalJson = $$"""
            {
              "DataSchemaVersion": {{AppDataSchema.CurrentVersion + 1}},
              "Beans": [],
              "RoastLogs": [],
              "RoastLevels": []
            }
            """;
        await File.WriteAllTextAsync(canonicalPath, originalJson);
        var appData = new ManagedAppDataService(canonicalPath, () => "1.5.0");
        var service = new DataBackupService(
            appData,
            Path.Combine(_testDirectory, "Backups"));

        Func<Task> action = async () => await service.CreateExportStreamAsync();

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*newer*recovery*");
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(originalJson);
    }

    [Theory]
    [InlineData("2025-02-10T13:25:00Z")]
    [InlineData("2025-02-10T13:25:00+02:00")]
    [InlineData("2025-02-10T13:25:00")]
    public async Task RestoreExternalBackupAsync_VersionOneTimestampWireShapes_MigrateBeforeSave(
        string timestamp)
    {
        string sourcePath = Path.Combine(_testDirectory, $"{Guid.NewGuid():N}.json");
        string json = $$"""
            {
              "Beans": [],
              "RoastLogs": [{
                "BeanType": "Peru",
                "Temperature": 205,
                "BatchWeight": 200,
                "FinalWeight": 0,
                "RoastMinutes": 10,
                "RoastSeconds": 0,
                "RoastDate": "{{timestamp}}"
              }]
            }
            """;
        await File.WriteAllTextAsync(sourcePath, json);
        AppData? saved = null;
        var appDataService = CreateAppDataService(CreateData("Current", 1, 0));
        appDataService
            .Setup(service => service.SaveAppDataAsync(It.IsAny<AppData>()))
            .Callback((AppData data) => saved = data)
            .ReturnsAsync(true);
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        await service.RestoreExternalBackupAsync(sourcePath);

        DateTime parsed = JsonSerializer.Deserialize<DateTime>($"\"{timestamp}\"");
        saved!.RoastLogs.Single().DroppedAtUtc
            .Should().Be(V1ToV2AppDataMigration.ConvertLegacyRoastDate(parsed));
        saved.RoastLogs.Single().CompletionStatus
            .Should().Be(RoastCompletionStatus.AwaitingWeight);
    }

    [Fact]
    public async Task PreviewExternalBackupAsync_ForwardSchemaRejectsWithoutReplacingData()
    {
        string sourcePath = Path.Combine(_testDirectory, "forward.json");
        string originalJson = $$"""
            { "DataSchemaVersion": {{AppDataSchema.CurrentVersion + 1}}, "Beans": [], "RoastLogs": [] }
            """;
        await File.WriteAllTextAsync(sourcePath, originalJson);
        var appDataService = CreateAppDataService(CreateData("Current", 1, 0));
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        Func<Task> action = () => service.PreviewExternalBackupAsync(sourcePath);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*newer*recovery*");
        appDataService.Verify(
            candidate => candidate.SaveAppDataAsync(It.IsAny<AppData>()),
            Times.Never);
        (await File.ReadAllTextAsync(sourcePath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task GetSafetyBackupsAsync_NullCollectionElement_RemainsDiscoverableAndExportable()
    {
        string backupDirectory = Path.Combine(_testDirectory, "Backups");
        Directory.CreateDirectory(backupDirectory);
        string backupPath = Path.Combine(backupDirectory, "cafemaestro_safety_null.json");
        const string rawJson = "{\"RoastLogs\":[null]}";
        await File.WriteAllTextAsync(backupPath, rawJson);
        var service = new DataBackupService(
            CreateAppDataService(CreateData("Current", 0, 0)).Object,
            backupDirectory);

        DataBackupSummary raw =
            (await service.GetSafetyBackupsAsync()).Should().ContainSingle().Subject;
        raw.IsRawRecovery.Should().BeTrue();
        await using Stream stream = await service.CreateSafetyBackupExportStreamAsync(raw.Id);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Should().Be(rawJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private static Mock<IAppDataService> CreateAppDataService(AppData currentData)
    {
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.CurrentData).Returns(currentData);
        appDataService.SetupGet(service => service.DataFilePath).Returns("cafemaestro_data.json");
        appDataService.Setup(service => service.LoadAppDataAsync()).ReturnsAsync(() => currentData);
        appDataService
            .Setup(service => service.SaveAppDataAsync(It.IsAny<AppData>()))
            .ReturnsAsync(true)
            .Callback((AppData data) => currentData = data);
        return appDataService;
    }

    private static AppData CreateData(string prefix, int beanCount, int roastCount)
    {
        return new AppData
        {
            AppVersion = "1.2.0",
            LastModified = DateTime.UtcNow,
            Beans = Enumerable.Range(1, beanCount)
                .Select(index => new BeanData
                {
                    CoffeeName = $"{prefix} Bean {index}",
                    Country = "Test",
                    Quantity = 1,
                    RemainingQuantity = 1
                })
                .ToList(),
            RoastLogs = Enumerable.Range(1, roastCount)
                .Select(index => new RoastData
                {
                    BeanType = $"{prefix} Bean {index}",
                    BeanDisplaySnapshot = $"{prefix} Bean {index}",
                    BatchWeight = 1,
                    Temperature = 200,
                    RoastDate = DateTime.UtcNow,
                    DroppedAtUtc = DateTimeOffset.UtcNow,
                    CoolingDurationSeconds = 0,
                    CompletionStatus = RoastCompletionStatus.AwaitingWeight
                })
                .ToList(),
            RoastLevels =
            [
                new RoastLevelData("Light", 0, 12),
                new RoastLevelData("Dark", 12, 100)
            ]
        };
    }
}
