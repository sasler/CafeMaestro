using System.Text.Json;
using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class ManagedAppDataMigrationTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public ManagedAppDataMigrationTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task InitializeAsync_LegacyCanonicalFile_MigratesOnceAndCreatesRawRecoveryCopy()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        const string legacyJson = """
            {
              "beans": [],
              "roastlogs": [
                {
                  "Id": "751776dc-fbb1-4484-a949-7d4875491585",
                  "BeanType": "Ethiopia",
                  "Temperature": 205,
                  "BatchWeight": 200,
                  "FinalWeight": 0,
                  "RoastMinutes": 9,
                  "RoastSeconds": 30,
                  "RoastDate": "2025-03-02T09:45:00Z",
                  "Notes": "legacy"
                }
              ],
              "RoastLevels": [{ "Name": "All", "MinWeightLossPercentage": 0, "MaxWeightLossPercentage": 100 }]
            }
            """;
        await File.WriteAllTextAsync(canonicalPath, legacyJson);
        var service = new ManagedAppDataService(canonicalPath, () => "2.0.0");

        AppData loaded = await service.InitializeAsync(Mock.Of<IPreferencesService>());

        loaded.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
        loaded.RoastLogs.Single().CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
        string backupPath = Directory.EnumerateFiles(
            Path.Combine(_testDirectory, "Backups"),
            "cafemaestro_safety_*.json").Should().ContainSingle().Subject;
        (await File.ReadAllTextAsync(backupPath)).Should().Be(legacyJson);
        JsonDocument persisted = JsonDocument.Parse(await File.ReadAllTextAsync(canonicalPath));
        persisted.RootElement.GetProperty("DataSchemaVersion").GetInt32()
            .Should().Be(AppDataSchema.CurrentVersion);

        string migratedJson = await File.ReadAllTextAsync(canonicalPath);
        await service.LoadAppDataAsync();
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(migratedJson);
        Directory.EnumerateFiles(
            Path.Combine(_testDirectory, "Backups"),
            "cafemaestro_safety_*.json").Should().ContainSingle();
    }

    [Fact]
    public async Task InitializeAsync_AlreadyCurrentFile_DoesNotRewriteOrCreateBackup()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        AppData current = AppDataFactory.CreateDefault();
        current.AppVersion = "unchanged";
        string originalJson = JsonSerializer.Serialize(current);
        await File.WriteAllTextAsync(canonicalPath, originalJson);
        var service = new ManagedAppDataService(canonicalPath);

        await service.InitializeAsync(Mock.Of<IPreferencesService>());

        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(originalJson);
        Directory.Exists(Path.Combine(_testDirectory, "Backups")).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ForwardVersion_RejectsWithoutRewritingOrBackingUp()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        string originalJson = $$"""
            { "DataSchemaVersion": {{AppDataSchema.CurrentVersion + 1}}, "Beans": [], "RoastLogs": [] }
            """;
        await File.WriteAllTextAsync(canonicalPath, originalJson);
        var service = new ManagedAppDataService(canonicalPath);

        Func<Task> action = () => service.InitializeAsync(Mock.Of<IPreferencesService>());

        await action.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*newer*recovery*");
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(originalJson);
        Directory.Exists(Path.Combine(_testDirectory, "Backups")).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_InvalidMigratedData_PreservesCanonicalAndRecoveryCopy()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        const string invalidLegacyJson = """
            {
              "Beans": [],
              "RoastLogs": [{
                "BeanType": "Invalid",
                "Temperature": 205,
                "BatchWeight": 100,
                "FinalWeight": 110,
                "RoastMinutes": 10,
                "RoastSeconds": 0,
                "RoastDate": "2025-01-01T12:00:00Z"
              }]
            }
            """;
        await File.WriteAllTextAsync(canonicalPath, invalidLegacyJson);
        var service = new ManagedAppDataService(canonicalPath);

        Func<Task> action = () => service.InitializeAsync(Mock.Of<IPreferencesService>());

        await action.Should().ThrowAsync<InvalidDataException>();
        await action.Should().ThrowAsync<InvalidDataException>();
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(invalidLegacyJson);
        string backupPath = Directory.EnumerateFiles(
            Path.Combine(_testDirectory, "Backups"),
            "cafemaestro_safety_*.json").Should().ContainSingle().Subject;
        (await File.ReadAllTextAsync(backupPath)).Should().Be(invalidLegacyJson);
    }

    [Theory]
    [InlineData("{\"RoastLogs\":[]}")]
    [InlineData("{\"Beans\":null,\"RoastLogs\":null,\"RoastLevels\":null}")]
    public async Task InitializeAsync_LegacyMissingOrNullCollections_MigratesSerializedData(
        string legacyJson)
    {
        string canonicalPath = Path.Combine(_testDirectory, $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(canonicalPath, legacyJson);
        var service = new ManagedAppDataService(canonicalPath);

        AppData loaded = await service.InitializeAsync(Mock.Of<IPreferencesService>());

        loaded.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
        loaded.Beans.Should().BeEmpty();
        loaded.RoastLogs.Should().BeEmpty();
        loaded.RoastLevels.Should().HaveCount(7);
    }

    [Fact]
    public async Task InvalidMigrationRecovery_IsDiscoverableAndExportableAsOriginalBytes()
    {
        string canonicalPath = Path.Combine(_testDirectory, "invalid-for-recovery.json");
        const string invalidLegacyJson = """
            {
              "Beans": [],
              "RoastLogs": [{
                "BeanType": "Invalid",
                "Temperature": 205,
                "BatchWeight": 100,
                "FinalWeight": 110,
                "RoastMinutes": 10,
                "RoastSeconds": 0,
                "RoastDate": "2025-01-01T12:00:00Z"
              }]
            }
            """;
        await File.WriteAllTextAsync(canonicalPath, invalidLegacyJson);
        var appData = new ManagedAppDataService(canonicalPath);

        await FluentActions.Invoking(() =>
                appData.InitializeAsync(Mock.Of<IPreferencesService>()))
            .Should().ThrowAsync<InvalidDataException>();
        var backups = new DataBackupService(
            appData,
            Path.Combine(_testDirectory, "Backups"));

        DataBackupSummary recovery =
            (await backups.GetSafetyBackupsAsync()).Should().ContainSingle().Subject;
        recovery.IsRestorable.Should().BeFalse();
        recovery.IsRawRecovery.Should().BeTrue();
        await using Stream stream = await backups.CreateSafetyBackupExportStreamAsync(recovery.Id);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Should().Be(invalidLegacyJson);
        Directory.EnumerateFiles(Path.Combine(_testDirectory, "Backups"), "*.tmp")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_LegacyNullElement_PreservesDiscoverableRawRecovery()
    {
        string canonicalPath = Path.Combine(_testDirectory, "legacy-null-element.json");
        const string invalidLegacyJson = "{\"RoastLogs\":[null]}";
        await File.WriteAllTextAsync(canonicalPath, invalidLegacyJson);
        var appData = new ManagedAppDataService(canonicalPath);

        await FluentActions.Invoking(() =>
                appData.InitializeAsync(Mock.Of<IPreferencesService>()))
            .Should().ThrowAsync<InvalidDataException>();

        var backups = new DataBackupService(
            appData,
            Path.Combine(_testDirectory, "Backups"));
        DataBackupSummary recovery =
            (await backups.GetSafetyBackupsAsync()).Should().ContainSingle().Subject;
        recovery.IsRawRecovery.Should().BeTrue();
        await using Stream stream = await backups.CreateSafetyBackupExportStreamAsync(recovery.Id);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Should().Be(invalidLegacyJson);
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(invalidLegacyJson);
    }

    [Fact]
    public async Task InitializeAsync_ProductionShapedLegacyHistory_MigratesLinkingEdgesFromJson()
    {
        Guid uniqueBeanId = Guid.NewGuid();
        string canonicalPath = Path.Combine(_testDirectory, "production-shaped-v1.json");
        string legacyJson = $$"""
            {
              "Beans": [
                { "Id": "{{uniqueBeanId}}", "Country": "Ethiopia", "CoffeeName": "Guji", "Variety": "Heirloom", "Quantity": 1, "RemainingQuantity": 1 },
                { "Country": "Brazil", "CoffeeName": "Santos", "Variety": "Bourbon", "Quantity": 1, "RemainingQuantity": 1 },
                { "Country": "Brazil", "CoffeeName": "Santos", "Variety": "Bourbon", "Quantity": 1, "RemainingQuantity": 1 },
                { "Country": "Rwanda", "CoffeeName": "New name", "Variety": "Bourbon", "Quantity": 1, "RemainingQuantity": 1 }
              ],
              "RoastLogs": [
                { "BeanType": "Ethiopia - Guji (Heirloom)", "Temperature": 205, "BatchWeight": 200, "FinalWeight": 170, "RoastMinutes": 10, "RoastSeconds": 0, "RoastDate": "2025-01-01T12:00:00Z" },
                { "BeanType": "Brazil - Santos (Bourbon)", "Temperature": 205, "BatchWeight": 200, "FinalWeight": 170, "RoastMinutes": 10, "RoastSeconds": 0, "RoastDate": "2025-01-02T12:00:00Z" },
                { "BeanType": "Rwanda - Old name (Bourbon)", "Temperature": 205, "BatchWeight": 200, "FinalWeight": 0, "RoastMinutes": 10, "RoastSeconds": 0, "RoastDate": "2025-01-03T12:00:00Z" }
              ]
            }
            """;
        await File.WriteAllTextAsync(canonicalPath, legacyJson);
        var service = new ManagedAppDataService(canonicalPath, () => "1.5.0");

        AppData loaded = await service.InitializeAsync(Mock.Of<IPreferencesService>());

        loaded.RoastLogs[0].BeanId.Should().Be(uniqueBeanId);
        loaded.RoastLogs[0].CompletionStatus.Should().Be(RoastCompletionStatus.Complete);
        loaded.RoastLogs[1].BeanId.Should().BeNull();
        loaded.RoastLogs[2].BeanId.Should().BeNull();
        loaded.RoastLogs[2].BeanDisplaySnapshot.Should().Be("Rwanda - Old name (Bourbon)");
        loaded.RoastLogs[2].CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
