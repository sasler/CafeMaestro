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

## Shell and lifecycle

Roast is the first of exactly four root tabs: Roast, Log, Beans, and Settings. The tab bar is visible
for Setup and Handoff, and hidden for Active, Recovery, and Persistence Error through one shared
presentation policy. Root Back actions from the browsing tabs return to Roast; detail and import
destinations remain registered routes rather than tabs.

Window `Stopped` and `Resumed` handlers are registered once by `App` and target the stable
`RoastPageViewModel`. Stopping marks UI work suspended and releases display wake without changing the
persisted roast. Resuming refreshes the domain snapshot before UI ticking and wake ownership resume.
Cold startup initializes and migrates data on `LoadingPage`, presents Shell, then hands any queued
platform-neutral activation payload to its handler.

## Back-to-back flow

The first Drop freezes and commits immediately, then prioritizes a prefilled Batch 2 setup. Batch 1
continues through a five-minute cooling projection. After the second Drop, a ready Batch 1 becomes
the primary weigh-in action; otherwise both batches remain cooling and Finish session leaves those
obligations intact.

A cooling channel also offers a subordinate **Ready now** action. After confirmation, the session
stores that batch's actual cooling duration and an explicit early-completion fact, cancels its
reminder, and projects it as Needs weight without inventing a final weight or changing another
cooling batch. The state survives a restart and remains ready if the device clock rolls backward.

When more than one physical batch is ready, no batch is selected by default. Weigh-in validates a
positive final weight at 0.1 g precision and never permits more than the recorded input weight.

All Roast Console overlays use the active theme's semantic popup surface, border, corner radius and
scrim. The toolkit host itself is transparent, so it cannot add a light platform-default frame around
the themed content in dark mode.

## Recovery

A persisted active draft opens the Recovery view rather than silently continuing. A normal clock
can Keep roasting or Record drop at a corrected end time. If wall-clock rollback made the running
interval unknowable, the view requires an explicit elapsed `mm:ss`; the recovery adapter passes that
duration into the domain contract so no clamped zero or contradictory timeline is persisted.
