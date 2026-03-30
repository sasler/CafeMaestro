using CafeMaestro.Models;
using FluentAssertions;

namespace CafeMaestro.Tests;

public class ModelValidationTests
{
    [Fact]
    public void BeanData_Validate_ReturnsNoErrors_ForValidModel()
    {
        var bean = new BeanData
        {
            Country = "Colombia",
            CoffeeName = "Pink Bourbon",
            Quantity = 5,
            RemainingQuantity = 2.5,
            Price = 18.75m
        };

        var errors = bean.Validate();

        errors.Should().BeEmpty();
        bean.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-5)]
    public void BeanData_Validate_ReturnsError_WhenQuantityIsNegative(double quantity)
    {
        var bean = new BeanData
        {
            Country = "Kenya",
            CoffeeName = "AA",
            Quantity = quantity,
            RemainingQuantity = 0
        };

        bean.Validate().Should().Contain(error => error.Contains("Quantity"));
        bean.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-0.1, 1)]
    [InlineData(2, 1)]
    public void BeanData_Validate_ReturnsError_WhenRemainingQuantityIsOutOfBounds(double remainingQuantity, double quantity)
    {
        var bean = new BeanData
        {
            Country = "Brazil",
            CoffeeName = "Santos",
            Quantity = quantity,
            RemainingQuantity = remainingQuantity
        };

        bean.Validate().Should().Contain(error => error.Contains("RemainingQuantity"));
        bean.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BeanData_Validate_ReturnsError_WhenPriceIsNegative()
    {
        var bean = new BeanData
        {
            Country = "Ethiopia",
            CoffeeName = "Heirloom",
            Quantity = 1,
            RemainingQuantity = 1,
            Price = -0.01m
        };

        bean.Validate().Should().Contain(error => error.Contains("Price"));
        bean.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BeanData_Validate_ReturnsErrors_WhenRequiredNamesAreNull()
    {
        var bean = new BeanData
        {
            Country = null!,
            CoffeeName = null!,
            Quantity = 1,
            RemainingQuantity = 1
        };

        var errors = bean.Validate();

        errors.Should().Contain(error => error.Contains("Country"));
        errors.Should().Contain(error => error.Contains("offee name"));
        bean.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BeanData_Validate_ReturnsErrors_WhenRequiredNamesAreEmptyOrWhitespace()
    {
        var bean = new BeanData
        {
            Country = "  ",
            CoffeeName = "",
            Quantity = 1,
            RemainingQuantity = 1
        };

        var errors = bean.Validate();

        errors.Should().Contain(error => error.Contains("Country"));
        errors.Should().Contain(error => error.Contains("offee name"));
        bean.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BeanData_UseQuantity_ReturnsTrue_AndDeductsAmount_WhenEnoughStockExists()
    {
        var bean = new BeanData
        {
            Country = "Guatemala",
            CoffeeName = "Caturra",
            Quantity = 3,
            RemainingQuantity = 3
        };

        var result = bean.UseQuantity(1.25);

        result.Should().BeTrue();
        bean.RemainingQuantity.Should().Be(1.75);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(3.1)]
    public void BeanData_UseQuantity_ReturnsFalse_AndDoesNotChangeRemainingQuantity_WhenAmountIsInvalid(double amount)
    {
        var bean = new BeanData
        {
            Country = "Rwanda",
            CoffeeName = "Bourbon",
            Quantity = 3,
            RemainingQuantity = 3
        };

        var result = bean.UseQuantity(amount);

        result.Should().BeFalse();
        bean.RemainingQuantity.Should().Be(3);
    }

    [Fact]
    public void RoastData_Validate_ReturnsNoErrors_ForValidModel()
    {
        var roast = new RoastData
        {
            BeanType = "Ethiopian Natural",
            Temperature = 205,
            BatchWeight = 250,
            FinalWeight = 210,
            RoastMinutes = 10,
            RoastSeconds = 30,
            FirstCrackMinutes = 8,
            FirstCrackSeconds = 45
        };

        var errors = roast.Validate();

        errors.Should().BeEmpty();
        roast.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RoastData_Validate_ReturnsError_WhenBeanTypeIsEmpty()
    {
        var roast = new RoastData
        {
            BeanType = "",
            Temperature = 210,
            BatchWeight = 200,
            FinalWeight = 170,
            RoastMinutes = 9,
            RoastSeconds = 30
        };

        roast.Validate().Should().Contain(error => error.Contains("BeanType"));
        roast.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RoastData_Validate_ReturnsError_WhenBatchWeightIsNotPositive(double batchWeight)
    {
        var roast = new RoastData
        {
            BeanType = "Washed Colombia",
            Temperature = 215,
            BatchWeight = batchWeight,
            FinalWeight = 0,
            RoastMinutes = 11,
            RoastSeconds = 0
        };

        roast.Validate().Should().Contain(error => error.Contains("BatchWeight"));
        roast.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-0.1, 200)]
    [InlineData(210, 200)]
    public void RoastData_Validate_ReturnsError_WhenFinalWeightIsOutOfBounds(double finalWeight, double batchWeight)
    {
        var roast = new RoastData
        {
            BeanType = "Costa Rica",
            Temperature = 220,
            BatchWeight = batchWeight,
            FinalWeight = finalWeight,
            RoastMinutes = 12,
            RoastSeconds = 15
        };

        var errors = roast.Validate();

        errors.Should().Contain(error => error.Contains("FinalWeight"));
        roast.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(501)]
    public void RoastData_Validate_ReturnsError_WhenTemperatureIsOutOfBounds(double temperature)
    {
        var roast = new RoastData
        {
            BeanType = "Peru",
            Temperature = temperature,
            BatchWeight = 180,
            FinalWeight = 150,
            RoastMinutes = 10,
            RoastSeconds = 10
        };

        roast.Validate().Should().Contain(error => error.Contains("Temperature"));
        roast.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RoastData_Validate_ReturnsError_WhenRoastMinutesAreNegative()
    {
        var roast = new RoastData
        {
            BeanType = "Sumatra",
            Temperature = 200,
            BatchWeight = 180,
            FinalWeight = 150,
            RoastMinutes = -1,
            RoastSeconds = 30
        };

        roast.Validate().Should().Contain(error => error.Contains("RoastMinutes"));
        roast.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(60)]
    public void RoastData_Validate_ReturnsError_WhenRoastSecondsAreOutOfRange(int roastSeconds)
    {
        var roast = new RoastData
        {
            BeanType = "Panama",
            Temperature = 202,
            BatchWeight = 200,
            FinalWeight = 168,
            RoastMinutes = 10,
            RoastSeconds = roastSeconds
        };

        roast.Validate().Should().Contain(error => error.Contains("RoastSeconds"));
        roast.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(10, 31, 10, 30)]
    [InlineData(11, 0, 10, 30)]
    public void RoastData_Validate_ReturnsError_WhenFirstCrackOccursAfterRoastEnds(
        int firstCrackMinutes,
        int firstCrackSeconds,
        int roastMinutes,
        int roastSeconds)
    {
        var roast = new RoastData
        {
            BeanType = "Nicaragua",
            Temperature = 208,
            BatchWeight = 220,
            FinalWeight = 188,
            RoastMinutes = roastMinutes,
            RoastSeconds = roastSeconds,
            FirstCrackMinutes = firstCrackMinutes,
            FirstCrackSeconds = firstCrackSeconds
        };

        roast.Validate().Should().Contain(error => error.Contains("First crack"));
        roast.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, 30)]
    [InlineData(1, -1)]
    [InlineData(1, 60)]
    public void RoastData_Validate_ReturnsError_WhenFirstCrackComponentsAreInvalid(int firstCrackMinutes, int firstCrackSeconds)
    {
        var roast = new RoastData
        {
            BeanType = "Burundi",
            Temperature = 210,
            BatchWeight = 220,
            FinalWeight = 190,
            RoastMinutes = 9,
            RoastSeconds = 30,
            FirstCrackMinutes = firstCrackMinutes,
            FirstCrackSeconds = firstCrackSeconds
        };

        roast.Validate().Should().Contain(error => error.Contains("First crack"));
        roast.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RoastData_Validate_ReturnsError_WhenOnlyOneFirstCrackComponentIsSet()
    {
        var roast = new RoastData
        {
            BeanType = "El Salvador",
            Temperature = 203,
            BatchWeight = 200,
            FinalWeight = 172,
            RoastMinutes = 11,
            RoastSeconds = 15,
            FirstCrackMinutes = 8
        };

        roast.Validate().Should().Contain(error => error.Contains("First crack"));
        roast.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RoastLevelData_Validate_ReturnsNoErrors_ForValidModel()
    {
        var roastLevel = new RoastLevelData
        {
            Name = "City",
            MinWeightLossPercentage = 12,
            MaxWeightLossPercentage = 15
        };

        var errors = roastLevel.Validate();

        errors.Should().BeEmpty();
        roastLevel.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RoastLevelData_Validate_ReturnsError_WhenNameIsEmpty()
    {
        var roastLevel = new RoastLevelData
        {
            Name = "",
            MinWeightLossPercentage = 10,
            MaxWeightLossPercentage = 12
        };

        roastLevel.Validate().Should().Contain(error => error.Contains("Name"));
        roastLevel.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-0.1, 10)]
    [InlineData(20, 10)]
    [InlineData(10, 100.1)]
    public void RoastLevelData_Validate_ReturnsError_WhenWeightLossBoundsAreInvalid(double min, double max)
    {
        var roastLevel = new RoastLevelData
        {
            Name = "Full City",
            MinWeightLossPercentage = min,
            MaxWeightLossPercentage = max
        };

        var errors = roastLevel.Validate();

        errors.Should().Contain(error => error.Contains("WeightLoss") || error.Contains("MinWeightLossPercentage") || error.Contains("MaxWeightLossPercentage"));
        roastLevel.IsValid.Should().BeFalse();
    }
}
