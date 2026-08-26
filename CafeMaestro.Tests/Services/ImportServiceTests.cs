using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class ImportServiceTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public ImportServiceTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    // ------------------------------------------------------------------ auto-mapping

    [Fact]
    public void SuggestMappings_ForBeans_MatchesTheHeadersRealExportsUse()
    {
        IImportService service = CreateService(new AppData());

        IReadOnlyDictionary<string, string> mappings = service.SuggestMappings(
            ImportKind.Beans,
            ["Coffee", "Origin", "Variaty", "Oreder (kg)"]);

        mappings["CoffeeName"].Should().Be("Coffee");
        mappings["Country"].Should().Be("Origin");
        mappings["Variety"].Should().Be("Variaty");
        mappings["Quantity"].Should().Be("Oreder (kg)");
    }

    [Fact]
    public void SuggestMappings_ForRoasts_MatchesTheHeadersRealExportsUse()
    {
        IImportService service = CreateService(new AppData());

        IReadOnlyDictionary<string, string> mappings = service.SuggestMappings(
            ImportKind.Roasts,
            ["Roast Date", "Coffee Bean", "Time", "Batch Weight"]);

        mappings["RoastDate"].Should().Be("Roast Date");
        mappings["BeanType"].Should().Be("Coffee Bean");
        mappings["RoastTime"].Should().Be("Time");
        mappings["BatchWeight"].Should().Be("Batch Weight");
    }

    [Fact]
    public void SuggestMappings_LeavesFieldsWithNoPlausibleHeaderUnmapped()
    {
        IImportService service = CreateService(new AppData());

        IReadOnlyDictionary<string, string> mappings = service.SuggestMappings(
            ImportKind.Roasts,
            ["Date", "Coffee Bean", "Batch Weight"]);

        // Loss percentage has no column here; guessing one would silently rewrite final weights.
        mappings.Should().NotContainKey("WeightLoss");
        mappings.Should().NotContainKey("Notes");
    }

    // ------------------------------------------------------------------ bean rows

    [Fact]
    public async Task BuildPlanAsync_ForBeans_AcceptsValidRowsAndReportsEveryRejection()
    {
        IImportService service = CreateService(new AppData());

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Beans,
            [
                Row(("Name", "Yirgacheffe"), ("Country", "Ethiopia"), ("Qty", "1.5")),
                Row(("Name", ""), ("Country", "Ethiopia"), ("Qty", "1")),
                Row(("Name", "Huila"), ("Country", ""), ("Qty", "1")),
                Row(("Name", "Nyeri"), ("Country", "Kenya"), ("Qty", "not-a-number"))
            ],
            BeanMappings);

        plan.AcceptedRows.Should().ContainSingle();
        plan.RejectedRows.Should().HaveCount(3);
        plan.RejectedRows[0].Detail.Should().Contain("Coffee name is required");
        plan.RejectedRows[1].Detail.Should().Contain("Country is required");
        plan.RejectedRows[2].Detail.Should().Contain("not a number");
        plan.RejectedRows.Select(row => row.RowNumber).Should().Equal(2, 3, 4);
    }

    [Fact]
    public async Task BuildPlanAsync_ForBeans_RejectsDuplicatesOfExistingAndOfEarlierRows()
    {
        var existing = new AppData
        {
            Beans =
            [
                new BeanData { CoffeeName = "Yirgacheffe", Country = "Ethiopia", Variety = "Heirloom", Quantity = 1, RemainingQuantity = 1 }
            ]
        };
        IImportService service = CreateService(existing);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Beans,
            [
                Row(("Name", "yirgacheffe"), ("Country", "ethiopia"), ("Variety", "heirloom")),
                Row(("Name", "Huila"), ("Country", "Colombia"), ("Variety", "Caturra")),
                Row(("Name", "Huila"), ("Country", "Colombia"), ("Variety", "Caturra"))
            ],
            BeanMappingsWithVariety);

        plan.AcceptedRows.Should().ContainSingle().Which.RowNumber.Should().Be(2);
        plan.RejectedRows.Should().HaveCount(2);
        plan.RejectedRows.Should().OnlyContain(row => row.Detail.Contains("already in the inventory"));
    }

    [Fact]
    public async Task BuildPlanAsync_ForBeans_ParsesQuantityAndDateInvariantlyAndDefaultsMissingQuantity()
    {
        var target = new AppData();
        (IImportService service, Mock<IAppDataService> appData) = CreateServiceWithMock(target);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Beans,
            [
                Row(("Name", "Sidra"), ("Country", "Ecuador"), ("Qty", "2.75"), ("Purchased", "2026-03-01")),
                Row(("Name", "Gesha"), ("Country", "Panama"), ("Qty", ""))
            ],
            BeanMappingsWithDate);

        plan.AcceptedRows.Should().HaveCount(2);
        await service.CommitAsync(plan);
        appData.Verify(data => data.UpdateAsync(It.IsAny<Action<AppData>>(), It.IsAny<CancellationToken>()), Times.Once);

        BeanData sidra = target.Beans.Single(bean => bean.CoffeeName == "Sidra");
        sidra.Quantity.Should().Be(2.75);
        sidra.RemainingQuantity.Should().Be(2.75);
        sidra.PurchaseDate.Should().Be(new DateTime(2026, 3, 1));
        target.Beans.Single(bean => bean.CoffeeName == "Gesha").Quantity.Should().Be(1);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task BuildPlanAsync_ForBeans_RejectsNonFiniteQuantities(string quantity)
    {
        // A named floating-point value parses but cannot be serialized, so accepting one would
        // fail the whole atomic commit and take every valid row down with it.
        var target = new AppData();
        (IImportService service, Mock<IAppDataService> appData) = CreateServiceWithMock(target);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Beans,
            [
                Row(("Name", "Nyeri"), ("Country", "Kenya"), ("Qty", quantity)),
                Row(("Name", "Huila"), ("Country", "Colombia"), ("Qty", "1"))
            ],
            BeanMappings);
        ImportCommitResult result = await service.CommitAsync(plan);

        plan.RejectedRows.Should().ContainSingle()
            .Which.Detail.Should().Contain("not a number");
        result.Succeeded.Should().BeTrue();
        result.Imported.Should().Be(1);
        target.Beans.Should().ContainSingle().Which.Quantity.Should().Be(1);
        target.Beans.Should().OnlyContain(bean => double.IsFinite(bean.Quantity));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public async Task BuildPlanAsync_ForRoasts_RejectsNonFiniteWeights(string weight)
    {
        IImportService service = CreateService(new AppData());

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", weight))],
            RoastMappings);

        plan.AcceptedRows.Should().BeEmpty();
        plan.RejectedRows.Should().ContainSingle()
            .Which.Detail.Should().Contain("not a weight above zero");
    }

    // ------------------------------------------------------------------ roast rows

    [Fact]
    public async Task BuildPlanAsync_ForRoasts_RejectsMissingAndUnparsableBatchWeights()
    {
        IImportService service = CreateService(new AppData());

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [
                Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "220"), ("Time", "11:30")),
                Row(("Date", "2026-03-02"), ("Bean", "Kenya AA"), ("Batch", ""), ("Time", "11:30")),
                Row(("Date", "2026-03-03"), ("Bean", "Kenya AA"), ("Batch", "not-a-weight"), ("Time", "11:30"))
            ],
            RoastMappings);

        plan.AcceptedRows.Should().ContainSingle();
        plan.RejectedRows.Should().HaveCount(2);
        plan.RejectedRows[0].Detail.Should().Contain("Batch weight is required");
        plan.RejectedRows[1].Detail.Should().Contain("not a weight above zero");
    }

    [Fact]
    public async Task BuildPlanAsync_ForRoasts_ReadsTimeAsMinutesAndSecondsNotHours()
    {
        var target = new AppData();
        IImportService service = CreateService(target);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "220 g"), ("Time", "11:30"))],
            RoastMappings);
        await service.CommitAsync(plan);

        RoastData roast = target.RoastLogs.Should().ContainSingle().Subject;
        roast.RoastMinutes.Should().Be(11);
        roast.RoastSeconds.Should().Be(30);
        roast.BatchWeight.Should().Be(220);
    }

    [Fact]
    public async Task BuildPlanAsync_ForRoasts_WithoutFinalWeight_LandsAsAwaitingWeightReadyNow()
    {
        var target = new AppData();
        IImportService service = CreateService(target);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "220"), ("Time", "11:30"))],
            RoastMappings);
        await service.CommitAsync(plan);

        RoastData roast = target.RoastLogs.Single();
        roast.CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
        roast.FinalWeight.Should().BeNull();
        roast.RoastLevelName.Should().Be("Pending");
        roast.DroppedAtUtc.Should().NotBeNull();
        roast.CoolingDurationSeconds.Should().Be(0);
        roast.ReadyToWeighAtUtc.Should().Be(roast.DroppedAtUtc);
    }

    [Fact]
    public async Task BuildPlanAsync_ForRoasts_UsesLossPercentageOnlyToReconstructAMissingFinalWeight()
    {
        var target = new AppData();
        IImportService service = CreateService(target, roastLevelName: "Medium");

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [
                Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "200"), ("Loss", "15")),
                Row(("Date", "2026-03-02"), ("Bean", "Kenya AA"), ("Batch", "200"), ("Final", "170"), ("Loss", "50"))
            ],
            RoastMappingsWithWeights);
        await service.CommitAsync(plan);

        target.RoastLogs[0].FinalWeight.Should().Be(170);
        target.RoastLogs[0].CompletionStatus.Should().Be(RoastCompletionStatus.Complete);
        target.RoastLogs[0].RoastLevelName.Should().Be("Medium");
        target.RoastLogs[1].FinalWeight.Should().Be(170);
    }

    [Fact]
    public async Task BuildPlanAsync_ForRoasts_RejectsRowsThatViolateTheRoastModel()
    {
        IImportService service = CreateService(new AppData());

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "200"), ("Final", "250"))],
            RoastMappingsWithWeights);

        plan.AcceptedRows.Should().BeEmpty();
        plan.RejectedRows.Should().ContainSingle()
            .Which.Detail.Should().Contain("FinalWeight must be less than or equal to BatchWeight");
    }

    [Fact]
    public async Task BuildPlanAsync_ForRoasts_RejectsDuplicateEntries()
    {
        IImportService service = CreateService(new AppData());

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [
                Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "220"), ("Time", "11:30")),
                Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "220"), ("Time", "11:30"))
            ],
            RoastMappings);

        plan.AcceptedRows.Should().ContainSingle();
        plan.RejectedRows.Should().ContainSingle()
            .Which.Detail.Should().Contain("already logged");
    }

    [Fact]
    public async Task RoastLogExport_ReimportsCleanly()
    {
        // CafeMaestro's own roast-log export is the CSV users actually re-import: quoted bean
        // names, a timestamped date, mm:ss elapsed time, and a Weight Loss % column.
        string path = Path.Combine(_testDirectory, "CafeMaestro_RoastLog.csv");
        await File.WriteAllTextAsync(
            path,
            """
            Date,Bean Type,Temperature,Batch Weight,Final Weight,Weight Loss %,Roast Time,Roast Level,Notes
            2024-02-28 00:00,"Colombia - Tumbaga Decaf (Arabica)",235,200,173.6,13.2,14:00,Medium-Light,""
            2024-04-22 00:00,"Ethiopia - Yirgacheffe (Arabica)",235,230,197.57,14.1,13:30,Medium,""
            """);

        var target = new AppData();
        IImportService service = CreateService(target);
        ImportFileContent content = await service.ReadFileAsync(path);
        IReadOnlyDictionary<string, string> mappings =
            service.SuggestMappings(ImportKind.Roasts, content.Headers);

        mappings["RoastDate"].Should().Be("Date");
        mappings["BeanType"].Should().Be("Bean Type");
        mappings["Temperature"].Should().Be("Temperature");
        mappings["BatchWeight"].Should().Be("Batch Weight");
        mappings["FinalWeight"].Should().Be("Final Weight");
        mappings["RoastTime"].Should().Be("Roast Time");
        mappings["Notes"].Should().Be("Notes");

        ImportPlan plan = await service.BuildPlanAsync(ImportKind.Roasts, content.Rows, mappings);
        ImportCommitResult result = await service.CommitAsync(plan);

        result.Succeeded.Should().BeTrue();
        plan.RejectedRows.Should().BeEmpty();
        RoastData first = target.RoastLogs[0];
        first.BeanType.Should().Be("Colombia - Tumbaga Decaf (Arabica)");
        first.RoastDate.Should().Be(new DateTime(2024, 2, 28, 0, 0, 0));
        first.BatchWeight.Should().Be(200);
        first.FinalWeight.Should().Be(173.6);
        first.RoastMinutes.Should().Be(14);
        first.RoastSeconds.Should().Be(0);
        first.CompletionStatus.Should().Be(RoastCompletionStatus.Complete);
        // The supplied final weight stands; the Weight Loss % column never overrides it.
        first.WeightLossPercentage.Should().BeApproximately(13.2, 0.05);
    }

    [Fact]
    public async Task RoastLogImport_PreservesOptionalStableBeanIdForDuplicateNames()
    {
        BeanData first = new()
        {
            Id = Guid.NewGuid(), Country = "Ethiopia", CoffeeName = "Guji", Variety = "Heirloom",
            Quantity = 1, RemainingQuantity = 1
        };
        BeanData second = new()
        {
            Id = Guid.NewGuid(), Country = first.Country, CoffeeName = first.CoffeeName,
            Variety = first.Variety, Quantity = 1, RemainingQuantity = 1
        };
        var target = new AppData { Beans = [first, second] };
        IImportService service = CreateService(target);
        var mappings = new Dictionary<string, string>
        {
            ["RoastDate"] = "Date",
            ["BeanType"] = "Bean Type",
            ["BeanId"] = "Bean ID",
            ["BatchWeight"] = "Batch Weight"
        };

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(
                ("Date", "2026-03-01"),
                ("Bean Type", first.DisplayName),
                ("Bean ID", second.Id.ToString()),
                ("Batch Weight", "220"))],
            mappings);
        ImportCommitResult result = await service.CommitAsync(plan);

        result.Succeeded.Should().BeTrue();
        RoastData imported = target.RoastLogs.Should().ContainSingle().Subject;
        imported.BeanId.Should().Be(second.Id);
        imported.BeanDisplaySnapshot.Should().Be(first.DisplayName);
    }

    [Fact]
    public async Task RoastLogExportImport_RoundTripsStableIdsForDuplicateDisplayNames()
    {
        BeanData first = new()
        {
            Id = Guid.NewGuid(), Country = "Ethiopia", CoffeeName = "Guji", Variety = "Heirloom",
            Quantity = 1, RemainingQuantity = 1
        };
        BeanData second = new()
        {
            Id = Guid.NewGuid(), Country = first.Country, CoffeeName = first.CoffeeName,
            Variety = first.Variety, Quantity = 1, RemainingQuantity = 1
        };
        RoastData firstRoast = ExportableRoast(first, 210, 220, new DateTime(2026, 3, 1));
        RoastData secondRoast = ExportableRoast(second, 225, 240, new DateTime(2026, 3, 2));
        var exportedData = new AppData
        {
            Beans = [first, second],
            RoastLogs = [firstRoast, secondRoast]
        };
        var exporterAppData = new Mock<IAppDataService>();
        exporterAppData.SetupGet(service => service.DataFilePath).Returns("cafemaestro_data.json");
        exporterAppData.Setup(service => service.LoadAppDataAsync()).ReturnsAsync(exportedData);
        using var exporter = new RoastDataService(
            exporterAppData.Object,
            Mock.Of<IRoastLevelService>(),
            Mock.Of<ICoolingNotificationService>(),
            Mock.Of<IRoastPreferencesService>());
        await using var csvStream = new MemoryStream();
        await exporter.ExportRoastLogAsync(csvStream);
        csvStream.Position = 0;
        using var csvReader = new StreamReader(csvStream);
        string path = Path.Combine(_testDirectory, "duplicate-beans-round-trip.csv");
        await File.WriteAllTextAsync(path, await csvReader.ReadToEndAsync());

        var target = new AppData { Beans = [first, second] };
        IImportService importer = CreateService(target);
        ImportFileContent content = await importer.ReadFileAsync(path);
        IReadOnlyDictionary<string, string> mappings =
            importer.SuggestMappings(ImportKind.Roasts, content.Headers);
        ImportPlan plan = await importer.BuildPlanAsync(ImportKind.Roasts, content.Rows, mappings);
        ImportCommitResult result = await importer.CommitAsync(plan);

        result.Succeeded.Should().BeTrue();
        plan.RejectedRows.Should().BeEmpty();
        target.RoastLogs.Select(roast => roast.BeanId).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task IncompleteRoastExport_RoundTripsBackOntoTheAwaitingWeightPath()
    {
        // The exporter writes 0 in the final-weight column and "Pending" in the loss column for a
        // roast that has not been weighed. Both mean "not recorded".
        var target = new AppData();
        IImportService service = CreateService(target);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "220"), ("Final", "0"), ("Loss", "Pending"))],
            RoastMappingsWithWeights);
        ImportCommitResult result = await service.CommitAsync(plan);

        result.Succeeded.Should().BeTrue();
        plan.RejectedRows.Should().BeEmpty();
        RoastData roast = target.RoastLogs.Should().ContainSingle().Subject;
        roast.FinalWeight.Should().BeNull();
        roast.CompletionStatus.Should().Be(RoastCompletionStatus.AwaitingWeight);
        roast.RoastLevelName.Should().Be("Pending");
        roast.CoolingDurationSeconds.Should().Be(0);
        roast.ReadyToWeighAtUtc.Should().Be(roast.DroppedAtUtc);
    }

    [Fact]
    public async Task ArbitraryNonNumericLossIsStillRejected()
    {
        IImportService service = CreateService(new AppData());

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "220"), ("Final", ""), ("Loss", "roughly a lot"))],
            RoastMappingsWithWeights);

        plan.AcceptedRows.Should().BeEmpty();
        plan.RejectedRows.Should().ContainSingle()
            .Which.Detail.Should().Contain("is not a number");
    }

    [Fact]
    public async Task ASuppliedNegativeFinalWeightIsRejectedRatherThanDerivedFromLoss()
    {
        // HasFinalWeight is FinalWeight > 0, so a negative supplied weight used to look absent and
        // be silently replaced by a value reconstructed from the loss column.
        IImportService service = CreateService(new AppData());

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "200"), ("Final", "-5"), ("Loss", "15"))],
            RoastMappingsWithWeights);

        plan.AcceptedRows.Should().BeEmpty();
        plan.RejectedRows.Should().ContainSingle()
            .Which.Detail.Should().Contain("must be zero or greater");
    }

    // ------------------------------------------------------------------ commit

    [Fact]
    public async Task CommitAsync_WritesEveryAcceptedRowInOneMutationAndOneNotification()
    {
        string path = Path.Combine(_testDirectory, "cafemaestro_data.json");
        var appData = new ManagedAppDataService(path, () => "1.0.0");
        await appData.InitializeAsync(Mock.Of<IPreferencesService>());
        IImportService service = CreateService(appData);
        int notifications = 0;
        appData.DataChanged += (_, _) => notifications++;

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Beans,
            [
                Row(("Name", "Yirgacheffe"), ("Country", "Ethiopia")),
                Row(("Name", "Huila"), ("Country", "Colombia")),
                Row(("Name", ""), ("Country", "Kenya"))
            ],
            BeanMappings);
        ImportCommitResult result = await service.CommitAsync(plan);

        result.Succeeded.Should().BeTrue();
        result.Imported.Should().Be(2);
        result.Skipped.Should().Be(1);
        notifications.Should().Be(1);
        appData.CurrentData.Beans.Should().HaveCount(2);
        (await appData.LoadAppDataAsync()).Beans.Should().HaveCount(2);
    }

    [Fact]
    public async Task CommitAsync_RechecksDuplicatesAgainstTheDataItIsActuallyWriting()
    {
        // Review's duplicate check is a snapshot. Another writer can add a matching record between
        // Review and commit, so the policy is re-applied inside the mutation.
        var target = new AppData();
        IImportService service = CreateService(target);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Beans,
            [
                Row(("Name", "Yirgacheffe"), ("Country", "Ethiopia")),
                Row(("Name", "Huila"), ("Country", "Colombia"))
            ],
            BeanMappings);

        plan.AcceptedRows.Should().HaveCount(2);

        // Someone else adds one of the reviewed beans before the commit runs.
        target.Beans.Add(new BeanData
        {
            CoffeeName = "Yirgacheffe",
            Country = "Ethiopia",
            Quantity = 1,
            RemainingQuantity = 1
        });

        ImportCommitResult result = await service.CommitAsync(plan);

        result.Succeeded.Should().BeTrue();
        result.Imported.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.Errors.Should().Contain(error => error.Contains("while this import was being reviewed"));
        target.Beans.Where(bean => bean.CoffeeName == "Yirgacheffe").Should().ContainSingle();
        target.Beans.Should().HaveCount(2);
    }

    [Fact]
    public async Task CommitAsync_RechecksRoastDuplicatesAgainstTheDataItIsActuallyWriting()
    {
        var target = new AppData();
        IImportService service = CreateService(target);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Roasts,
            [Row(("Date", "2026-03-01"), ("Bean", "Kenya AA"), ("Batch", "220"), ("Time", "11:30"))],
            RoastMappings);

        RoastData accepted = new()
        {
            BeanType = "Kenya AA",
            BeanDisplaySnapshot = "Kenya AA",
            RoastDate = new DateTime(2026, 3, 1),
            BatchWeight = 220,
            Temperature = RoastImportAdapter.DefaultTemperature,
            RoastMinutes = 11,
            RoastSeconds = 30,
            CompletionStatus = RoastCompletionStatus.AwaitingWeight,
            DroppedAtUtc = DateTimeOffset.UtcNow,
            CoolingDurationSeconds = 0
        };
        target.RoastLogs.Add(accepted);

        ImportCommitResult result = await service.CommitAsync(plan);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
        target.RoastLogs.Should().ContainSingle();
    }

    [Fact]
    public async Task CommitAsync_WhenTheMutationIsRefused_ImportsNothingAndSaysSo()
    {
        var appData = new Mock<IAppDataService>();
        appData.Setup(service => service.LoadAppDataAsync()).ReturnsAsync(new AppData());
        appData.Setup(service => service.UpdateAsync(It.IsAny<Action<AppData>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        IImportService service = CreateService(appData);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Beans,
            [Row(("Name", "Yirgacheffe"), ("Country", "Ethiopia"))],
            BeanMappings);
        ImportCommitResult result = await service.CommitAsync(plan);

        result.Succeeded.Should().BeFalse();
        result.Imported.Should().Be(0);
        result.Errors.Should().Contain(error => error.Contains("No records were changed"));
    }

    [Fact]
    public async Task CommitAsync_WithNoAcceptedRows_NeverTouchesAppData()
    {
        var appData = new Mock<IAppDataService>();
        appData.Setup(service => service.LoadAppDataAsync()).ReturnsAsync(new AppData());
        IImportService service = CreateService(appData);

        ImportPlan plan = await service.BuildPlanAsync(
            ImportKind.Beans,
            [Row(("Name", ""), ("Country", "Ethiopia"))],
            BeanMappings);
        ImportCommitResult result = await service.CommitAsync(plan);

        result.Succeeded.Should().BeFalse();
        result.Skipped.Should().Be(1);
        appData.Verify(
            data => data.UpdateAsync(It.IsAny<Action<AppData>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BuildPlanAsync_HonoursCancellation()
    {
        IImportService service = CreateService(new AppData());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = () => service.BuildPlanAsync(
            ImportKind.Beans,
            [Row(("Name", "Yirgacheffe"), ("Country", "Ethiopia"))],
            BeanMappings,
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ------------------------------------------------------------------ file reading

    [Fact]
    public async Task ReadFileAsync_ReadsHeadersAndEveryRowWithoutModifyingTheSource()
    {
        string path = Path.Combine(_testDirectory, "beans.csv");
        await File.WriteAllTextAsync(path, "Name,Country\nYirgacheffe,Ethiopia\nHuila,Colombia\n");
        string originalContent = await File.ReadAllTextAsync(path);
        IImportService service = CreateService(new AppData());

        ImportFileContent content = await service.ReadFileAsync(path);

        content.Headers.Should().Equal("Name", "Country");
        content.Rows.Should().HaveCount(2);
        (await File.ReadAllTextAsync(path)).Should().Be(originalContent);
    }

    [Fact]
    public async Task ReadFileAsync_WithHeadersButNoRows_ReturnsNoRows()
    {
        string path = Path.Combine(_testDirectory, "empty.csv");
        await File.WriteAllTextAsync(path, "Name,Country\n");
        IImportService service = CreateService(new AppData());

        ImportFileContent content = await service.ReadFileAsync(path);

        content.Headers.Should().Equal("Name", "Country");
        content.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadFileAsync_WithAMissingFile_Throws()
    {
        IImportService service = CreateService(new AppData());

        Func<Task> act = () => service.ReadFileAsync(Path.Combine(_testDirectory, "absent.csv"));

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static readonly Dictionary<string, string> BeanMappings = new()
    {
        ["CoffeeName"] = "Name",
        ["Country"] = "Country",
        ["Quantity"] = "Qty"
    };

    private static readonly Dictionary<string, string> BeanMappingsWithVariety = new()
    {
        ["CoffeeName"] = "Name",
        ["Country"] = "Country",
        ["Variety"] = "Variety"
    };

    private static readonly Dictionary<string, string> BeanMappingsWithDate = new()
    {
        ["CoffeeName"] = "Name",
        ["Country"] = "Country",
        ["Quantity"] = "Qty",
        ["PurchaseDate"] = "Purchased"
    };

    private static readonly Dictionary<string, string> RoastMappings = new()
    {
        ["RoastDate"] = "Date",
        ["BeanType"] = "Bean",
        ["BatchWeight"] = "Batch",
        ["RoastTime"] = "Time"
    };

    private static readonly Dictionary<string, string> RoastMappingsWithWeights = new()
    {
        ["RoastDate"] = "Date",
        ["BeanType"] = "Bean",
        ["BatchWeight"] = "Batch",
        ["FinalWeight"] = "Final",
        ["WeightLoss"] = "Loss"
    };

    private static Dictionary<string, string> Row(params (string Header, string Value)[] cells) =>
        cells.ToDictionary(cell => cell.Header, cell => cell.Value);

    private static RoastData ExportableRoast(
        BeanData bean,
        double temperature,
        double batchWeight,
        DateTime date) => new()
        {
            Id = Guid.NewGuid(), BeanId = bean.Id, BeanType = bean.DisplayName,
            BeanDisplaySnapshot = bean.DisplayName, Temperature = temperature, BatchWeight = batchWeight,
            FinalWeight = batchWeight - 20, RoastDate = date, RoastMinutes = 11, RoastSeconds = 30,
            RoastLevelName = "Medium", CompletionStatus = RoastCompletionStatus.Complete
        };

    private static IImportService CreateService(AppData data, string roastLevelName = "Medium") =>
        CreateServiceWithMock(data, roastLevelName).Service;

    private static (IImportService Service, Mock<IAppDataService> AppData) CreateServiceWithMock(
        AppData data,
        string roastLevelName = "Medium")
    {
        var appData = new Mock<IAppDataService>();
        appData.Setup(service => service.LoadAppDataAsync()).ReturnsAsync(data);
        appData.Setup(service => service.UpdateAsync(It.IsAny<Action<AppData>>(), It.IsAny<CancellationToken>()))
            .Returns((Action<AppData> mutation, CancellationToken _) =>
            {
                mutation(data);
                return Task.FromResult(true);
            });

        return (CreateService(appData, roastLevelName), appData);
    }

    private static IImportService CreateService(Mock<IAppDataService> appData, string roastLevelName = "Medium") =>
        CreateService(appData.Object, roastLevelName);

    private static IImportService CreateService(IAppDataService appData, string roastLevelName = "Medium")
    {
        var roastLevels = new Mock<IRoastLevelService>();
        roastLevels.Setup(service => service.GetRoastLevelsAsync())
            .ReturnsAsync([new RoastLevelData(roastLevelName, 0, 100)]);

        return new ImportService(
            new CsvParserService(),
            appData,
            [new BeanImportAdapter(appData), new RoastImportAdapter(appData, roastLevels.Object)]);
    }
}
