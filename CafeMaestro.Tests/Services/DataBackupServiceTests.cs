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
                    BatchWeight = 1,
                    Temperature = 200,
                    RoastDate = DateTime.UtcNow
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
