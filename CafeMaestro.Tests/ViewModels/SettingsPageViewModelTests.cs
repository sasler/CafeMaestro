using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class SettingsPageViewModelTests
{
    [Fact]
    public async Task OnAppearingAsync_LoadsFilePathThemeAndRoastLevels()
    {
        var appDataService = CreateAppDataServiceMock();
        var preferencesService = CreatePreferencesServiceMock();
        var roastDataService = new Mock<IRoastDataService>();
        var roastLevelService = CreateRoastLevelServiceMock(
            [
                new RoastLevelData("Dark", 16.0, 18.0),
                new RoastLevelData("Light", 11.0, 13.0)
            ]);

        var viewModel = CreateViewModel(
            appDataService.Object,
            preferencesService.Object,
            roastDataService.Object,
            roastLevelService.Object);

        await viewModel.OnAppearingAsync();

        viewModel.DataFilePath.Should().Be(@"C:\data\cafemaestro.json");
        viewModel.SelectedThemeIndex.Should().Be(2);
        viewModel.RoastLevels.Select(level => level.Name).Should().ContainInOrder("Light", "Dark");
    }

    [Fact]
    public async Task UseExistingDataFileAsync_SavesPreferenceAndUpdatesPath()
    {
        var appDataService = CreateAppDataServiceMock();
        var preferencesService = CreatePreferencesServiceMock();
        var roastDataService = new Mock<IRoastDataService>();
        var roastLevelService = CreateRoastLevelServiceMock([]);
        var viewModel = CreateViewModel(
            appDataService.Object,
            preferencesService.Object,
            roastDataService.Object,
            roastLevelService.Object);

        await viewModel.UseExistingDataFileAsync(@"D:\custom\cafemaestro.json");

        appDataService.Verify(service => service.SetCustomFilePathAsync(@"D:\custom\cafemaestro.json"), Times.Once);
        preferencesService.Verify(service => service.SaveAppDataFilePathAsync(@"D:\custom\cafemaestro.json"), Times.Once);
        preferencesService.Verify(service => service.SetFirstRunCompletedAsync(), Times.Once);
    }

    [Fact]
    public async Task UseDefaultDataFileAsync_UsesDefaultPathAndMarksFirstRunComplete()
    {
        var appDataService = CreateAppDataServiceMock();
        var preferencesService = CreatePreferencesServiceMock();
        var roastDataService = new Mock<IRoastDataService>();
        var roastLevelService = CreateRoastLevelServiceMock([]);
        var viewModel = CreateViewModel(
            appDataService.Object,
            preferencesService.Object,
            roastDataService.Object,
            roastLevelService.Object);

        await viewModel.UseDefaultDataFileAsync();

        appDataService.Verify(service => service.ResetToDefaultPathAsync(), Times.Once);
        preferencesService.Verify(service => service.SaveAppDataFilePathAsync(@"C:\Users\sasch\Documents\CafeMaestro\cafemaestro_data.json"), Times.Once);
        preferencesService.Verify(service => service.SetFirstRunCompletedAsync(), Times.Once);
    }

    [Fact]
    public async Task SaveRoastLevelCommand_AddsNewRoastLevel()
    {
        var levels = new List<RoastLevelData>
        {
            new("Light", 11.0, 13.0)
        };

        var appDataService = CreateAppDataServiceMock();
        var preferencesService = CreatePreferencesServiceMock();
        var roastDataService = new Mock<IRoastDataService>();
        var roastLevelService = CreateRoastLevelServiceMock(levels);
        var viewModel = CreateViewModel(
            appDataService.Object,
            preferencesService.Object,
            roastDataService.Object,
            roastLevelService.Object);

        await viewModel.OnAppearingAsync();
        viewModel.AddRoastLevelCommand.Execute(null);
        viewModel.RoastLevelName = "City";
        viewModel.MinWeightLossText = "13.0";
        viewModel.MaxWeightLossText = "15.0";

        await viewModel.SaveRoastLevelCommand.ExecuteAsync(null);

        levels.Should().ContainSingle(level =>
            level.Name == "City" &&
            level.MinWeightLossPercentage == 13.0 &&
            level.MaxWeightLossPercentage == 15.0);
        viewModel.RoastLevels.Select(level => level.Name).Should().Contain("City");
    }

    [Fact]
    public async Task SaveRoastLevelCommand_UpdatesExistingRoastLevel()
    {
        var roastLevelId = Guid.NewGuid();
        var levels = new List<RoastLevelData>
        {
            new()
            {
                Id = roastLevelId,
                Name = "City",
                MinWeightLossPercentage = 13.0,
                MaxWeightLossPercentage = 15.0
            }
        };

        var appDataService = CreateAppDataServiceMock();
        var preferencesService = CreatePreferencesServiceMock();
        var roastDataService = new Mock<IRoastDataService>();
        var roastLevelService = CreateRoastLevelServiceMock(levels);
        var viewModel = CreateViewModel(
            appDataService.Object,
            preferencesService.Object,
            roastDataService.Object,
            roastLevelService.Object);

        await viewModel.OnAppearingAsync();
        viewModel.EditRoastLevelCommand.Execute(viewModel.RoastLevels.Single());
        viewModel.RoastLevelName = "Full City";
        viewModel.MinWeightLossText = "14.0";
        viewModel.MaxWeightLossText = "16.0";

        await viewModel.SaveRoastLevelCommand.ExecuteAsync(null);

        levels.Should().ContainSingle(level =>
            level.Id == roastLevelId &&
            level.Name == "Full City" &&
            level.MinWeightLossPercentage == 14.0 &&
            level.MaxWeightLossPercentage == 16.0);
    }

    [Fact]
    public async Task DeleteRoastLevelCoreAsync_RemovesRoastLevel()
    {
        var roastLevelId = Guid.NewGuid();
        var levels = new List<RoastLevelData>
        {
            new()
            {
                Id = roastLevelId,
                Name = "Dark",
                MinWeightLossPercentage = 16.0,
                MaxWeightLossPercentage = 18.0
            }
        };

        var appDataService = CreateAppDataServiceMock();
        var preferencesService = CreatePreferencesServiceMock();
        var roastDataService = new Mock<IRoastDataService>();
        var roastLevelService = CreateRoastLevelServiceMock(levels);
        var viewModel = CreateViewModel(
            appDataService.Object,
            preferencesService.Object,
            roastDataService.Object,
            roastLevelService.Object);

        await viewModel.OnAppearingAsync();

        bool success = await viewModel.DeleteRoastLevelCoreAsync(viewModel.RoastLevels.Single());

        success.Should().BeTrue();
        levels.Should().BeEmpty();
        viewModel.RoastLevels.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetRoastLevelsToDefaultsCoreAsync_RestoresDefaultLevels()
    {
        var levels = new List<RoastLevelData>
        {
            new("Custom", 10.0, 12.0)
        };

        var appDataService = CreateAppDataServiceMock();
        var preferencesService = CreatePreferencesServiceMock();
        var roastDataService = new Mock<IRoastDataService>();
        var roastLevelService = CreateRoastLevelServiceMock(levels);
        var viewModel = CreateViewModel(
            appDataService.Object,
            preferencesService.Object,
            roastDataService.Object,
            roastLevelService.Object);

        await viewModel.OnAppearingAsync();

        bool success = await viewModel.ResetRoastLevelsToDefaultsCoreAsync();

        success.Should().BeTrue();
        viewModel.RoastLevels.Should().HaveCount(7);
        viewModel.RoastLevels.First().Name.Should().Be("Under Developed");
        viewModel.RoastLevels.Last().Name.Should().Be("Burned");
    }

    private static SettingsPageViewModel CreateViewModel(
        IAppDataService appDataService,
        IPreferencesService preferencesService,
        IRoastDataService roastDataService,
        IRoastLevelService roastLevelService)
    {
        return new SettingsPageViewModel(
            preferencesService,
            appDataService,
            roastDataService,
            roastLevelService,
            Mock.Of<IFileSaver>(),
            Mock.Of<IFolderPicker>(),
            Mock.Of<INavigationService>(),
            new WeakReferenceMessenger());
    }

    private static Mock<IAppDataService> CreateAppDataServiceMock()
    {
        var appDataService = new Mock<IAppDataService>();
        appDataService.SetupGet(service => service.DataFilePath).Returns(@"C:\data\cafemaestro.json");
        appDataService.Setup(service => service.SetCustomFilePathAsync(It.IsAny<string>()))
            .ReturnsAsync(new AppData());
        appDataService.Setup(service => service.ResetToDefaultPathAsync())
            .ReturnsAsync(new AppData())
            .Callback(() => appDataService.SetupGet(service => service.DataFilePath)
                .Returns(@"C:\Users\sasch\Documents\CafeMaestro\cafemaestro_data.json"));
        return appDataService;
    }

    private static Mock<IPreferencesService> CreatePreferencesServiceMock()
    {
        var preferencesService = new Mock<IPreferencesService>();
        preferencesService.Setup(service => service.GetThemePreferenceAsync()).ReturnsAsync(ThemePreference.Dark);
        preferencesService.Setup(service => service.IsFirstRunAsync()).ReturnsAsync(false);
        return preferencesService;
    }

    private static Mock<IRoastLevelService> CreateRoastLevelServiceMock(List<RoastLevelData> levels)
    {
        var roastLevelService = new Mock<IRoastLevelService>();
        roastLevelService.Setup(service => service.GetRoastLevelsAsync())
            .ReturnsAsync(() => levels.OrderBy(level => level.MinWeightLossPercentage).ToList());
        roastLevelService.Setup(service => service.AddRoastLevelAsync(It.IsAny<RoastLevelData>()))
            .ReturnsAsync((RoastLevelData level) =>
            {
                levels.Add(level);
                return true;
            });
        roastLevelService.Setup(service => service.UpdateRoastLevelAsync(It.IsAny<RoastLevelData>()))
            .ReturnsAsync((RoastLevelData updatedLevel) =>
            {
                var existingLevel = levels.Single(level => level.Id == updatedLevel.Id);
                existingLevel.Name = updatedLevel.Name;
                existingLevel.MinWeightLossPercentage = updatedLevel.MinWeightLossPercentage;
                existingLevel.MaxWeightLossPercentage = updatedLevel.MaxWeightLossPercentage;
                return true;
            });
        roastLevelService.Setup(service => service.DeleteRoastLevelAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
            {
                levels.RemoveAll(level => level.Id == id);
                return true;
            });
        roastLevelService.Setup(service => service.SaveRoastLevelsAsync(It.IsAny<List<RoastLevelData>>()))
            .ReturnsAsync((List<RoastLevelData> updatedLevels) =>
            {
                levels.Clear();
                levels.AddRange(updatedLevels);
                return true;
            });
        return roastLevelService;
    }
}
