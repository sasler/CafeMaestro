# Settings

Settings is a short index of destinations rather than one long scroll. Every row names a section,
shows the value that section currently holds, and pushes a focused page.

## Index

| Row | Summary | Detail page |
| --- | --- | --- |
| Roasting | `First Crack off · Cooling 5 min · 0.1 g` | First Crack, cooling duration, weight precision, cooling notifications |
| Appearance | `Dark` | System / Light / Dark with a live preview |
| Data & Backups | `12 beans · 84 roasts · backed up today` | Current data, backups, automatic recovery copies, CSV transfer, Start New Data |
| Roast Levels | `7 configured` | List, add/edit/delete, Reset defaults |
| About | Current app version | Version, history, privacy, licence |

`SettingsIndexPageViewModel.OnAppearingAsync` rebuilds every summary, so returning from a detail
page shows the change that was just made. The five detail routes are registered in
`AppShell.RegisterRoutes` and named in `Navigation/Routes.cs`; none of them is a tab.

## Roasting preferences

Preferences are read and written through `IRoastPreferencesService`. The session domain snapshots
them into a roast at Start and at Drop, so **changing a preference affects subsequent roasts only** —
an active draft keeps the First Crack and cooling values it began with.

| Preference | Default | Notes |
| --- | --- | --- |
| Cooling duration | 5 minutes | Selectable from Off to 30 minutes; clamped by the service |
| Weight precision | 0.1 g | A fixed capture contract, displayed rather than editable |
| Track First Crack | Off | Enabling it adds Mark 1C, development time and DTR to future roasts |
| Cooling notifications | Off | See below |

A write that fails returns `false`; the ViewModel restores the previous value and says the
preference was not saved, so the screen never shows a setting that was never stored.

### Notification preference vs. permission

The toggle is the *app* preference. `ICoolingNotificationService.GetPermissionStateAsync` reports the
*OS* state separately as one of:

- `Unavailable` — this platform/build cannot post reminders; the toggle is disabled and says so.
- `NotDetermined` — supported, not yet asked. Turning the preference on asks then; a denial is a
  normal outcome, not an error.
- `Granted` — Android can post reminders, though battery policy may delay delivery.
- `Denied` — the preference stays as the user set it, and the page shows a *Blocked by system
  settings* chip rather than silently switching itself off.

The OS prompt is never raised at app launch. Android 13+ uses `POST_NOTIFICATIONS`; a contextual
first-Drop explanation offers the opt-in, while enabling the toggle remains an explicit way to ask.
Allowed reminders use a low-importance channel and best-effort inexact alarms. Other targets keep
the cross-platform `Unavailable` no-op behavior.

## Appearance

`ThemePreferencePolicy` owns the resolution: an explicit System/Light/Dark choice already on the
device is preserved, and only a device that has never stored one falls back to Dark. Selecting a
theme saves it and swaps the colour dictionary once — tokens, icon geometries and component styles
stay merged and re-resolve their `DynamicResource` colours, so every live page updates without
navigation being rebuilt.

## Data & Backups

The backup, restore, share, recovery-copy and CSV operations are the ones documented in
[Versioned persistence](data-persistence.md); this page moved them, it did not rewrite them.

Start New Data sits alone in a danger zone below everything else and still creates an automatic
recovery copy before replacing anything.

### Active-roast guard

Start New Data, Restore from Backup and Restore Previous Data first read
`IRoastSessionService.GetSnapshotAsync`. If the snapshot reports an active roast or that recovery is
required, the operation stops and explains that the roast must be dropped or discarded first. A
snapshot that cannot be read is treated as "a roast may be running" rather than waved through, so a
dataset replacement can never happen silently under a running batch.
