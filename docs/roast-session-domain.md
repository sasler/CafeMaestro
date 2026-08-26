# Roast session domain

The roast console is durable because the app persists **domain state, not screen state**.
`IRoastSessionService` owns every roast transition; ViewModels request a transition and render
the returned `RoastSessionSnapshot`. No ViewModel writes app data, moves bean quantity, or
schedules a notification itself.

## Boundary

| Type | Responsibility |
|---|---|
| `IRoastSessionService` | The only writer of `AppData.ActiveRoastSession` and roast-workflow fields |
| `IRoastQueryService` | Read-only projections: carry-forward setup, reference result, open-work queue |
| `IClock` | The single source of "now", so transitions and recovery are testable without sleeping |
| `IRoastPreferencesService` | Cooling duration, First Crack, and cooling-notification preferences |
| `ICoolingNotificationService` | Schedules and cancels the optional cooling reminder by roast id |
| `IAppDataService.UpdateAsync` / `TryUpdateAsync` | Lock-scoped read-modify-validate-write persistence |

## Stored facts versus derived state

Only facts are persisted. Everything the screen shows is derived at read time:

- **Elapsed time** = `AccumulatedElapsedSeconds + max(0, now − RunningSinceUtc)` while roasting,
  and `AccumulatedElapsedSeconds` while paused. A per-second value is never written.
- **Cooling versus Needs weight** = a stored `AwaitingWeight` status compared against
  `DroppedAtUtc + CoolingDurationSeconds`, unless `CoolingCompletedEarly` records a confirmed
  **Ready now** transition. Nothing has to run at zero, so process death, clock rollback, a denied
  notification permission, or a delayed alarm cannot strand a roast in the wrong state.

The visible timer is refreshed by a page-owned UI ticker that asks for a fresh projection. The
ticker stops with visibility; the roast does not.

## Transition invariants

| Action | Preconditions | Atomic effects |
|---|---|---|
| Start | No active draft; bean exists; valid temperature and weight | Create or reuse the session and persist the draft **before** the UI shows Roasting |
| Pause | Draft is Roasting | Fold `now − RunningSinceUtc` into the accumulated total, clear the running anchor |
| Resume | Draft is Paused | Re-anchor to now, preserving accumulated time |
| Mark 1C | Active, enabled at Start, not already marked | Store the current whole second once |
| Drop | Active Roasting or Paused | Append the roast, decrement the bean, clear the draft, advance the batch number — one mutation |
| Retry Drop | A previous Drop write failed | Reuse the same draft id; no duplicate record and no second decrement |
| Ready now | An unresolved batch is still cooling | Persist the actual duration plus early-completion fact; cancel its reminder; keep final weight empty |
| Save weight | `AwaitingWeight`; above 0 and not above the batch weight; normalized to 0.1 g | Set the final weight, level and `Complete`; cancel the reminder |
| Mark Unweighed | `AwaitingWeight` | Set `Unweighed` without inventing a weight or loss; cancel the reminder |
| Finish session | No active draft | Clear only `ActiveRoastSession`; cooling and needs-weight roasts stay open |

The draft id becomes the final `RoastData.Id`, which is what makes Drop idempotent: a retry after a
failed write and a second tap after a committed write both resolve to the same single record.

Notification scheduling happens **after** persistence. If it fails, the roast stays saved and the
result carries a non-blocking warning.

## Recovery

A persisted draft found on a cold launch sets `RoastSessionSnapshot.RequiresRecovery`, so the app
asks what physically happened instead of silently resuming:

- **Still going** folds the closed-app interval into the accumulated total and re-anchors to now.
- **Ended at…** performs a Drop at a corrected time, rejected if it falls outside the roast.
- **Discard** clears the draft, optionally logging a failed roast and optionally consuming beans.

If the device clock moved behind the roast's own anchors, confirming "still going" rebases those
anchors onto the current clock. Elapsed time already earned lives in the accumulated total, so it
never goes negative and never stalls. Time-zone changes need no handling at all, because every
anchor is stored as UTC and only presentation converts to local time.

## Carry-forward

1. Batch 2 from the handoff copies the just-dropped batch's bean, temperature and batch weight.
2. Starting from Roast pre-fills from the newest non-discarded roast for the selected bean, even
   if that roast still needs a weight.
3. The reference result is the newest **Complete** roast. Cooling, needs-weight, unweighed and
   discarded rows never displace it; the newer pending batches are reported as a count instead.
4. Legacy and imported rows without a `BeanId` may match a bean by an exact display snapshot, but
   only when that name identifies exactly one bean. Ambiguous rows stay unlinked.
5. Renaming a bean never rewrites a historical snapshot.

## Tests

`CafeMaestro.Tests/Services/RoastSessionServiceTests.cs` and `RoastQueryServiceTests.cs` run the
domain against the real persistence stack over a temporary file, with a fake clock and injected
write failures.
