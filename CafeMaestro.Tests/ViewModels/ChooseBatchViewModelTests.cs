using CafeMaestro.Models;
using CafeMaestro.Services;
using CafeMaestro.ViewModels.Popups;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.ViewModels;

public class ChooseBatchViewModelTests
{
    [Fact]
    public void ApplyQueryAttributes_ProjectsEveryBatchUnselectedAndDistinguishable()
    {
        DateTimeOffset dropped = new(2026, 8, 25, 14, 35, 0, TimeSpan.Zero);
        var viewModel = new ChooseBatchViewModel(Mock.Of<IOverlayService>());

        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [ChooseBatchViewModel.ChoicesKey] = new List<BatchChoice>
            {
                Choice("Ethiopia - Guji", 1, 240, dropped),
                Choice("Ethiopia - Guji", 2, 235, dropped.AddMinutes(12))
            }
        });

        viewModel.Options.Should().HaveCount(2);
        viewModel.Options.Should().OnlyContain(option => !option.IsSelected);
        viewModel.SelectedChoice.Should().BeNull();
        viewModel.CanContinue.Should().BeFalse();
        viewModel.Options.Select(option => option.BatchDisplay).Should().Equal("B1", "B2");
        viewModel.Options.Select(option => option.DetailDisplay).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SelectCommand_KeepsExactlyOneBatchSelectedAndUnblocksContinue()
    {
        DateTimeOffset dropped = new(2026, 8, 25, 14, 35, 0, TimeSpan.Zero);
        BatchChoice second = Choice("Ethiopia - Guji", 2, 235, dropped.AddMinutes(12));
        var viewModel = new ChooseBatchViewModel(Mock.Of<IOverlayService>());
        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [ChooseBatchViewModel.ChoicesKey] = new List<BatchChoice>
            {
                Choice("Ethiopia - Guji", 1, 240, dropped),
                second
            }
        });

        viewModel.SelectCommand.Execute(viewModel.Options[0]);
        viewModel.SelectCommand.Execute(viewModel.Options[1]);

        viewModel.Options[0].IsSelected.Should().BeFalse();
        viewModel.Options[1].IsSelected.Should().BeTrue();
        viewModel.Options[1].SelectionDisplay.Should().Be("SELECTED");
        viewModel.SelectedChoice.Should().Be(second);
        viewModel.CanContinue.Should().BeTrue();
    }

    private static BatchChoice Choice(string bean, int batch, double weight, DateTimeOffset dropped) => new()
    {
        RoastId = Guid.NewGuid(),
        BatchNumber = batch,
        BeanDisplaySnapshot = bean,
        BatchWeight = weight,
        DroppedAtUtc = dropped,
        TotalSeconds = 644
    };
}
