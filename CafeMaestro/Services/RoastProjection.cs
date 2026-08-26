using CafeMaestro.Models;

namespace CafeMaestro.Services;

/// <summary>
/// Pure projections from stored facts plus a clock reading. Nothing here writes, so the same
/// rules produce the same answers on a cold launch, after a time-zone change, or mid-session.
/// </summary>
internal static class RoastProjection
{
    /// <summary>
    /// A single roast longer than this is treated as a clock anomaly rather than a real batch.
    /// </summary>
    internal const double MaxPlausibleRoastSeconds = 6 * 60 * 60;

    /// <summary>Elapsed roast time derived from the persisted anchors; never negative.</summary>
    internal static double ElapsedSeconds(ActiveRoastDraft draft, DateTimeOffset asOfUtc)
    {
        if (draft.Phase == ActiveRoastPhase.Paused || !draft.RunningSinceUtc.HasValue)
        {
            return Math.Max(0, draft.AccumulatedElapsedSeconds);
        }

        double running = (asOfUtc - draft.RunningSinceUtc.Value).TotalSeconds;
        return Math.Max(0, draft.AccumulatedElapsedSeconds) + Math.Max(0, running);
    }

    internal static bool IsElapsedImplausible(ActiveRoastDraft draft, DateTimeOffset asOfUtc)
    {
        if (draft.RunningSinceUtc.HasValue && asOfUtc < draft.RunningSinceUtc.Value)
        {
            return true;
        }

        return asOfUtc < draft.StartedAtUtc ||
            ElapsedSeconds(draft, asOfUtc) > MaxPlausibleRoastSeconds;
    }

    /// <summary>
    /// Whether wall-clock rollback made the current running interval unknowable. Recovery must
    /// collect an elapsed duration instead of treating the clamped projection as earned time.
    /// </summary>
    internal static bool RequiresCorrectedElapsed(
        ActiveRoastDraft draft,
        DateTimeOffset asOfUtc) =>
        asOfUtc < draft.StartedAtUtc ||
        draft.RunningSinceUtc is DateTimeOffset runningSinceUtc && asOfUtc < runningSinceUtc;

    internal static ActiveRoastSnapshot ToSnapshot(ActiveRoastDraft draft, DateTimeOffset asOfUtc) =>
        new()
        {
            Id = draft.Id,
            SessionId = draft.SessionId,
            BatchNumber = draft.BatchNumber,
            BeanId = draft.BeanId,
            BeanDisplaySnapshot = draft.BeanDisplaySnapshot,
            Temperature = draft.Temperature,
            BatchWeight = draft.BatchWeight,
            Phase = draft.Phase,
            StartedAtUtc = draft.StartedAtUtc,
            ElapsedSeconds = ElapsedSeconds(draft, asOfUtc),
            FirstCrackElapsedSeconds = draft.FirstCrackElapsedSeconds,
            FirstCrackEnabled = draft.FirstCrackEnabled,
            CoolingDurationSeconds = draft.CoolingDurationSeconds,
            IsElapsedImplausible = IsElapsedImplausible(draft, asOfUtc),
            RequiresCorrectedElapsed = RequiresCorrectedElapsed(draft, asOfUtc)
        };

    /// <summary>
    /// Stored status combined with the clock. Awaiting weight reads as Cooling before the
    /// readiness timestamp and Needs weight after it, or immediately when the user explicitly
    /// released the batch, so no write is required at zero.
    /// </summary>
    internal static RoastEffectiveStatus EffectiveStatus(RoastData roast, DateTimeOffset asOfUtc) =>
        roast.CompletionStatus switch
        {
            RoastCompletionStatus.Complete => RoastEffectiveStatus.Complete,
            RoastCompletionStatus.Unweighed => RoastEffectiveStatus.Unweighed,
            RoastCompletionStatus.Discarded => RoastEffectiveStatus.Discarded,
            _ => roast.CoolingCompletedEarly || asOfUtc >= ReadyToWeighAtUtc(roast)
                ? RoastEffectiveStatus.NeedsWeight
                : RoastEffectiveStatus.Cooling
        };

    internal static DateTimeOffset ReadyToWeighAtUtc(RoastData roast) =>
        roast.ReadyToWeighAtUtc ?? DroppedAtUtc(roast);

    internal static DateTimeOffset DroppedAtUtc(RoastData roast) =>
        roast.DroppedAtUtc ?? V1ToV2AppDataMigration.ConvertLegacyRoastDate(roast.RoastDate);

    internal static RoastWorkItem ToWorkItem(RoastData roast, DateTimeOffset asOfUtc)
    {
        DateTimeOffset droppedAt = DroppedAtUtc(roast);
        DateTimeOffset readyAt = ReadyToWeighAtUtc(roast);
        RoastEffectiveStatus status = EffectiveStatus(roast, asOfUtc);
        return new RoastWorkItem
        {
            RoastId = roast.Id,
            SessionId = roast.SessionId,
            BatchNumber = roast.BatchNumber,
            BeanId = roast.BeanId,
            BeanDisplaySnapshot = string.IsNullOrWhiteSpace(roast.BeanDisplaySnapshot)
                ? roast.BeanType
                : roast.BeanDisplaySnapshot,
            Temperature = roast.Temperature,
            BatchWeight = roast.BatchWeight,
            DroppedAtUtc = droppedAt,
            ReadyToWeighAtUtc = readyAt,
            RemainingCoolingSeconds = status == RoastEffectiveStatus.Cooling
                ? Math.Max(0, (readyAt - asOfUtc).TotalSeconds)
                : 0,
            Status = status,
            TotalSeconds = roast.TotalSeconds,
            Notes = roast.Notes,
            Summary = roast.Summary,
            RoastLevelName = roast.RoastLevelName
        };
    }

    /// <summary>Cooling is pinned first, then ready work; each status is oldest first.</summary>
    internal static List<RoastWorkItem> OpenWork(AppData data, DateTimeOffset asOfUtc) =>
        (data.RoastLogs ?? [])
            .Where(roast => roast.CompletionStatus == RoastCompletionStatus.AwaitingWeight)
            .Select(roast => ToWorkItem(roast, asOfUtc))
            .OrderBy(item => item.Status == RoastEffectiveStatus.Cooling ? 0 : 1)
            .ThenBy(item => item.DroppedAtUtc)
            .ThenBy(item => item.BatchNumber ?? int.MaxValue)
            .ToList();

    /// <summary>
    /// Whether a roast belongs to a bean. Rows without a BeanId may match on an exact display
    /// snapshot, but only when that name identifies exactly one bean in the current inventory.
    /// </summary>
    /// <remarks>
    /// A stored BeanId is authoritative, including an orphaned or empty value; the display-name
    /// fallback is reserved for genuinely legacy rows whose identity is absent. Ambiguous names
    /// intentionally resolve to no bean rather than guessing between duplicate inventory entries.
    /// </remarks>
    internal static bool BelongsToBean(
        RoastData roast,
        BeanData bean,
        IReadOnlyList<BeanData> allBeans)
    {
        BeanData? resolved = ResolveBean(roast, allBeans);
        return resolved?.Id == bean.Id;
    }

    /// <summary>
    /// Resolves a stored roast to one current bean without silently reassigning its history.
    /// Stable IDs always win. Only a missing ID may use an exact, unique display snapshot.
    /// </summary>
    internal static BeanData? ResolveBean(
        RoastData roast,
        IReadOnlyList<BeanData> allBeans)
    {
        ArgumentNullException.ThrowIfNull(roast);
        ArgumentNullException.ThrowIfNull(allBeans);

        if (roast.BeanId is Guid stableBeanId)
        {
            return stableBeanId == Guid.Empty
                ? null
                : allBeans.FirstOrDefault(candidate => candidate.Id == stableBeanId);
        }

        string snapshot = string.IsNullOrWhiteSpace(roast.BeanDisplaySnapshot)
            ? roast.BeanType
            : roast.BeanDisplaySnapshot;
        if (string.IsNullOrWhiteSpace(snapshot) ||
            allBeans.Count(candidate =>
                string.Equals(candidate.DisplayName, snapshot, StringComparison.Ordinal)) != 1)
        {
            return null;
        }

        return allBeans.First(candidate =>
            string.Equals(candidate.DisplayName, snapshot, StringComparison.Ordinal));
    }
}
