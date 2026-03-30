using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class RoastImportPageViewModelTests
{
    [Fact]
    public async Task BrowseCommand_LoadsHeaders_AutoMapsColumns_AndBuildsPreview()
    {
        const string filePath = @"C:\imports\roasts.csv";
        var csvParserService = new Mock<ICsvParserService>();
        csvParserService.Setup(service => service.GetCsvHeadersAsync(filePath))
            .ReturnsAsync(["Roast Date", "Coffee Bean", "Time", "Batch Weight"]);
        csvParserService.Setup(service => service.ReadCsvContentAsync(filePath, 5))
            .ReturnsAsync(
            [
                new Dictionary<string, string>
                {
                    ["Roast Date"] = "2025-03-01",
                    ["Coffee Bean"] = "Kenya AA",
                    ["Time"] = "11:30",
                    ["Batch Weight"] = "220"
                }
            ]);

        var viewModel = CreateViewModel(csvParserService: csvParserService);
        viewModel.PickFileAsync = () => Task.FromResult<string?>(filePath);

        await viewModel.BrowseCommand.ExecuteAsync(null);

        viewModel.FilePath.Should().Be(filePath);
        viewModel.Headers.Should().Contain(["Roast Date", "Coffee Bean", "Time", "Batch Weight"]);
        viewModel.ColumnMappings.Single(mapping => mapping.PropertyKey == "RoastDate").SelectedHeader.Should().Be("Roast Date");
        viewModel.ColumnMappings.Single(mapping => mapping.PropertyKey == "BeanType").SelectedHeader.Should().Be("Coffee Bean");
        viewModel.ColumnMappings.Single(mapping => mapping.PropertyKey == "RoastTime").SelectedHeader.Should().Be("Time");
        viewModel.ColumnMappings.Single(mapping => mapping.PropertyKey == "BatchWeight").SelectedHeader.Should().Be("Batch Weight");
        viewModel.PreviewData.Should().ContainSingle();
        viewModel.ImportStatus.Should().Contain("Ready to import 1 roast log");
        viewModel.CanImport.Should().BeTrue();
    }

    [Fact]
    public async Task ImportCommand_ImportsRoasts_ShowsSummary_AndNavigatesBack()
    {
        const string filePath = @"C:\imports\roasts.csv";
        var csvParserService = new Mock<ICsvParserService>();
        csvParserService.Setup(service => service.GetCsvHeadersAsync(filePath))
            .ReturnsAsync(["Date", "Coffee Bean"]);
        csvParserService.Setup(service => service.ReadCsvContentAsync(filePath, 5))
            .ReturnsAsync(
            [
                new Dictionary<string, string>
                {
                    ["Date"] = "2025-03-01",
                    ["Coffee Bean"] = "Colombia"
                }
            ]);

        var roastDataService = new Mock<IRoastDataService>();
        roastDataService.Setup(service => service.ImportRoastsFromCsvAsync(filePath, It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync((2, 1, new List<string> { "Row 3 missing bean type" }));

        var navigationService = new Mock<INavigationService>();
        navigationService.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);

        var alertService = new Mock<IAlertService>();
        alertService.Setup(service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var viewModel = CreateViewModel(
            roastDataService: roastDataService,
            csvParserService: csvParserService,
            navigationService: navigationService,
            alertService: alertService);
        viewModel.PickFileAsync = () => Task.FromResult<string?>(filePath);

        await viewModel.BrowseCommand.ExecuteAsync(null);
        await viewModel.ImportCommand.ExecuteAsync(null);

        roastDataService.Verify(
            service => service.ImportRoastsFromCsvAsync(
                filePath,
                It.Is<Dictionary<string, string>>(mapping =>
                    mapping["RoastDate"] == "Date" &&
                    mapping["BeanType"] == "Coffee Bean")),
            Times.Once);
        alertService.Verify(
            service => service.ShowAlertAsync(
                "Import Complete",
                It.Is<string>(message => message.Contains("Imported 2 roast log(s). Failed: 1.")),
                "OK"),
            Times.Once);
        navigationService.Verify(service => service.GoBackAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelCommand_NavigatesBack()
    {
        var navigationService = new Mock<INavigationService>();
        navigationService.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);

        var viewModel = CreateViewModel(navigationService: navigationService);

        await viewModel.CancelCommand.ExecuteAsync(null);

        navigationService.Verify(service => service.GoBackAsync(), Times.Once);
    }

    private static RoastImportPageViewModel CreateViewModel(
        Mock<IRoastDataService>? roastDataService = null,
        Mock<ICsvParserService>? csvParserService = null,
        Mock<INavigationService>? navigationService = null,
        Mock<IAlertService>? alertService = null)
    {
        roastDataService ??= new Mock<IRoastDataService>();
        csvParserService ??= new Mock<ICsvParserService>();
        navigationService ??= new Mock<INavigationService>();
        alertService ??= new Mock<IAlertService>();

        navigationService.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);
        alertService.Setup(service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return new RoastImportPageViewModel(
            roastDataService.Object,
            csvParserService.Object,
            navigationService.Object,
            alertService.Object);
    }
}
