using CafeMaestro.Services;
using FluentAssertions;
using Microsoft.Maui.Storage;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class UserFileServiceTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public UserFileServiceTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task PickFileAsync_CopiesPickedContentToManagedTemporaryFile()
    {
        string sourcePath = Path.Combine(_testDirectory, "beans.csv");
        await File.WriteAllTextAsync(sourcePath, "Coffee,Country\nTest,Neverland");

        var filePicker = new Mock<IFilePicker>();
        filePicker
            .Setup(picker => picker.PickAsync(It.IsAny<PickOptions>()))
            .ReturnsAsync(new FileResult(sourcePath));
        var documentSaver = new Mock<IDocumentSaveService>();
        var service = new UserFileService(
            filePicker.Object,
            documentSaver.Object,
            Path.Combine(_testDirectory, "Imports"));

        UserFileSelection? result = await service.PickFileAsync(
            UserFileType.Csv,
            "Select CSV");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("beans.csv");
        result.LocalPath.Should().NotBe(sourcePath);
        (await File.ReadAllTextAsync(result.LocalPath))
            .Should().Be("Coffee,Country\nTest,Neverland");
    }

    [Fact]
    public async Task PickFileAsync_WhenPickerIsCanceled_ReturnsNull()
    {
        var filePicker = new Mock<IFilePicker>();
        filePicker
            .Setup(picker => picker.PickAsync(It.IsAny<PickOptions>()))
            .ReturnsAsync((FileResult?)null);
        var service = new UserFileService(
            filePicker.Object,
            Mock.Of<IDocumentSaveService>(),
            Path.Combine(_testDirectory, "Imports"));

        UserFileSelection? result = await service.PickFileAsync(
            UserFileType.Json,
            "Select backup");

        result.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
