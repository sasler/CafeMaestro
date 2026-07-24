using System.Text;
using CafeMaestro.Services;
using FluentAssertions;
using Microsoft.Maui.Storage;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class UserFileContentUriTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public UserFileContentUriTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task PickFileAsync_ContentUriUsesOpenReadAsyncInsteadOfFullPath()
    {
        var fileResult = new FileResult("content://documents/beans.csv");
        var picker = new Mock<IFilePicker>();
        picker
            .Setup(service => service.PickAsync(It.IsAny<PickOptions>()))
            .ReturnsAsync(fileResult);
        bool wasOpened = false;
        var service = new UserFileService(
            picker.Object,
            Mock.Of<IDocumentSaveService>(),
            Path.Combine(_testDirectory, "Imports"),
            _ =>
            {
                wasOpened = true;
                Stream stream = new MemoryStream(
                    Encoding.UTF8.GetBytes("Coffee,Country\nStreamed,Neverland"));
                return Task.FromResult(stream);
            });

        UserFileSelection? selection = await service.PickFileAsync(
            UserFileType.Csv,
            "Select beans");

        selection.Should().NotBeNull();
        wasOpened.Should().BeTrue();
        (await File.ReadAllTextAsync(selection!.LocalPath))
            .Should().Be("Coffee,Country\nStreamed,Neverland");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}