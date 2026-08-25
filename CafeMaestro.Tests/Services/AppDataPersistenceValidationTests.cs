using System.Text.Json;
using System.Text.Json.Nodes;
using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;

namespace CafeMaestro.Tests.Services;

public sealed class AppDataPersistenceValidationTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public AppDataPersistenceValidationTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task InitializeAsync_CurrentRoastWithNegativeCooling_RejectsWireData()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.RoastLogs.Add(CreateAwaitingRoast());
        data.RoastLogs[0].CoolingDurationSeconds = -1;

        await AssertWireDataIsRejectedAsync(data, "*CoolingDurationSeconds*");
    }

    [Fact]
    public async Task InitializeAsync_CurrentRoastWithOverflowingReadyTime_RejectsWireData()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.RoastLogs.Add(CreateAwaitingRoast());
        data.RoastLogs[0].DroppedAtUtc = DateTimeOffset.MaxValue;
        data.RoastLogs[0].CoolingDurationSeconds = 1;

        await AssertWireDataIsRejectedAsync(data, "*readiness time*");
    }

    [Fact]
    public async Task InitializeAsync_CurrentAwaitingRoastMissingWorkflowAnchors_RejectsInsteadOfRepairing()
    {
        AppData data = AppDataFactory.CreateDefault();
        RoastData roast = CreateAwaitingRoast();
        roast.RoastDate = DateTime.UtcNow;
        roast.DroppedAtUtc = null;
        roast.CoolingDurationSeconds = null;
        data.RoastLogs.Add(roast);

        await AssertWireDataIsRejectedAsync(data, "*require DroppedAtUtc*");
    }

    [Theory]
    [InlineData(RoastCompletionStatus.Unweighed)]
    [InlineData(RoastCompletionStatus.Discarded)]
    public async Task InitializeAsync_CurrentClosedRoastWithFinalWeight_RejectsInsteadOfCoercing(
        RoastCompletionStatus status)
    {
        AppData data = AppDataFactory.CreateDefault();
        RoastData roast = CreateAwaitingRoast();
        roast.CompletionStatus = status;
        roast.FinalWeight = 180;
        data.RoastLogs.Add(roast);

        await AssertWireDataIsRejectedAsync(data, "*cannot contain FinalWeight*");
    }

    [Theory]
    [InlineData("empty-session-id")]
    [InlineData("invalid-next-batch")]
    [InlineData("draft-session-mismatch")]
    [InlineData("invalid-draft-id")]
    [InlineData("invalid-batch-number")]
    [InlineData("invalid-bean-id")]
    [InlineData("orphan-bean-id")]
    [InlineData("missing-snapshot")]
    [InlineData("invalid-temperature")]
    [InlineData("invalid-weight")]
    [InlineData("invalid-phase")]
    [InlineData("invalid-start-anchor")]
    [InlineData("roasting-without-running-anchor")]
    [InlineData("paused-with-running-anchor")]
    [InlineData("negative-accumulated-time")]
    [InlineData("negative-first-crack")]
    [InlineData("negative-cooling")]
    public void GetValidationErrors_InvalidActiveSessionGraph_ReportsError(string scenario)
    {
        AppData data = AppDataFactory.CreateDefault();
        data.ActiveRoastSession = CreateValidSession(data);
        ActiveRoastDraft draft = data.ActiveRoastSession.ActiveRoast!;

        switch (scenario)
        {
            case "empty-session-id": data.ActiveRoastSession.Id = Guid.Empty; break;
            case "invalid-next-batch": data.ActiveRoastSession.NextBatchNumber = 0; break;
            case "draft-session-mismatch": draft.SessionId = Guid.NewGuid(); break;
            case "invalid-draft-id": draft.Id = Guid.Empty; break;
            case "invalid-batch-number": draft.BatchNumber = 0; break;
            case "invalid-bean-id": draft.BeanId = Guid.Empty; break;
            case "orphan-bean-id": draft.BeanId = Guid.NewGuid(); break;
            case "missing-snapshot": draft.BeanDisplaySnapshot = " "; break;
            case "invalid-temperature": draft.Temperature = 501; break;
            case "invalid-weight": draft.BatchWeight = 0; break;
            case "invalid-phase": draft.Phase = (ActiveRoastPhase)99; break;
            case "invalid-start-anchor": draft.StartedAtUtc = default; break;
            case "roasting-without-running-anchor": draft.RunningSinceUtc = null; break;
            case "paused-with-running-anchor":
                draft.Phase = ActiveRoastPhase.Paused;
                draft.RunningSinceUtc = DateTimeOffset.UtcNow;
                break;
            case "negative-accumulated-time": draft.AccumulatedElapsedSeconds = -1; break;
            case "negative-first-crack": draft.FirstCrackElapsedSeconds = -1; break;
            case "negative-cooling": draft.CoolingDurationSeconds = -1; break;
        }

        AppDataNormalizer.GetValidationErrors(data).Should().NotBeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_InvalidActiveSessionWireGraph_RejectsIt()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.ActiveRoastSession = CreateValidSession(data);
        data.ActiveRoastSession.ActiveRoast!.RunningSinceUtc = null;

        await AssertWireDataIsRejectedAsync(data, "*RunningSinceUtc*");
    }

    [Fact]
    public async Task InitializeAsync_PausedSessionWithFutureFirstCrack_RejectsWireGraph()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.ActiveRoastSession = CreateValidSession(data);
        ActiveRoastDraft draft = data.ActiveRoastSession.ActiveRoast!;
        draft.Phase = ActiveRoastPhase.Paused;
        draft.RunningSinceUtc = null;
        draft.AccumulatedElapsedSeconds = 30;
        draft.FirstCrackElapsedSeconds = 600;

        await AssertWireDataIsRejectedAsync(data, "*FirstCrackElapsedSeconds*elapsed*");
    }

    [Fact]
    public async Task InitializeAsync_ActiveSessionCoolingProjectionOverflow_RejectsWireGraph()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.ActiveRoastSession = CreateValidSession(data);
        data.ActiveRoastSession.StartedAtUtc = DateTimeOffset.MaxValue;
        ActiveRoastDraft draft = data.ActiveRoastSession.ActiveRoast!;
        draft.StartedAtUtc = DateTimeOffset.MaxValue;
        draft.RunningSinceUtc = DateTimeOffset.MaxValue;
        draft.CoolingDurationSeconds = 1;

        await AssertWireDataIsRejectedAsync(data, "*cooling*range*");
    }

    [Theory]
    [InlineData("temperature", double.NaN)]
    [InlineData("temperature", double.PositiveInfinity)]
    [InlineData("weight", double.NaN)]
    [InlineData("weight", double.PositiveInfinity)]
    public void ActiveSession_NonFiniteNumericValue_FailsStorageValidation(
        string field,
        double invalidValue)
    {
        AppData data = AppDataFactory.CreateDefault();
        data.ActiveRoastSession = CreateValidSession(data);
        ActiveRoastDraft draft = data.ActiveRoastSession.ActiveRoast!;
        if (field == "temperature")
        {
            draft.Temperature = invalidValue;
        }
        else
        {
            draft.BatchWeight = invalidValue;
        }

        AppDataNormalizer.GetValidationErrors(data)
            .Should().Contain(error => error.Contains(
                field == "temperature" ? "Temperature" : "BatchWeight"));
    }

    [Theory]
    [InlineData(nameof(ActiveRoastDraft.Temperature))]
    [InlineData(nameof(ActiveRoastDraft.BatchWeight))]
    public async Task InitializeAsync_ActiveSessionOverflowedWireNumber_RejectsWireGraph(
        string propertyName)
    {
        AppData data = AppDataFactory.CreateDefault();
        data.ActiveRoastSession = CreateValidSession(data);
        string json = JsonSerializer.Serialize(data);
        string originalValue = propertyName == nameof(ActiveRoastDraft.Temperature)
            ? "\"Temperature\":205"
            : "\"BatchWeight\":200";
        string invalidJson = json.Replace(
            originalValue,
            $"\"{propertyName}\":1e309",
            StringComparison.Ordinal);

        await AssertRawWireDataIsRejectedAsync(invalidJson, $"*{propertyName}*");
    }

    [Fact]
    public async Task InitializeAsync_CurrentRoastMissingCompletionStatus_RejectsWireShape()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.RoastLogs.Add(CreateAwaitingRoast());
        JsonObject root = JsonNode.Parse(JsonSerializer.Serialize(data))!.AsObject();
        root[nameof(AppData.RoastLogs)]!.AsArray()[0]!.AsObject()
            .Remove(nameof(RoastData.CompletionStatus));

        await AssertRawWireDataIsRejectedAsync(root.ToJsonString(), "*CompletionStatus*");
    }

    [Fact]
    public async Task InitializeAsync_CurrentActiveDraftMissingPhase_RejectsWireShape()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.ActiveRoastSession = CreateValidSession(data);
        JsonObject root = JsonNode.Parse(JsonSerializer.Serialize(data))!.AsObject();
        root[nameof(AppData.ActiveRoastSession)]!.AsObject()
            [nameof(RoastSessionData.ActiveRoast)]!.AsObject()
            .Remove(nameof(ActiveRoastDraft.Phase));

        await AssertRawWireDataIsRejectedAsync(root.ToJsonString(), "*Phase*");
    }

    [Fact]
    public async Task InitializeAsync_CurrentActiveSessionReferencingStoredBean_Loads()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.ActiveRoastSession = CreateValidSession(data);
        string path = Path.Combine(_testDirectory, $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(data));
        var service = new ManagedAppDataService(path);

        AppData loaded = await service.InitializeAsync(Mock.Of<IPreferencesService>());

        loaded.ActiveRoastSession!.ActiveRoast!.BeanId
            .Should().Be(loaded.Beans.Single().Id);
    }

    [Theory]
    [InlineData(nameof(AppData.Beans))]
    [InlineData(nameof(AppData.RoastLogs))]
    [InlineData(nameof(AppData.RoastLevels))]
    public async Task InitializeAsync_CurrentSchemaMissingCollection_RejectsWireShape(
        string collectionName)
    {
        JsonObject root = JsonNode.Parse(
            JsonSerializer.Serialize(AppDataFactory.CreateDefault()))!.AsObject();
        root.Remove(collectionName);

        await AssertRawWireDataIsRejectedAsync(root.ToJsonString(), $"*{collectionName}*");
    }

    [Theory]
    [InlineData(nameof(AppData.Beans))]
    [InlineData(nameof(AppData.RoastLogs))]
    [InlineData(nameof(AppData.RoastLevels))]
    public async Task InitializeAsync_CurrentSchemaNullCollection_RejectsWireShape(
        string collectionName)
    {
        JsonObject root = JsonNode.Parse(
            JsonSerializer.Serialize(AppDataFactory.CreateDefault()))!.AsObject();
        root[collectionName] = null;

        await AssertRawWireDataIsRejectedAsync(root.ToJsonString(), $"*{collectionName}*");
    }

    [Fact]
    public async Task InitializeAsync_CurrentRoastWithBlankSnapshot_RejectsInsteadOfRepairing()
    {
        AppData data = AppDataFactory.CreateDefault();
        RoastData roast = CreateAwaitingRoast();
        roast.BeanDisplaySnapshot = " ";
        data.RoastLogs.Add(roast);

        await AssertWireDataIsRejectedAsync(data, "*BeanDisplaySnapshot*");
    }

    [Fact]
    public async Task InitializeAsync_CurrentRoastMissingBeanType_RejectsWireShape()
    {
        AppData data = AppDataFactory.CreateDefault();
        data.RoastLogs.Add(CreateAwaitingRoast());
        JsonObject root = JsonNode.Parse(JsonSerializer.Serialize(data))!.AsObject();
        root[nameof(AppData.RoastLogs)]!.AsArray()[0]!.AsObject()
            .Remove(nameof(RoastData.BeanType));

        await AssertRawWireDataIsRejectedAsync(root.ToJsonString(), "*BeanType*");
    }

    [Fact]
    public async Task InitializeAsync_CurrentRoastWithBlankBeanType_RejectsWireData()
    {
        AppData data = AppDataFactory.CreateDefault();
        RoastData roast = CreateAwaitingRoast();
        roast.BeanType = " ";
        data.RoastLogs.Add(roast);

        await AssertWireDataIsRejectedAsync(data, "*BeanType*");
    }

    private async Task AssertWireDataIsRejectedAsync(AppData data, string message)
    {
        await AssertRawWireDataIsRejectedAsync(JsonSerializer.Serialize(data), message);
    }

    private async Task AssertRawWireDataIsRejectedAsync(string json, string message)
    {
        string path = Path.Combine(_testDirectory, $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json);
        var service = new ManagedAppDataService(path);

        Func<Task> action = () => service.InitializeAsync(Mock.Of<IPreferencesService>());

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage(message);
    }

    private static RoastData CreateAwaitingRoast() => new()
    {
        BeanType = "Ethiopia",
        BeanDisplaySnapshot = "Ethiopia",
        Temperature = 205,
        BatchWeight = 200,
        RoastMinutes = 10,
        RoastDate = DateTime.UtcNow,
        DroppedAtUtc = DateTimeOffset.UtcNow,
        CoolingDurationSeconds = 300,
        CompletionStatus = RoastCompletionStatus.AwaitingWeight
    };

    private static RoastSessionData CreateValidSession(AppData data)
    {
        Guid sessionId = Guid.NewGuid();
        Guid beanId = Guid.NewGuid();
        DateTimeOffset started = DateTimeOffset.UtcNow.AddMinutes(-5);
        data.Beans.Add(new BeanData
        {
            Id = beanId,
            Country = "Ethiopia",
            CoffeeName = "Test",
            Quantity = 1,
            RemainingQuantity = 1
        });
        return new RoastSessionData
        {
            Id = sessionId,
            StartedAtUtc = started,
            NextBatchNumber = 1,
            ActiveRoast = new ActiveRoastDraft
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                BatchNumber = 1,
                BeanId = beanId,
                BeanDisplaySnapshot = "Ethiopia",
                Temperature = 205,
                BatchWeight = 200,
                Phase = ActiveRoastPhase.Roasting,
                StartedAtUtc = started,
                RunningSinceUtc = started,
                AccumulatedElapsedSeconds = 0,
                FirstCrackEnabled = true,
                CoolingDurationSeconds = 300
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
