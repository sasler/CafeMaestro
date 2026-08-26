# CSV import

Beans and roast logs used to have near-identical import pages. They now share one guided flow.
Only the destination rules differ, and those live behind an adapter.

## Flow

```text
Select type and file  →  Map columns  →  Review  →  Result
```

`ImportPageViewModel.Step` is the single presentation state. Each step owns one primary action, and
the action states what it will do — `IMPORT 22 VALID BEANS`, never a bare "Import".

| Step | Contents | Gate to continue |
| --- | --- | --- |
| Select file | Two type cards and Browse CSV | A file with headers and at least one data row |
| Map columns | Auto-map summary, expanded Required fields, collapsed Optional fields | Every required field mapped |
| Review | Valid / needs attention / total counts, five-row preview, expandable excluded rows | At least one valid row |
| Result | Imported and skipped counts, up to five concrete errors, destination action | — |

Context decides the type. `Routes.Import` takes `ImportPageViewModel.KindParameter`, so Beans →
Import, Log → Import, and both Data & Backups actions arrive with the right `ImportKind` already
selected. The type cards stay visible and switchable, but no entry point asks a question it already
knows the answer to.

## The source file is read-only

`IUserFileService.PickFileAsync` copies the selection into the app cache and returns that local
path, which is what the flow reads. Import never writes to, moves, or deletes the file the user
chose. The cached working copy is released when the user leaves the flow, not when the page merely
disappears, so navigating within the flow keeps the file and mapping intact.

Leaving after only choosing a file needs no confirmation — selecting a file creates nothing. Leaving
after editing the mapping asks first.

## Adapters own everything type-specific

```csharp
public interface IImportAdapter
{
    ImportKindDescriptor Descriptor { get; }
    IReadOnlyList<ImportFieldDefinition> Fields { get; }
    Task<IImportSession> CreateSessionAsync(CancellationToken cancellationToken = default);
}
```

| Adapter | Required fields | Duplicate policy |
| --- | --- | --- |
| `BeanImportAdapter` | Coffee name, Country | Same coffee name, country and variety already in inventory |
| `RoastImportAdapter` | Date, Coffee bean, Batch weight | Same bean, day, batch weight, temperature and elapsed time — the signature the log already uses to strip duplicates after a restore |

A session judges rows one at a time against the existing data *and* the rows already accepted in
this import, so a file that repeats itself is caught as well as a file that repeats history.
Rejected rows are never coerced: they are excluded, listed with the reason, and reported in the
result.

Auto-mapping is shared. `ImportHeaderMatcher` scores each header against a field's display name,
property key, exact aliases, and keywords; a field with no plausible header is left unmapped rather
than guessed at.

## One atomic commit

`ImportService.CommitAsync` appends every accepted row inside a single
`IAppDataService.UpdateAsync`:

- The whole accepted set lands together or not at all.
- Exactly one `DataChanged` fires, so every affected surface refreshes once.
- A refused mutation writes nothing and keeps the reviewed plan, so Retry does not ask for the file
  again.

Partial row failures do not undo valid rows — those rows were excluded before the mutation ran, not
rolled back after it.

## Parsing

Storage is invariant, so parsing tries invariant culture first and falls back to the device culture
for files exported by locale-aware spreadsheets. Unit suffixes (`240 g`, `1.5 kg`, `218 °C`,
`13.8 %`) are stripped before the number is read. Roast durations are `mm:ss`; a colon is never read
as hours, and a bare number is total seconds.

Loss percentage is a derived column: it only reconstructs a final weight that the file did not
supply. It never overrides one that it did.

## Imported roasts join the work queue honestly

`NewRoastDefaults.Apply` gives an imported roast the same workflow fields a manually saved one gets.
A roast with no final weight becomes Awaiting weight with a zero cooling duration, so
`ReadyToWeighAtUtc` equals the drop time and the row appears in the Roast Log as ready to weigh
rather than pretending to cool. Only supported metadata marks a record Unweighed instead.
