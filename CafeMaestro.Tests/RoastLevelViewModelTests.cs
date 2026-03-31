using CafeMaestro.Models;
using CafeMaestro.ViewModels;
using FluentAssertions;

namespace CafeMaestro.Tests;

public class RoastLevelViewModelTests
{
    [Fact]
    public void SettingProperties_RaisesExpectedPropertyChangedEvents()
    {
        var viewModel = new RoastLevelViewModel();
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        viewModel.Name = "City";
        viewModel.MinWeightLossPercentage = 13.5;
        viewModel.MaxWeightLossPercentage = 15.5;

        changedProperties.Should().Contain(nameof(RoastLevelViewModel.Name));
        changedProperties.Should().Contain(nameof(RoastLevelViewModel.MinWeightLossPercentage));
        changedProperties.Should().Contain(nameof(RoastLevelViewModel.MaxWeightLossPercentage));
        changedProperties.Count(name => name == nameof(RoastLevelViewModel.DisplayRange)).Should().Be(2);
    }

    [Fact]
    public void FromModel_CreatesMatchingViewModel()
    {
        var id = Guid.NewGuid();
        var model = new RoastLevelData
        {
            Id = id,
            Name = "Full City",
            MinWeightLossPercentage = 14.2,
            MaxWeightLossPercentage = 16.8
        };

        var viewModel = RoastLevelViewModel.FromModel(model);

        viewModel.Id.Should().Be(id);
        viewModel.Name.Should().Be("Full City");
        viewModel.MinWeightLossPercentage.Should().Be(14.2);
        viewModel.MaxWeightLossPercentage.Should().Be(16.8);
        viewModel.DisplayRange.Should().Be("14.2% - 16.8% weight loss");
    }

    [Fact]
    public void ToModel_CreatesMatchingModel()
    {
        var id = Guid.NewGuid();
        var viewModel = new RoastLevelViewModel
        {
            Id = id,
            Name = "French",
            MinWeightLossPercentage = 18.0,
            MaxWeightLossPercentage = 20.5
        };

        var model = viewModel.ToModel();

        model.Id.Should().Be(id);
        model.Name.Should().Be("French");
        model.MinWeightLossPercentage.Should().Be(18.0);
        model.MaxWeightLossPercentage.Should().Be(20.5);
    }

    [Fact]
    public void DisplayRange_UpdatesWhenWeightLossValuesChange()
    {
        var viewModel = new RoastLevelViewModel
        {
            MinWeightLossPercentage = 12.0,
            MaxWeightLossPercentage = 14.0
        };

        viewModel.DisplayRange.Should().Be("12.0% - 14.0% weight loss");

        viewModel.MinWeightLossPercentage = 12.5;
        viewModel.MaxWeightLossPercentage = 14.5;

        viewModel.DisplayRange.Should().Be("12.5% - 14.5% weight loss");
    }
}
