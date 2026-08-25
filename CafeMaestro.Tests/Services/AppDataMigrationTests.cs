using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;

namespace CafeMaestro.Tests.Services;

public sealed class AppDataMigrationTests
{
    private readonly V1ToV2AppDataMigration _migration = new();

    [Fact]
    public void Migrate_CompletedLegacyRoast_PreservesHistoryAndAddsWorkflowFields()
    {
        Guid roastId = Guid.NewGuid();
        var roastDate = new DateTime(2025, 6, 1, 14, 30, 0, DateTimeKind.Utc);
        var data = CreateVersionOneData(
            new RoastData
            {
                Id = roastId,
                BeanType = "Ethiopia - Guji (Heirloom)",
                Temperature = 210,
                BatchWeight = 250,
                FinalWeight = 210,
                RoastMinutes = 10,
                RoastSeconds = 42,
                RoastDate = roastDate,
                Notes = "Floral",
                RoastLevelName = "Medium-Light",
                FirstCrackMinutes = 8,
                FirstCrackSeconds = 15
            });

        _migration.Migrate(data);

        data.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
        data.ActiveRoastSession.Should().BeNull();
        RoastData migrated = data.RoastLogs.Single();
        migrated.Id.Should().Be(roastId);
        migrated.BeanDisplaySnapshot.Should().Be("Ethiopia - Guji (Heirloom)");
        migrated.CompletionStatus.Should().Be(RoastCompletionStatus.Complete);
        migrated.FinalWeight.Should().Be(210);
        migrated.DroppedAtUtc.Should().Be(new DateTimeOffset(roastDate));
        migrated.RoastMinutes.Should().Be(10);
        migrated.RoastSeconds.Should().Be(42);
        migrated.Notes.Should().Be("Floral");
        migrated.RoastLevelName.Should().Be("Medium-Light");
        migrated.FirstCrackMinutes.Should().Be(8);
        migrated.FirstCrackSeconds.Should().Be(15);
    }

    [Fact]
    public void Migrate_ZeroWeightLegacyRoast_BecomesImmediatelyReadyAwaitingWeight()
    {
        var roastDate = new DateTime(2025, 3, 2, 9, 45, 0, DateTimeKind.Utc);
        var data = CreateVersionOneData(CreateLegacyRoast("Kenya", roastDate, finalWeight: 0));

        _migration.Migrate(data);

        RoastData migrated = data.RoastLogs.Single();
        migrated.CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
        migrated.FinalWeight.Should().BeNull();
        migrated.DroppedAtUtc.Should().Be(new DateTimeOffset(roastDate));
        migrated.CoolingDurationSeconds.Should().Be(0);
        migrated.ReadyToWeighAtUtc.Should().Be(migrated.DroppedAtUtc);
        migrated.RoastLevelName.Should().BeEmpty();
    }

    [Fact]
    public void Migrate_NegativeLegacyWeight_PreservesInvalidValueForValidation()
    {
        var data = CreateVersionOneData(
            CreateLegacyRoast("Invalid", DateTime.UtcNow, finalWeight: -1));

        _migration.Migrate(data);
        AppDataNormalizer.Normalize(data, allowLegacyRepairs: true);

        data.RoastLogs.Single().FinalWeight.Should().Be(-1);
        AppDataNormalizer.GetValidationErrors(data)
            .Should().Contain(error => error.Contains("FinalWeight", StringComparison.Ordinal));
    }

    [Fact]
    public void Migrate_UniqueExactBeanName_LinksBeanId()
    {
        BeanData bean = CreateBean("Colombia", "Pink Bourbon", "Washed");
        var data = CreateVersionOneData(CreateLegacyRoast(bean.DisplayName, DateTime.UtcNow, 180));
        data.Beans.Add(bean);

        _migration.Migrate(data);

        data.RoastLogs.Single().BeanId.Should().Be(bean.Id);
    }

    [Fact]
    public void Migrate_AmbiguousBeanName_DoesNotGuessBeanId()
    {
        BeanData first = CreateBean("Brazil", "Santos", "Bourbon");
        BeanData second = CreateBean("Brazil", "Santos", "Bourbon");
        var data = CreateVersionOneData(CreateLegacyRoast(first.DisplayName, DateTime.UtcNow, 180));
        data.Beans.AddRange([first, second]);

        _migration.Migrate(data);

        data.RoastLogs.Single().BeanId.Should().BeNull();
    }

    [Fact]
    public void Migrate_RenamedBean_PreservesSnapshotWithoutLinkingToWrongBean()
    {
        BeanData renamedBean = CreateBean("Rwanda", "New name", "Bourbon");
        const string historicalName = "Rwanda - Old name (Bourbon)";
        var data = CreateVersionOneData(CreateLegacyRoast(historicalName, DateTime.UtcNow, 180));
        data.Beans.Add(renamedBean);

        _migration.Migrate(data);

        RoastData migrated = data.RoastLogs.Single();
        migrated.BeanDisplaySnapshot.Should().Be(historicalName);
        migrated.BeanId.Should().BeNull();
    }

    [Fact]
    public void Migrate_MissingCollections_NormalizesThemBeforeUse()
    {
        var data = new AppData
        {
            DataSchemaVersion = AppDataSchema.LegacyVersion,
            Beans = null!,
            RoastLogs = null!,
            RoastLevels = null!
        };

        _migration.Migrate(data);

        data.Beans.Should().NotBeNull().And.BeEmpty();
        data.RoastLogs.Should().NotBeNull().And.BeEmpty();
        data.RoastLevels.Should().NotBeNull();
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Migrate_RoastDate_UsesExplicitUtcRuleForEveryDateTimeKind(DateTimeKind kind)
    {
        DateTime roastDate = DateTime.SpecifyKind(new DateTime(2025, 2, 10, 13, 25, 0), kind);
        var data = CreateVersionOneData(CreateLegacyRoast("Peru", roastDate, 150));

        _migration.Migrate(data);

        DateTime expectedUtc = kind == DateTimeKind.Utc
            ? roastDate
            : DateTime.SpecifyKind(roastDate, DateTimeKind.Local).ToUniversalTime();
        data.RoastLogs.Single().DroppedAtUtc.Should().Be(new DateTimeOffset(expectedUtc));
    }

    [Fact]
    public void Pipeline_ExecutesMigrationsSequentiallyBySourceVersion()
    {
        var executionOrder = new List<int>();
        var data = new AppData { DataSchemaVersion = 0 };
        var pipeline = new AppDataMigrationPipeline(
        [
            new RecordingMigration(1, 2, executionOrder),
            new RecordingMigration(0, 1, executionOrder)
        ]);

        bool migrated = pipeline.MigrateToCurrent(data);

        migrated.Should().BeTrue();
        executionOrder.Should().Equal(0, 1);
        data.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
    }

    private static AppData CreateVersionOneData(RoastData roast)
    {
        return new AppData
        {
            DataSchemaVersion = AppDataSchema.LegacyVersion,
            Beans = [],
            RoastLogs = [roast],
            RoastLevels = [new RoastLevelData("All", 0, 100)]
        };
    }

    private static RoastData CreateLegacyRoast(string beanType, DateTime roastDate, double finalWeight)
    {
        return new RoastData
        {
            BeanType = beanType,
            Temperature = 205,
            BatchWeight = 200,
            FinalWeight = finalWeight,
            RoastMinutes = 10,
            RoastSeconds = 0,
            RoastDate = roastDate
        };
    }

    private static BeanData CreateBean(string country, string coffeeName, string variety)
    {
        return new BeanData
        {
            Country = country,
            CoffeeName = coffeeName,
            Variety = variety,
            Quantity = 1,
            RemainingQuantity = 1
        };
    }

    private sealed class RecordingMigration(
        int sourceVersion,
        int targetVersion,
        ICollection<int> executionOrder) : IAppDataMigration
    {
        public int SourceVersion => sourceVersion;
        public int TargetVersion => targetVersion;

        public void Migrate(AppData data)
        {
            executionOrder.Add(SourceVersion);
            data.DataSchemaVersion = TargetVersion;
        }
    }
}
