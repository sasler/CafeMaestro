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
        var legacyData = new AppData
        {
            Beans =
            [
                new BeanData
                {
                    CoffeeName = "Legacy",
                    Country = "Test",
                    Quantity = 1,
                    RemainingQuantity = 1
                }
            ],
            RoastLogs = [],
            RoastLevels = [new RoastLevelData("All", 0, 100)]
        };
        string originalJson = JsonSerializer.Serialize(legacyData);
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
    public async Task SaveAppDataAsync_UsesCanonicalFileAndLeavesNoTemporaryFile()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cafemaestro_data.json");
        var preferences = new Mock<IPreferencesService>();
        var service = new ManagedAppDataService(canonicalPath);
        await service.InitializeAsync(preferences.Object);
        AppData data = AppDataFactory.CreateDefault();
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
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
