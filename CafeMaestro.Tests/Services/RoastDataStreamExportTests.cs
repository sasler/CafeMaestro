using System.Text;
using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class RoastDataStreamExportTests
{
    [Fact]
    public async Task ExportRoastLogAsync_WritesEscapedInvariantCsvToProvidedStream()
    {
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.DataFilePath).Returns("cafemaestro_data.json");
        appDataService
            .Setup(service => service.LoadAppDataAsync())
            .ReturnsAsync(new AppData
            {
                RoastLogs =
                [
                    new RoastData
                    {
                        RoastDate = new DateTime(2026, 7, 24, 8, 30, 0),
                        BeanType = "Ethiopia, \"Natural\"",
                        Temperature = 205.5,
                        BatchWeight = 500.25,
                        FinalWeight = 420.5,
                        RoastMinutes = 9,
                        RoastSeconds = 30,
                        RoastLevelName = "Medium",
                        Notes = "Sweet, \"berry\""
                    }
                ]
            });
        var service = new RoastDataService(
            appDataService.Object,
            Mock.Of<IRoastLevelService>());
        await using var destination = new MemoryStream();

        await service.ExportRoastLogAsync(destination);

        destination.Position = 0;
        using var reader = new StreamReader(destination, Encoding.UTF8);
        string csv = await reader.ReadToEndAsync();
        csv.Should().Contain("\"Ethiopia, \"\"Natural\"\"\"");
        csv.Should().Contain("205.5,500.25,420.5");
        csv.Should().Contain("\"Sweet, \"\"berry\"\"\"");
    }
}
