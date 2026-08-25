using System.Text.Json;
using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class ManagedAppDataServiceTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public ManagedAppDataServiceTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task InitializeAsync_MigratesLegacyFileWithoutChangingSource()
    {
        string legacyPath = Path.Combine(_testDirectory, "legacy.json");
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        const string originalJson = """
            {
              "Beans": [
                {
                  "CoffeeName": "Legacy",
                  "Country": "Test",
                  "Quantity": 1,
                  "RemainingQuantity": 1
                }
              ],
              "RoastLogs": [],
              "RoastLevels": [
                { "Name": "All", "MinWeightLossPercentage": 0, "MaxWeightLossPercentage": 100 }
              ]
            }
            """;
        await File.WriteAllTextAsync(legacyPath, originalJson);
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync(legacyPath);

        var service = new ManagedAppDataService(canonicalPath);
        AppData loaded = await service.InitializeAsync(preferences.Object);

        loaded.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Legacy");
        service.DataFilePath.Should().Be(canonicalPath);
        File.Exists(canonicalPath).Should().BeTrue();
        (await File.ReadAllTextAsync(legacyPath)).Should().Be(originalJson);
        preferences.Verify(
            candidate => candidate.SaveAppDataFilePathAsync(canonicalPath),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_InvalidLegacyPath_PreservesDiscoverableRawRecovery()
    {
        string legacyPath = Path.Combine(_testDirectory, "invalid-legacy.json");
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        const string originalJson = "{\"RoastLogs\":[null]}";
        await File.WriteAllTextAsync(legacyPath, originalJson);
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync(legacyPath);
        var service = new ManagedAppDataService(canonicalPath);

        await FluentActions.Invoking(() => service.InitializeAsync(preferences.Object))
            .Should().ThrowAsync<InvalidDataException>();

        string backupDirectory = Path.Combine(_testDirectory, "Backups");
        string backup = Directory.EnumerateFiles(
            backupDirectory,
            SafetyBackupFile.SearchPattern).Should().ContainSingle().Subject;
        (await File.ReadAllTextAsync(backup)).Should().Be(originalJson);
        (await File.ReadAllTextAsync(legacyPath)).Should().Be(originalJson);
        File.Exists(canonicalPath).Should().BeFalse();
        service.IsRecoveryRequired.Should().BeFalse();
        var backupService = new DataBackupService(service, backupDirectory);
        (await backupService.GetSafetyBackupsAsync()).Should().ContainSingle(summary =>
            !summary.IsRestorable && summary.DisplayName == "Raw recovery copy");
    }

    [Fact]
    public async Task SaveAppDataAsync_UsesCanonicalFileAndLeavesNoTemporaryFile()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        var preferences = new Mock<IPreferencesService>();
        var service = new ManagedAppDataService(canonicalPath, () => "1.3.0");
        await service.InitializeAsync(preferences.Object);
        AppData data = await service.LoadAppDataAsync();
        data.Beans.Add(new BeanData
        {
            CoffeeName = "Saved",
            Country = "Test",
            Quantity = 1,
            RemainingQuantity = 1
        });

        bool result = await service.SaveAppDataAsync(data);

        result.Should().BeTrue();
        Directory.EnumerateFiles(_testDirectory, "*.tmp").Should().BeEmpty();
        AppData? persisted = JsonSerializer.Deserialize<AppData>(
            await File.ReadAllTextAsync(canonicalPath));
        persisted!.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Saved");
        persisted.AppVersion.Should().Be("1.3.0");
    }

    [Fact]
    public async Task InitializeAsync_InaccessibleLegacyPathFallsBackToCanonicalStorage()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync("\0");
        var service = new ManagedAppDataService(canonicalPath);

        AppData loaded = await service.InitializeAsync(preferences.Object);

        loaded.Should().NotBeNull();
        File.Exists(canonicalPath).Should().BeTrue();
        preferences.Verify(
            candidate => candidate.SaveAppDataFilePathAsync(canonicalPath),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_PostLoadPreferencesFailure_DoesNotMarkCanonicalRecovery()
    {
        string canonicalPath = Path.Combine(_testDirectory, "valid-canonical.json");
        await File.WriteAllTextAsync(
            canonicalPath,
            JsonSerializer.Serialize(AppDataFactory.CreateDefault()));
        var preferences = new Mock<IPreferencesService>();
        preferences
            .Setup(service => service.SaveAppDataFilePathAsync(canonicalPath))
            .ThrowsAsync(new IOException("Preferences unavailable."));
        var service = new ManagedAppDataService(canonicalPath);

        Func<Task> action = () => service.InitializeAsync(preferences.Object);

        await action.Should().ThrowAsync<IOException>();
        service.IsRecoveryRequired.Should().BeFalse();
        service.CurrentData.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
    }
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
