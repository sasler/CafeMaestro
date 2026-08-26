# Roast Log Work Queue

The Roast Log is both a durable work queue and searchable history. `IRoastQueryService` supplies all
effective status and ordering; the page never infers readiness from stored values in a converter.

## Queue and history

- `OpenWork` pins Cooling batches before Needs weight batches, oldest first within each status.
- `History` contains Complete, Unweighed, and Discarded records, newest first.
- A single page-owned one-second ticker asks the query service for a fresh time projection. List cells
  own no timers, events, or storage reads.
- Search is cancellable and debounced over the in-memory projections.

Cards identify a batch by bean snapshot, batch number, local drop time, input weight, temperature,
and roast duration. Status always uses a word and card/edge shape in addition to semantic colour.
Unweighed and Discarded records display an em dash for output, loss, and level rather than zero.

## Actions

- One ready batch opens Weigh In directly. Two or more ready batches first open an unselected batch
  chooser listing batch number, input weight, and drop time, so no result can be assigned by position
  or recency. Continue stays disabled until a batch is chosen, and the chosen row says `SELECTED`
  rather than relying on colour alone.
- Overlays are resolved and bound by `OverlayService` itself. Popup query attributes are not used:
  they arrive unbound in trimmed Release builds, which strips the sheets of their batch and title.
- Final-weight entry and corrections use the same Weigh In sheet and session-domain mutation.
- The generic edit page changes only mutable recorded details: temperature, roast time, First Crack,
  and notes. It does not rewrite batch identity, input weight, session identity, or final weight.
- Detail and list deletion name the bean and date before deleting.
- Import remains the contextual existing Roast Import route until the unified import ticket replaces
  it. CSV export is available directly from the Log header.

The page subscribes to app-data changes only while visible. Weigh-in, edits, and deletion therefore
publish one persistence event and trigger one projection refresh without a second manual reload.
