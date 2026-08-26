# Changelog

All notable changes to CafeMaestro will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] - Complete Architecture Refactor
### Added
- One guided CSV import flow — choose type and file, map columns, review every row, then import — replacing the separate Bean and Roast import pages
- Beans, Roast Log and Data & Backups open that flow with Beans or Roast Logs already selected, so no contextual action asks a question it already knows the answer to
- Review reports valid, needs-attention and total counts, names every excluded row and its reason, and states exactly what the import will write before anything is saved
- Accepted rows are committed in one atomic app-data mutation that raises a single data-changed notification, so every affected surface refreshes once
- `IImportService` with per-destination `IImportAdapter` implementations owning field definitions, row validation, duplicate policy and commit behaviour
- Imported roasts without a final weight arrive as Awaiting weight and ready to weigh now, so historical rows land in the Roast Log work queue as actionable
- `docs/import.md` describing the flow states, the adapter contract and the atomic commit
- Settings is now a short index of destinations — Roasting, Appearance, Data & Backups, Roast Levels and About — each row showing the value it currently holds and refreshing that summary when you return
- Roasting preferences page for First Crack tracking, cooling duration, weight precision and cooling notifications, with a note that changes apply to future roasts only
- Cooling-notification state reports the app preference and the OS permission separately, so a reminder that will not be delivered says why instead of appearing to be on
- `ICoolingNotificationService.GetPermissionStateAsync`/`RequestPermissionAsync` with an `Unavailable` result on platforms that cannot post reminders, giving the Android work a stable contract to bind to
- Appearance page with System/Light/Dark and a live sample card that previews the selected theme's surfaces, status colours and instrument type
- About page carrying version, first-installed version, version history, privacy and licence
- `docs/settings.md` describing the index, the preference contracts and the active-roast data guard
- Roast Log work queue with Cooling and Needs weight batches pinned above searchable Complete, Unweighed, and Discarded history
- Accessible shared roast status cards, a focused roast-detail route, explicit multi-batch weigh-in selection, and honest missing-result values
- App-scoped Window stop/resume recovery that releases display wake, pauses UI ticking, and refreshes the persisted roast snapshot without retaining transient pages
- A platform-neutral queued activation payload handoff that runs only after data initialization and Shell presentation, ready for Android cooling reminders
- Responsive Beans inventory with in-memory search, availability filters, quantity-first low/out-of-stock states, cached-row retry behavior, and a 600 dp list/detail layout
- Bean detail with inventory facts, the newest completed roast, recent incomplete work, stable-identity Edit/Delete actions, and Start Roast navigation into the prefilled confirmation flow
- Grouped Add/Edit Bean cards for identity, details, inventory, and notes using the shared Direction B visual system
- Direction B Roast Console vertical slice with three-field prefilled setup, previous completed-result reference, persisted elapsed sweep, Pause/Resume, one-tap Drop, cooling handoff, and back-to-back Batch 2 setup
- Typed Setup, Active, Handoff, Recovery, and Persistence Error roast views plus MVVM popup flows for explicit batch selection, 0.1 g weigh-in, discard, navigation confirmation, reset, and time correction
- Five-minute cooling channels that become Ready to weigh without negative countdowns, with first-drop Batch 2 and second-drop Batch 1 action priority
- Display-wake ownership that follows only an actively roasting foreground page and is always released on pause, drop, recovery, or page exit
- Focused Roast Console tests for representative presentation transitions, repeating elapsed/cooling geometry boundaries, weight validation, reset, and drop-time correction
- `IRoastSessionService`: the single writer of roast-session state, owning Start, Pause, Resume, Mark 1C, Drop, Discard, weigh-in, Mark Unweighed, Finish session and recovery as lock-scoped atomic mutations
- Bean inventory now moves inside the same mutation that appends the roast, so a failed write can never consume beans without a matching log entry, and a retried or double-tapped Drop applies exactly once
- `IRoastQueryService` projections: carry-forward setup values, the newest **completed** result as the reference roast, and status-prioritized open work ordered oldest first within each state
- `IClock` abstraction so every transition, elapsed-time projection and recovery path is deterministic and testable without sleeping
- Elapsed time derived from persisted UTC anchors rather than an in-memory ticker, so pause/resume, backgrounding, process death and time-zone changes all recompute the same value
- Cooling and Needs weight derived from the drop timestamp plus the roast's own cooling snapshot, so no write is required when cooling reaches zero
- Cold-launch recovery for a persisted roast: still going, ended at a corrected time, or discard, with a rolled-back device clock rebased instead of producing negative or stalled elapsed time
- `IRoastPreferencesService` with the settled defaults — five-minute cooling, 0.1 g weight precision, First Crack off — snapshotted into a roast at Start so later preference changes never rewrite an active draft or history
- `ICoolingNotificationService` contract with a cross-platform no-op implementation; scheduling happens after persistence and a failure downgrades to a non-blocking warning rather than rolling back a saved Drop
- Carry-forward keyed by `BeanId`, with legacy rows matched by an exact display snapshot only when that name identifies exactly one bean; ambiguous rows stay unlinked
- `docs/roast-session-domain.md` documenting the domain boundary, transition invariants and recovery rules
- Versioned JSON persistence with sequential schema migrations and recovery copies before in-place upgrades
- Lock-scoped atomic data mutations that validate and replace the complete dataset before publishing one change event
- Durable roast-session and completion fields required by the back-to-back roasting workflow
- Discoverable raw recovery copies that can be exported unchanged when invalid legacy data cannot be activated safely
- Direction B visual system: shared design tokens, semantic dark/light themes, component styles and a vector icon set
- `Resources/Styles/DesignTokens.xaml` with spacing, radii, type scale, control sizing, icon sizes and responsive breakpoints
- `Resources/Styles/ComponentStyles.xaml` with card, field, action-bar, status-chip, icon-button, empty/error/loading and focus styles
- `Resources/Styles/IconGeometries.xaml` with 25 glyphs on a 24-unit grid, a fixed 1.75 dp rendered stroke, round caps and joins
- Reusable `IconView` control that renders a glyph at 18/24/32 dp in any semantic colour, so icons follow the theme
- Bean-plus New batch glyph, Reset-only circular arrow, hollow mechanical Settings cog and filled First Crack bolt
- Four monochrome Shell tab assets for platform-selected/unselected tinting, Android screenshot-verified and Windows build-verified
- `ThemePreferencePolicy`, making dark the fallback only when no preference has ever been stored
- Debug-only component gallery page rendering every component and glyph in both themes
- `docs/visual-system.md` documenting the shared visual system and how to consume it
- Reusable phase glyph, word and channel-edge status variants, including neutral cards with semantic status edges
- Platform tabular-font tokens for stable times, weights, percentages, quantities and counts
- Tests covering theme key parity, WCAG text contrast in both themes, live system-theme behavior, semantic icon fallback, touch targets, resource reachability, rendered stroke weight and geometry invariants
- App-managed automatic saving to private storage, with no setup prompt
- Validated JSON backup previews and five-item automatic safety-backup history
- Android Storage Access Framework integration for opening and saving JSON/CSV documents
- Share functionality: share data file (JSON) and roast log (CSV) via OS share sheet
- IShareService interface and ShareService implementation using MAUI Share API
- Share Data File and Share Roast Log buttons on Settings page
- Support for saving roasts without final weight (optional field for batch roasting workflows)
- "Pending" roast level display for roasts awaiting final weight entry
- HasFinalWeight and WeightLossDisplay computed properties on RoastData model
- 8 new unit tests covering share commands and flexible roast saving

### Fixed
- Start New Data and both Restore paths are blocked with an explanation while a roast is active or awaiting recovery, so a dataset replacement can no longer land silently under a running batch
- Roast overlays now resolve and bind their own view and ViewModel: popup query attributes left the Weigh In and batch-choice sheets unbound in Release builds, so they appeared with no batch, no title, and unresponsive buttons on device
- Persistence change notifications now return to the app synchronization context before updating UI-bound subscribers
- Loading screen referenced `cafemaestro_logo.svg` and a non-existent `Primary` colour, so the logo and spinner never picked up their intended appearance
- Repeated theme switches no longer stack theme dictionaries: dictionaries added in code have no `Source`, so they were never removed again
- Android system chrome now follows semantic theme tokens with contrast-safe status/navigation icons and no template purple
- Small Bean/New batch, Drop, Cooling, Weigh and Drum glyphs no longer collapse into ambiguous silhouettes
- Android file operations now read picker results through streams, including content:// documents
- Removed the delayed settings request-message race that produced No response was received errors
- Invalid backup JSON is rejected without modifying active data or the selected source

### Changed
- CSV import now reads and validates the whole file before the Review step, so the counts shown are the counts imported rather than a five-row sample
- Import parses numbers and dates with invariant culture first and only then falls back to the device culture, matching how CafeMaestro stores them
- Version bumped to 1.11.0
- The single long Settings page is replaced by an index with focused, Back-navigable detail pages; data/backup, roast-level and theme behavior moved without changing the services behind them
- Data & Backups now isolates Start New Data in a danger zone below everything else, and the roast persistence-error escape hatch opens that page directly instead of the Settings index
- Final-weight entry and corrections now use the focused Weigh In flow; generic roast editing is limited to mutable recorded details
- Version bumped to 1.9.0
- Version bumped to 1.8.0
- Roast is now the launch/home destination in an exact four-tab Shell ordered Roast, Log, Beans, Settings; focused active/recovery states centrally hide the tab bar
- Version bumped to 1.7.0
- Bean-to-roast setup now passes a stable `BeanId` and performs final carry-forward lookup through `IRoastQueryService`, so renames never break historical linkage
- The Roast page now consumes immutable `IRoastSessionService` snapshots instead of owning timer or persistence truth; final weight is captured only through focused weigh-in after cooling
- Version bumped to 1.5.0
- Legacy roast records now preserve display snapshots, link exact unique beans, and distinguish completed from awaiting-weight results during migration
- Windows test hosts now restore the app's Windows graph directly while the app keeps Android and host-appropriate Apple targets
- Rebuilt DarkTheme and LightTheme around identical semantic colour keys; pre-redesign key names remain as aliases so existing pages keep rendering
- Dark is now the default appearance for a new install; an explicit System/Light/Dark choice is always preserved
- System theme changes now apply live while System is selected; explicit Light and Dark choices remain stable
- Light-theme surfaces and Roast, Attention and Danger channels now remain perceptually distinct with warmer supporting neutrals
- Shell tabs use the new monochrome Direction B assets
- Version bumped to 1.4.0
- Redesigned Settings data management with clear, labeled, accessible actions
- Restore and Start New now require confirmation and create an automatic recovery copy first
- Roast-log export writes to streams so Android document URIs never reach File.* APIs
- Version bumped to 1.3.0
- Bean quantity validation is now warning-only (no longer blocks timer start or saving)
- Final weight field is now optional on the Roast page
- Roast log displays "Pending" for weight loss and roast level when final weight is not yet entered
- CSV export shows "Pending" for incomplete roasts instead of "0.0%"
- RoastDataService handles Pending roast level for roasts without final weight

### Removed
- `BeanImportPage`, `RoastImportPage`, their ViewModels and routes, plus the per-row `ImportBeansFromCsvAsync`/`ImportRoastsFromCsvAsync` service methods they relied on
- The redundant Home page, ViewModel, route, DI registrations, tests, and icon asset
- Custom live-data file locations and the first-run storage-location prompt
- Broad Android storage and media permissions that are unnecessary with the system document picker

## [1.1.0] - Complete Architecture Refactor
### Added
- GitHub Actions CI workflow for automated build and test on PRs
- Comprehensive README.md with architecture docs, build commands, and CI badge
- Rewritten copilot-instructions.md reflecting new MVVM architecture and conventions
- xUnit test project with 87+ unit tests covering models, services, and ViewModels
- Service interfaces for all services (IAppDataService, IBeanDataService, IRoastDataService, etc.)
- Model validation with `Validate()` and `IsValid` on BeanData, RoastData, RoastLevelData
- CsvParserService extracting shared CSV parsing from Bean/RoastDataService
- NavigationService with centralized route constants (Routes.cs)
- AlertService for ViewModel-driven dialog interactions
- ViewModels for all pages using CommunityToolkit.Mvvm (ObservableObject, ObservableProperty, RelayCommand)
- Import support models for column mapping and preview data

### Changed
- Upgraded from .NET 9 to .NET 10 MAUI with CommunityToolkit.Maui 14.0.1
- Added CommunityToolkit.Mvvm 8.4.2 for MVVM source generators
- Replaced manual ServiceProvider resolution with constructor injection across all pages
- Replaced reflection-based event suppression with IDisposable SuspendNotifications pattern
- Moved RoastLevelViewModel from Models/ to ViewModels/, converted to ObservableObject
- Converted all page code-behind to proper MVVM with ViewModels
- Replaced hardcoded colors in XAML with theme resource references
- Replaced hardcoded Shell navigation strings with Routes constants

### Removed
- ~4,000+ lines of duplicated code-behind logic (moved to ViewModels)
- ~370 lines of duplicated CSV parsing code (consolidated into CsvParserService)
- 83 lines of unnecessary IConvertible boilerplate from RoastLevelViewModel
- Reflection-based event manipulation in RoastDataService
- Manual INotifyPropertyChanged implementations

## [1.1.0] - 2025-05-07
### Added
- Version tracking functionality using .NET MAUI's built-in capabilities
- CHANGELOG.md to track version history
- Version history information in Settings page

## [1.0.0] - 2025-05-07
### Added
- Initial release of CafeMaestro
- Core coffee roasting tracking functionality
- Bean inventory management
- Roast logging and timing capabilities
- Light and dark theme support
