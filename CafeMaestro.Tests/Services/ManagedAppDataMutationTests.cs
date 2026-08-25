using CafeMaestro.Models;
using CafeMaestro.Services;
using FluentAssertions;
using Moq;
using System.Text.Json;

namespace CafeMaestro.Tests.Services;

public sealed class ManagedAppDataMutationTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), "CafeMaestro.Tests", Guid.NewGuid().ToString("N"));

    public ManagedAppDataMutationTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task UpdateAsync_ValidMutation_WritesCacheAndDiskAndRaisesOneEvent()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        int eventCount = 0;
        service.DataChanged += (_, _) => eventCount++;

        bool updated = await service.UpdateAsync(data => data.Beans.Add(CreateBean("Atomic")));

        updated.Should().BeTrue();
        eventCount.Should().Be(1);
        service.CurrentData.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Atomic");
        AppData readBack = await service.LoadAppDataAsync();
        readBack.Should().BeEquivalentTo(service.CurrentData);
    }

    [Fact]
    public async Task UpdateAsync_InvalidMutation_RollsBackWithoutLeakingOrRaisingEvent()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        AppData liveData = service.CurrentData;
        AppData? mutationCandidate = null;
        string originalJson = await File.ReadAllTextAsync(service.DataFilePath);
        int eventCount = 0;
        service.DataChanged += (_, _) => eventCount++;

        bool updated = await service.UpdateAsync(data =>
        {
            mutationCandidate = data;
            data.Beans.Add(CreateBean(string.Empty));
        });

        updated.Should().BeFalse();
        mutationCandidate.Should().NotBeSameAs(liveData);
        service.CurrentData.Beans.Should().BeEmpty();
        eventCount.Should().Be(0);
        (await File.ReadAllTextAsync(service.DataFilePath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task UpdateAsync_WriterThrows_RollsBackCacheFileAndEvent()
    {
        string canonicalPath = Path.Combine(_testDirectory, "writer-failure.json");
        string originalJson = System.Text.Json.JsonSerializer.Serialize(AppDataFactory.CreateDefault());
        await File.WriteAllTextAsync(canonicalPath, originalJson);
        var service = new ManagedAppDataService(
            canonicalPath,
            () => "2.0.0",
            (_, _) => throw new IOException("Injected writer failure."));
        await service.LoadAppDataAsync();
        int eventCount = 0;
        service.DataChanged += (_, _) => eventCount++;

        bool updated = await service.UpdateAsync(data => data.Beans.Add(CreateBean("Rejected")));

        updated.Should().BeFalse();
        service.CurrentData.Beans.Should().BeEmpty();
        eventCount.Should().Be(0);
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentCalls_AreSerializedWithoutLostUpdates()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        int eventCount = 0;
        service.DataChanged += (_, _) => Interlocked.Increment(ref eventCount);

        Task<bool>[] updates = Enumerable.Range(0, 12)
            .Select(index => Task.Run(() => service.UpdateAsync(data =>
            {
                Thread.Sleep(5);
                data.Beans.Add(CreateBean($"Bean {index}"));
            })))
            .ToArray();

        bool[] results = await Task.WhenAll(updates);

        results.Should().OnlyContain(result => result);
        service.CurrentData.Beans.Should().HaveCount(12);
        eventCount.Should().Be(12);
        (await service.LoadAppDataAsync()).Beans.Should().HaveCount(12);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentCommits_PublishEventsSeriallyInCommitOrder()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        var firstEventEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstEvent = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var payloadCounts = new List<int>();
        int activeHandlers = 0;
        int maximumActiveHandlers = 0;

        service.DataChanged += (_, data) =>
        {
            int active = Interlocked.Increment(ref activeHandlers);
            maximumActiveHandlers = Math.Max(maximumActiveHandlers, active);
            lock (payloadCounts)
            {
                payloadCounts.Add(data.Beans.Count);
            }

            if (data.Beans.Count == 1)
            {
                firstEventEntered.SetResult();
                releaseFirstEvent.Task.GetAwaiter().GetResult();
            }

            Interlocked.Decrement(ref activeHandlers);
        };

        Task<bool> first = Task.Run(() =>
            service.UpdateAsync(data => data.Beans.Add(CreateBean("First"))));
        await firstEventEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<bool> second = Task.Run(() =>
            service.UpdateAsync(data => data.Beans.Add(CreateBean("Second"))));
        await EventuallyAsync(() => service.CurrentData.Beans.Count == 2);

        maximumActiveHandlers.Should().Be(1);
        releaseFirstEvent.SetResult();
        (await Task.WhenAll(first, second)).Should().OnlyContain(result => result);
        payloadCounts.Should().Equal(1, 2);
        maximumActiveHandlers.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_DelayedReentrantHandler_DrainsNestedNotification()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        var releaseNestedMutation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var nestedMutationCompleted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int eventCount = 0;

        service.DataChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref eventCount) != 1)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                await releaseNestedMutation.Task;
                bool result = await service.UpdateAsync(
                    data => data.Beans.Add(CreateBean("Delayed")));
                nestedMutationCompleted.TrySetResult(result);
            });
        };

        (await service.UpdateAsync(data => data.Beans.Add(CreateBean("Initial"))))
            .Should().BeTrue();
        releaseNestedMutation.SetResult();

        (await nestedMutationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2)))
            .Should().BeTrue();
        await EventuallyAsync(() => Volatile.Read(ref eventCount) == 2);
        service.CurrentData.Beans.Should().HaveCount(2);
    }

    [Fact]
    public async Task DataChanged_AsyncVoidSubscriberThatThrowsAfterAwait_IsRejectedAtRegistration()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        EventHandler<AppData> asyncSubscriber = async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Delayed subscriber failure.");
        };

        Action subscribe = () => service.DataChanged += asyncSubscriber;

        subscribe.Should().Throw<ArgumentException>().WithMessage("*synchronous*");
    }

    [Fact]
    public async Task UpdateAsync_SubscriberThrows_CommitStillSucceedsAndOtherHandlersRun()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        int successfulHandlerCount = 0;
        service.DataChanged += (_, _) => throw new InvalidOperationException("Subscriber failure.");
        service.DataChanged += (_, _) => successfulHandlerCount++;

        bool updated = await service.UpdateAsync(
            data => data.Beans.Add(CreateBean("Committed")));

        updated.Should().BeTrue();
        successfulHandlerCount.Should().Be(1);
        service.CurrentData.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Committed");
        (await service.LoadAppDataAsync()).Beans
            .Should().ContainSingle(bean => bean.CoffeeName == "Committed");
    }

    [Fact]
    public async Task UpdateAsync_CurrentCandidateClearsSnapshot_RejectsWithoutRepairing()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        RoastData roast = CreateAwaitingRoast();
        (await service.UpdateAsync(data => data.RoastLogs.Add(roast))).Should().BeTrue();
        string originalJson = await File.ReadAllTextAsync(service.DataFilePath);

        bool updated = await service.UpdateAsync(
            data => data.RoastLogs[0].BeanDisplaySnapshot = " ");

        updated.Should().BeFalse();
        service.CurrentData.RoastLogs.Single().BeanDisplaySnapshot.Should().Be("Ethiopia");
        (await File.ReadAllTextAsync(service.DataFilePath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task SaveAppDataAsync_CurrentCandidateNullsCollection_RejectsWithoutRepairing()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        AppData candidate = await service.LoadAppDataAsync();
        candidate.Beans = null!;
        string originalJson = await File.ReadAllTextAsync(service.DataFilePath);

        bool saved = await service.SaveAppDataAsync(candidate);

        saved.Should().BeFalse();
        service.CurrentData.Beans.Should().NotBeNull();
        (await File.ReadAllTextAsync(service.DataFilePath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task UpdateAsync_FutureSchemaCandidate_IsRejectedWithoutDownConversion()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        string originalJson = await File.ReadAllTextAsync(service.DataFilePath);

        bool updated = await service.UpdateAsync(
            data => data.DataSchemaVersion = AppDataSchema.CurrentVersion + 1);

        updated.Should().BeFalse();
        service.CurrentData.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
        (await File.ReadAllTextAsync(service.DataFilePath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task SaveAppDataAsync_FutureSchemaCandidate_IsRejectedWithoutDownConversion()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        AppData candidate = await service.LoadAppDataAsync();
        candidate.DataSchemaVersion = AppDataSchema.CurrentVersion + 1;
        string originalJson = await File.ReadAllTextAsync(service.DataFilePath);

        bool saved = await service.SaveAppDataAsync(candidate);

        saved.Should().BeFalse();
        service.CurrentData.DataSchemaVersion.Should().Be(AppDataSchema.CurrentVersion);
        (await File.ReadAllTextAsync(service.DataFilePath)).Should().Be(originalJson);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(AppDataSchema.CurrentVersion + 1)]
    public async Task SaveAppDataAsync_ColdServiceWithCanonicalFile_RejectsWithoutRewritingOrBackingUp(
        int persistedSchemaVersion)
    {
        string canonicalPath = Path.Combine(
            _testDirectory,
            $"cold-save-{persistedSchemaVersion}.json");
        AppData persisted = AppDataFactory.CreateDefault();
        persisted.DataSchemaVersion = persistedSchemaVersion;
        string originalJson = JsonSerializer.Serialize(persisted);
        await File.WriteAllTextAsync(canonicalPath, originalJson);
        var service = new ManagedAppDataService(canonicalPath, () => "1.5.0");
        AppData replacement = AppDataFactory.CreateDefault();
        replacement.Beans.Add(CreateBean("Replacement"));

        bool saved = await service.SaveAppDataAsync(replacement);

        saved.Should().BeFalse();
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(originalJson);
        Directory.Exists(Path.Combine(_testDirectory, "Backups")).Should().BeFalse();
        service.CurrentData.Beans.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ColdForwardCanonical_MarksRecoveryAndDoesNotWrite()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cold-forward-update.json");
        string originalJson = $$"""
            { "DataSchemaVersion": {{AppDataSchema.CurrentVersion + 1}}, "Beans": [], "RoastLogs": [], "RoastLevels": [] }
            """;
        await File.WriteAllTextAsync(canonicalPath, originalJson);
        var service = new ManagedAppDataService(canonicalPath, () => "1.5.0");

        Func<Task> action = () => service.UpdateAsync(
            data => data.Beans.Add(CreateBean("Blocked")));

        await action.Should().ThrowAsync<InvalidDataException>();
        service.IsRecoveryRequired.Should().BeTrue();
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task SaveAppDataAsync_ColdForwardCanonical_MarksRecoveryAndDoesNotWrite()
    {
        string canonicalPath = Path.Combine(_testDirectory, "cold-forward-save.json");
        string originalJson = $$"""
            { "DataSchemaVersion": {{AppDataSchema.CurrentVersion + 1}}, "Beans": [], "RoastLogs": [], "RoastLevels": [] }
            """;
        await File.WriteAllTextAsync(canonicalPath, originalJson);
        var service = new ManagedAppDataService(canonicalPath, () => "1.5.0");

        bool saved = await service.SaveAppDataAsync(AppDataFactory.CreateDefault());

        saved.Should().BeFalse();
        service.IsRecoveryRequired.Should().BeTrue();
        (await File.ReadAllTextAsync(canonicalPath)).Should().Be(originalJson);
    }

    [Fact]
    public async Task RecoveryRequired_AfterFailedReload_BlocksCachedUpdateAndSave()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        AppData stale = await service.LoadAppDataAsync();
        string forwardJson = $$"""
            { "DataSchemaVersion": {{AppDataSchema.CurrentVersion + 1}}, "Beans": [], "RoastLogs": [], "RoastLevels": [] }
            """;
        await File.WriteAllTextAsync(service.DataFilePath, forwardJson);
        Func<Task> reload = () => service.LoadAppDataAsync();
        await reload.Should().ThrowAsync<InvalidDataException>();
        bool mutationInvoked = false;

        bool updated = await service.UpdateAsync(data =>
        {
            mutationInvoked = true;
            data.Beans.Add(CreateBean("Blocked"));
        });
        stale.Beans.Add(CreateBean("Also blocked"));
        bool saved = await service.SaveAppDataAsync(stale);

        updated.Should().BeFalse();
        saved.Should().BeFalse();
        mutationInvoked.Should().BeFalse();
        service.IsRecoveryRequired.Should().BeTrue();
        (await File.ReadAllTextAsync(service.DataFilePath)).Should().Be(forwardJson);
    }

    [Fact]
    public async Task RecoveryRequired_WhenCanonicalDisappears_RemainsWriteBarrier()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        string forwardJson = $$"""
            { "DataSchemaVersion": {{AppDataSchema.CurrentVersion + 1}}, "Beans": [], "RoastLogs": [], "RoastLevels": [] }
            """;
        await File.WriteAllTextAsync(service.DataFilePath, forwardJson);
        await FluentActions.Invoking(() => service.LoadAppDataAsync())
            .Should().ThrowAsync<InvalidDataException>();
        File.Delete(service.DataFilePath);

        AppData fallback = await service.LoadAppDataAsync();
        fallback.Beans.Add(CreateBean("Blocked"));
        bool saved = await service.SaveAppDataAsync(fallback);

        saved.Should().BeFalse();
        service.IsRecoveryRequired.Should().BeTrue();
        File.Exists(service.DataFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task SuspendNotifications_RacingEnqueue_SuppressesCommittedEvent()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        object queueLock = typeof(ManagedAppDataService)
            .GetField(
                "_notificationQueueLock",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(service)!;
        int eventCount = 0;
        service.DataChanged += (_, _) => eventCount++;
        Task<bool> update;
        IDisposable? suspension = null;

        Monitor.Enter(queueLock);
        try
        {
            update = Task.Run(() =>
                service.UpdateAsync(data => data.Beans.Add(CreateBean("Suppressed"))));
            SpinWait.SpinUntil(
                    () => service.CurrentData.Beans.Count == 1,
                    TimeSpan.FromSeconds(2))
                .Should().BeTrue();
            Thread.Sleep(50);
            suspension = service.SuspendNotifications();
        }
        finally
        {
            Monitor.Exit(queueLock);
        }

        try
        {
            (await update).Should().BeTrue();
            eventCount.Should().Be(0);
            service.CurrentData.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Suppressed");
        }
        finally
        {
            suspension?.Dispose();
        }
    }

    [Fact]
    public async Task SuspendNotifications_RacingReloadAdmission_SuppressesEventAtQueueBoundary()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        object queueLock = typeof(ManagedAppDataService)
            .GetField(
                "_notificationQueueLock",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(service)!;
        int eventCount = 0;
        service.DataChanged += (_, _) => eventCount++;
        long originalRevision = service.CurrentData.PersistenceRevision;
        Task<AppData> reloadTask;
        IDisposable? suspension = null;

        Monitor.Enter(queueLock);
        try
        {
            reloadTask = Task.Run(service.ReloadDataAsync);
            SpinWait.SpinUntil(
                    () => service.CurrentData.PersistenceRevision > originalRevision,
                    TimeSpan.FromSeconds(2))
                .Should().BeTrue();
            suspension = service.SuspendNotifications();
        }
        finally
        {
            Monitor.Exit(queueLock);
        }

        try
        {
            await reloadTask.WaitAsync(TimeSpan.FromSeconds(2));
            eventCount.Should().Be(0);
        }
        finally
        {
            suspension?.Dispose();
        }
    }

    [Fact]
    public async Task ReloadDataAsync_QueuesNotificationBeforeLaterAtomicCommit()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        var dispatchLock = (SemaphoreSlim)typeof(ManagedAppDataService)
            .GetField(
                "_notificationDispatchLock",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(service)!;
        var payloadBeanCounts = new List<int>();
        service.DataChanged += (_, data) => payloadBeanCounts.Add(data.Beans.Count);
        long originalRevision = service.CurrentData.PersistenceRevision;
        await dispatchLock.WaitAsync();

        try
        {
            Task<AppData> reload = Task.Run(service.ReloadDataAsync);
            SpinWait.SpinUntil(
                    () => service.CurrentData.PersistenceRevision > originalRevision,
                    TimeSpan.FromSeconds(2))
                .Should().BeTrue();
            Thread.Sleep(50);
            reload.IsCompleted.Should().BeFalse();
            payloadBeanCounts.Should().BeEmpty();

            Task<bool> update = Task.Run(() =>
                service.UpdateAsync(data => data.Beans.Add(CreateBean("Later"))));
            SpinWait.SpinUntil(
                    () => service.CurrentData.Beans.Count == 1,
                    TimeSpan.FromSeconds(2))
                .Should().BeTrue();
            dispatchLock.Release();

            await reload.WaitAsync(TimeSpan.FromSeconds(2));
            (await update.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();
            payloadBeanCounts.Should().Equal(0, 1);
        }
        finally
        {
            if (dispatchLock.CurrentCount == 0)
            {
                dispatchLock.Release();
            }
        }
    }

    [Fact]
    public async Task SuspendNotifications_QueuedEventWaitingForDispatch_IsSuppressed()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        var dispatchLock = (SemaphoreSlim)typeof(ManagedAppDataService)
            .GetField(
                "_notificationDispatchLock",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(service)!;
        int eventCount = 0;
        service.DataChanged += (_, _) => eventCount++;
        await dispatchLock.WaitAsync();
        IDisposable? suspension = null;

        try
        {
            Task<bool> update = Task.Run(() =>
                service.UpdateAsync(data => data.Beans.Add(CreateBean("Suppressed"))));
            SpinWait.SpinUntil(
                    () => service.CurrentData.Beans.Count == 1,
                    TimeSpan.FromSeconds(2))
                .Should().BeTrue();
            Thread.Sleep(50);
            suspension = service.SuspendNotifications();
            dispatchLock.Release();

            (await update.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();
            eventCount.Should().Be(0);
        }
        finally
        {
            if (dispatchLock.CurrentCount == 0)
            {
                dispatchLock.Release();
            }

            suspension?.Dispose();
        }
    }

    [Fact]
    public async Task UpdateAsync_MutationThrows_DoesNotLeakCandidateOrRaiseEvent()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        int eventCount = 0;
        service.DataChanged += (_, _) => eventCount++;

        Func<Task> action = () => service.UpdateAsync(data =>
        {
            data.Beans.Add(CreateBean("Transient"));
            throw new InvalidOperationException("Mutation failed.");
        });

        await action.Should().ThrowAsync<InvalidOperationException>();
        service.CurrentData.Beans.Should().BeEmpty();
        eventCount.Should().Be(0);
    }

    [Fact]
    public async Task SaveAppDataAsync_StaleLoadedCopyAfterUpdate_IsRejectedWithoutLosingUpdate()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        AppData staleCopy = await service.LoadAppDataAsync();

        bool updated = await service.UpdateAsync(data => data.Beans.Add(CreateBean("Atomic")));
        staleCopy.Beans.Add(CreateBean("Stale"));
        bool saved = await service.SaveAppDataAsync(staleCopy);

        updated.Should().BeTrue();
        saved.Should().BeFalse();
        service.CurrentData.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Atomic");
        (await service.LoadAppDataAsync()).Beans
            .Should().ContainSingle(bean => bean.CoffeeName == "Atomic");
    }

    [Fact]
    public async Task SaveAppDataAsync_PublishesAfterLockSoHandlerCanMutateReentrantly()
    {
        ManagedAppDataService service = await CreateInitializedServiceAsync();
        AppData copy = await service.LoadAppDataAsync();
        copy.Beans.Add(CreateBean("Saved"));
        int eventCount = 0;

        service.DataChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref eventCount) == 1)
            {
                service.UpdateAsync(data => data.Beans.Add(CreateBean("Reentrant")))
                    .WaitAsync(TimeSpan.FromSeconds(2))
                    .GetAwaiter()
                    .GetResult()
                    .Should().BeTrue();
            }
        };

        bool saved = await service.SaveAppDataAsync(copy);

        saved.Should().BeTrue();
        eventCount.Should().Be(2);
        service.CurrentData.Beans.Select(bean => bean.CoffeeName)
            .Should().BeEquivalentTo("Saved", "Reentrant");
        (await service.LoadAppDataAsync()).Should().BeEquivalentTo(service.CurrentData);
    }

    [Fact]
    public async Task SaveAppDataAsync_RacingAtomicUpdate_RejectsStaleSaveAndKeepsDiskInSync()
    {
        string canonicalPath = Path.Combine(_testDirectory, "save-update-race.json");
        await File.WriteAllTextAsync(
            canonicalPath,
            JsonSerializer.Serialize(AppDataFactory.CreateDefault()));
        var writeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int writeCount = 0;
        async Task WriteAsync(AppData data, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref writeCount) == 1)
            {
                writeEntered.SetResult();
                await releaseWrite.Task.WaitAsync(cancellationToken);
            }

            await File.WriteAllTextAsync(
                canonicalPath,
                JsonSerializer.Serialize(data),
                cancellationToken);
        }

        var service = new ManagedAppDataService(
            canonicalPath,
            () => "1.5.0",
            WriteAsync);
        AppData staleCopy = await service.LoadAppDataAsync();
        staleCopy.Beans.Add(CreateBean("Stale"));

        Task<bool> update = service.UpdateAsync(data => data.Beans.Add(CreateBean("Atomic")));
        await writeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<bool> save = service.SaveAppDataAsync(staleCopy);
        releaseWrite.SetResult();

        (await update).Should().BeTrue();
        (await save).Should().BeFalse();
        service.CurrentData.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Atomic");
        AppData persisted = JsonSerializer.Deserialize<AppData>(
            await File.ReadAllTextAsync(canonicalPath))!;
        persisted.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Atomic");
    }

    [Fact]
    public async Task SaveAppDataAsync_PreInitializationNoFileCopyCannotOverwriteLaterUpdate()
    {
        string canonicalPath = Path.Combine(_testDirectory, "pre-initialization-race.json");
        var service = new ManagedAppDataService(canonicalPath, () => "1.5.0");
        AppData staleCopy = await service.LoadAppDataAsync();

        (await service.UpdateAsync(data => data.Beans.Add(CreateBean("Atomic")))).Should().BeTrue();
        staleCopy.Beans.Add(CreateBean("Stale"));

        (await service.SaveAppDataAsync(staleCopy)).Should().BeFalse();
        service.CurrentData.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Atomic");
        (await service.LoadAppDataAsync()).Beans
            .Should().ContainSingle(bean => bean.CoffeeName == "Atomic");
    }

    [Fact]
    public async Task SaveAppDataAsync_PreInitializationNoFileCopyCannotOverwriteLegacyImport()
    {
        string canonicalPath = Path.Combine(_testDirectory, "pre-init-legacy-import.json");
        string legacyPath = Path.Combine(_testDirectory, "legacy-source.json");
        AppData legacy = AppDataFactory.CreateDefault();
        legacy.DataSchemaVersion = 1;
        legacy.Beans.Add(CreateBean("Imported"));
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(legacy));
        var preferences = new Mock<IPreferencesService>();
        preferences.Setup(service => service.GetAppDataFilePathAsync()).ReturnsAsync(legacyPath);
        var service = new ManagedAppDataService(canonicalPath, () => "1.5.0");
        AppData staleDefault = await service.LoadAppDataAsync();

        AppData imported = await service.InitializeAsync(preferences.Object);
        staleDefault.Beans.Add(CreateBean("Stale"));

        imported.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Imported");
        (await service.SaveAppDataAsync(staleDefault)).Should().BeFalse();
        service.CurrentData.Beans.Should().ContainSingle(bean => bean.CoffeeName == "Imported");
        (await service.LoadAppDataAsync()).Beans
            .Should().ContainSingle(bean => bean.CoffeeName == "Imported");
    }

    private async Task<ManagedAppDataService> CreateInitializedServiceAsync()
    {
        string canonicalPath = Path.Combine(_testDirectory, $"{Guid.NewGuid():N}.json");
        var service = new ManagedAppDataService(canonicalPath, () => "2.0.0");
        await service.InitializeAsync(Mock.Of<IPreferencesService>());
        return service;
    }

    private static BeanData CreateBean(string coffeeName)
    {
        return new BeanData
        {
            Country = "Test",
            CoffeeName = coffeeName,
            Quantity = 1,
            RemainingQuantity = 1
        };
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

    private static async Task EventuallyAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
