# Roast Console

The Roast Console is CafeMaestro's simple-by-default live workflow. Setup contains exactly Bean,
Temperature, and Batch weight. Selecting a bean loads suggestions from its newest usable roast while
the reference card remains the newest completed result.

## State ownership

- `IRoastSessionService` persists Start, Pause, Resume, First Crack, Drop, reset/correction, discard,
  weigh-in, session completion, and recovery transitions.
- `RoastPageViewModel` renders immutable snapshots and requests transitions. It does not mutate
  `AppData`, inventory, Shell, device display, or popup APIs.
- `RoastPage` owns the 250 ms visual ticker and invalidates the instrument while visible. Elapsed
  truth is recomputed from persisted UTC anchors.
- `IOverlayService` is the only Roast Console boundary that reaches the current Shell to present
  registered CommunityToolkit MVVM popups.
- `IDisplayWakeService` keeps the display awake only while the effective phase is Roasting and the
  page is foregrounded.

## Back-to-back flow

The first Drop freezes and commits immediately, then prioritizes a prefilled Batch 2 setup. Batch 1
continues through a five-minute cooling projection. After the second Drop, a ready Batch 1 becomes
the primary weigh-in action; otherwise both batches remain cooling and Finish session leaves those
obligations intact.

When more than one physical batch is ready, no batch is selected by default. Weigh-in validates a
positive final weight at 0.1 g precision and never permits more than the recorded input weight.

## Recovery

A persisted active draft opens the Recovery view rather than silently continuing. A normal clock
can Keep roasting or Record drop at a corrected end time. If wall-clock rollback made the running
interval unknowable, the view requires an explicit elapsed `mm:ss`; the recovery adapter passes that
duration into the domain contract so no clamped zero or contradictory timeline is persisted.
