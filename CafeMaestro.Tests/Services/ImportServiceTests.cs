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
