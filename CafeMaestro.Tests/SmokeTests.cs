using CafeMaestro.Models;
using FluentAssertions;

namespace CafeMaestro.Tests;

public class SmokeTests
{
    [Fact]
    public void BeanData_CanBeCreated()
    {
        var bean = new BeanData
        {
            CoffeeName = "Ethiopian Yirgacheffe",
            Country = "Ethiopia",
            Quantity = 500
        };

        bean.CoffeeName.Should().Be("Ethiopian Yirgacheffe");
        bean.Country.Should().Be("Ethiopia");
        bean.Quantity.Should().Be(500);
        bean.RemainingQuantity.Should().Be(0);
    }

    [Fact]
    public void RoastData_CanBeCreated()
    {
        var roast = new RoastData
        {
            BeanType = "Test Bean",
            BatchWeight = 200,
            FinalWeight = 170,
            RoastMinutes = 12,
            RoastSeconds = 30
        };

        roast.BeanType.Should().Be("Test Bean");
        roast.WeightLossPercentage.Should().BeApproximately(15.0, 0.1);
    }

    [Fact]
    public void RoastLevelData_CanBeCreated()
    {
        var level = new RoastLevelData("Medium", 12, 16);

        level.Name.Should().Be("Medium");
        level.MinWeightLossPercentage.Should().Be(12);
        level.MaxWeightLossPercentage.Should().Be(16);
    }
}
