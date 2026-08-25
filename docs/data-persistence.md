# Versioned Persistence

CafeMaestro stores its canonical dataset as JSON in private app storage. `AppData.DataSchemaVersion`
identifies the persisted contract; files without that property are treated as schema version 1.

## Loading and migration

- Migrations implement `IAppDataMigration` and run sequentially before normalization and validation.
- The version 1 to 2 migration adds the roast-session storage shape and maps legacy roast history
  without changing historical duration, notes, first crack, roast level, identifiers, or dates.
- A legacy roast links to a bean only when its display snapshot has exactly one ordinal match.
- Positive final weights become `Complete`; zero or absent final weights become `AwaitingWeight` and
  are ready to weigh immediately through a zero-second cooling duration.
- Legacy `RoastDate` values become UTC `DroppedAtUtc` anchors. UTC values remain unchanged; Local and
  Unspecified values are interpreted using the device local time zone before conversion to UTC.
- Before the first canonical migration write, CafeMaestro copies the original JSON bytes into the
  existing safety-backup directory using temporary-file publication. Loading current-schema data
  does not rewrite or back it up again.
- A malformed file referenced by the retired custom-path preference is also copied byte-for-byte
  into the safety-backup directory before import fails, leaving the original source unchanged and
  making the raw recovery artifact available for export.
- A schema newer than the app supports is rejected without modifying or backing up the source file.
- Invalid original data remains discoverable as a raw recovery artifact. It cannot replace active
  data, but Settings can save its unchanged bytes for manual repair or support.
- A failed canonical load puts Settings into an explicit recovery-required state. Normal Save Backup
  validates the canonical file instead of exporting the empty fallback; Share Backup remains the
  direct raw-file preservation path. Save and Update operations are blocked until a canonical read
  succeeds or the recovery flow deliberately replaces the file, preventing a cached graph from
  overwriting data that still needs recovery. Explicit Restore and Start New operations preserve the
  unreadable canonical bytes atomically before activating validated replacement data.
- Current-schema loads validate cooling anchors, completion fields, and the full active-session
  storage graph without applying legacy workflow repair before the data can enter the cache. Cooling
  projections must remain inside the supported date range. Active-draft temperatures and weights
  must be finite, and non-finite or negative final weights are rejected at roast add and edit
  boundaries before level lookup or persistence.
- Current roasts require both the immutable `BeanDisplaySnapshot` and the compatibility `BeanType`
  used by existing search, export, and summary paths. An active draft must reference a bean in the
  persisted inventory.

## Atomic mutation contract

`IAppDataService.UpdateAsync` serializes each read, mutation, normalization, validation, and atomic
file replacement under the managed data lock. The mutation delegate receives a private copy rather
than the live cache. Invalid changes, mutation exceptions, and write failures leave both cache and
disk unchanged and publish no `DataChanged` event. A successful commit replaces the cache and emits
exactly one event after the lock is released. Concurrent commit notifications are queued in commit
order and dispatched serially, while handlers can still request reentrant mutations safely.
Subscriber failures are isolated from the already-committed mutation and from other subscribers.
`DataChanged` handlers must be synchronous; async-void subscriptions are rejected so exceptions
cannot escape after dispatch. Handlers can start explicit observed background tasks when needed.
Notification suspension and dispatch admission are decided under the same queue lock. A suspension
drops notifications that have not yet been admitted; a handler already admitted
before the scope begins is allowed to finish.
Initialization and reload notifications use this same ordered queue, so an older loaded snapshot
cannot publish after a newer atomic commit or run concurrently with its handler.

`TryUpdateAsync` lets a mutation reject its candidate before validation or writing. This is used for
conditional edits so a missing entity does not change `LastModified`, advance the revision, or emit
an event.

Copies returned by `LoadAppDataAsync` carry an in-memory persistence revision. A full-dataset save is
rejected if an atomic mutation committed after that copy was read, preventing a stale legacy save
from overwriting newer data. All Save and Update writes use the same lock, and events are published
after releasing it so handlers can safely request another mutation.
An uninitialized service also rejects a full save when the canonical file already exists. Callers
must load through the schema and migration gate first; backup replacement does this before creating
its safety copy, so unsupported data cannot be replaced by the fallback default graph.
Every disk reload or legacy import that changes persisted content advances the in-memory revision;
read-only reloads retain it. Backup replacement carries the loaded revision into its save, providing
compare-and-swap behavior: a concurrent commit makes the replacement fail instead of losing data
that was not present in the safety copy.
Successful replacement returns a freshly loaded committed graph, including its current revision,
timestamp, and app version.

The service preserves the established canonical path, legacy-path import, tolerant JSON property
casing, and temporary-file replacement behavior. Numeric JSON serialization continues to use
`System.Text.Json` invariant-culture semantics.
