using CafeMaestro.ViewModels.Popups;
using CafeMaestro.Models;
using FluentAssertions;

namespace CafeMaestro.Tests.ViewModels.Popups;

public class WeighInInputValidatorTests
{
    [Theory]
    [InlineData("206.4", 240, true)]
    [InlineData("206.45", 240, false)]
    [InlineData("0", 240, false)]
    [InlineData("241", 240, false)]
    public void Validate_EnforcesTenthGramAndBatchLimit(string input, double batch, bool expected) =>
        WeighInInputValidator.Validate(input, batch).IsValid.Should().Be(expected);

    [Fact]
    public void BatchChoiceOutcome_StartsWithoutImplicitSelection() =>
        BatchChoiceOutcome.Cancelled.Choice.Should().BeNull();
}
