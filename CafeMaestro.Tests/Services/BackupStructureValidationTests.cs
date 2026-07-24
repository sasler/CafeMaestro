using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class BackupStructureValidationTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public BackupStructureValidationTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task PreviewExternalBackupAsync_EmptyJsonObjectIsRejectedAsWrongStructure()
    {
        string sourcePath = Path.Combine(_testDirectory, "empty-object.json");
        await File.WriteAllTextAsync(sourcePath, "{}");
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.CurrentData).Returns(AppDataFactory.CreateDefault());
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        Func<Task> action = () => service.PreviewExternalBackupAsync(sourcePath);

        await action.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*structure*");
        appDataService.Verify(
            candidate => candidate.SaveAppDataAsync(It.IsAny<AppData>()),
            Times.Never);
    }

    [Fact]
    public async Task RestoreExternalBackupAsync_LegacyJsonWithoutRoastLevelsAddsDefaults()
    {
        string sourcePath = Path.Combine(_testDirectory, "legacy.json");
        await File.WriteAllTextAsync(sourcePath, "{\"Beans\":[],\"RoastLogs\":[]}");
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.CurrentData).Returns(AppDataFactory.CreateDefault());
        appDataService
            .Setup(service => service.SaveAppDataAsync(It.IsAny<AppData>()))
            .ReturnsAsync(true);
        var service = new DataBackupService(
            appDataService.Object,
            Path.Combine(_testDirectory, "Backups"));

        await service.RestoreExternalBackupAsync(sourcePath);

        appDataService.Verify(
            candidate => candidate.SaveAppDataAsync(
                It.Is<AppData>(data => data.RoastLevels.Count == 7)),
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
