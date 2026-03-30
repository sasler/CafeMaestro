using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class BeanImportPageViewModelTests
{
    [Fact]
    public async Task BrowseCommand_LoadsHeaders_AutoMapsColumns_AndBuildsPreview()
    {
        const string filePath = @"C:\imports\beans.csv";
        var csvParserService = new Mock<ICsvParserService>();
        csvParserService.Setup(service => service.GetCsvHeadersAsync(filePath))
            .ReturnsAsync(["Coffee", "Origin", "Variaty", "Oreder (kg)"]);
        csvParserService.Setup(service => service.ReadCsvContentAsync(filePath, 5))
            .ReturnsAsync(
            [
                new Dictionary<string, string>
                {
                    ["Coffee"] = "Yirgacheffe",
                    ["Origin"] = "Ethiopia",
                    ["Variaty"] = "Heirloom",
                    ["Oreder (kg)"] = "5"
                }
            ]);

        var viewModel = CreateViewModel(csvParserService: csvParserService);
        viewModel.PickFileAsync = () => Task.FromResult<string?>(filePath);

        await viewModel.BrowseCommand.ExecuteAsync(null);

        viewModel.FilePath.Should().Be(filePath);
        viewModel.Headers.Should().Contain(["Coffee", "Origin", "Variaty", "Oreder (kg)"]);
        viewModel.ColumnMappings.Single(mapping => mapping.PropertyKey == "CoffeeName").SelectedHeader.Should().Be("Coffee");
        viewModel.ColumnMappings.Single(mapping => mapping.PropertyKey == "Country").SelectedHeader.Should().Be("Origin");
        viewModel.ColumnMappings.Single(mapping => mapping.PropertyKey == "Variety").SelectedHeader.Should().Be("Variaty");
        viewModel.ColumnMappings.Single(mapping => mapping.PropertyKey == "Quantity").SelectedHeader.Should().Be("Oreder (kg)");
        viewModel.PreviewData.Should().ContainSingle();
        viewModel.ImportStatus.Should().Contain("Ready to import 1 bean");
        viewModel.CanImport.Should().BeTrue();
    }

    [Fact]
    public async Task ImportCommand_ImportsBeans_ShowsSummary_AndNavigatesBack()
    {
        const string filePath = @"C:\imports\beans.csv";
        var csvParserService = new Mock<ICsvParserService>();
        csvParserService.Setup(service => service.GetCsvHeadersAsync(filePath))
            .ReturnsAsync(["Coffee Name", "Country"]);
        csvParserService.Setup(service => service.ReadCsvContentAsync(filePath, 5))
            .ReturnsAsync(
            [
                new Dictionary<string, string>
                {
                    ["Coffee Name"] = "Sidra",
                    ["Country"] = "Ecuador"
                }
            ]);

        var beanDataService = new Mock<IBeanDataService>();
        beanDataService.Setup(service => service.ImportBeansFromCsvAsync(filePath, It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync((1, 0, new List<string>()));

        var navigationService = new Mock<INavigationService>();
        navigationService.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);

        var alertService = new Mock<IAlertService>();
        alertService.Setup(service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var viewModel = CreateViewModel(
            beanDataService: beanDataService,
            csvParserService: csvParserService,
            navigationService: navigationService,
            alertService: alertService);
        viewModel.PickFileAsync = () => Task.FromResult<string?>(filePath);

        await viewModel.BrowseCommand.ExecuteAsync(null);
        await viewModel.ImportCommand.ExecuteAsync(null);

        beanDataService.Verify(
            service => service.ImportBeansFromCsvAsync(
                filePath,
                It.Is<Dictionary<string, string>>(mapping =>
                    mapping["CoffeeName"] == "Coffee Name" &&
                    mapping["Country"] == "Country")),
            Times.Once);
        alertService.Verify(
            service => service.ShowAlertAsync(
                "Import Complete",
                It.Is<string>(message => message.Contains("Imported 1 beans")),
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

    private static BeanImportPageViewModel CreateViewModel(
        Mock<IBeanDataService>? beanDataService = null,
        Mock<ICsvParserService>? csvParserService = null,
        Mock<INavigationService>? navigationService = null,
        Mock<IAlertService>? alertService = null)
    {
        beanDataService ??= new Mock<IBeanDataService>();
        csvParserService ??= new Mock<ICsvParserService>();
        navigationService ??= new Mock<INavigationService>();
        alertService ??= new Mock<IAlertService>();

        navigationService.Setup(service => service.GoBackAsync()).Returns(Task.CompletedTask);
        alertService.Setup(service => service.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return new BeanImportPageViewModel(
            beanDataService.Object,
            csvParserService.Object,
            navigationService.Object,
            alertService.Object);
    }
}
