# Changelog

All notable changes to CafeMaestro will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] - Complete Architecture Refactor
### Added
- Direction B Roast Console vertical slice with three-field prefilled setup, previous completed-result reference, persisted elapsed sweep, Pause/Resume, one-tap Drop, cooling handoff, and back-to-back Batch 2 setup
- Typed Setup, Active, Handoff, Recovery, and Persistence Error roast views plus MVVM popup flows for explicit batch selection, 0.1 g weigh-in, discard, navigation confirmation, reset, and time correction
- Five-minute cooling channels that become Ready to weigh without negative countdowns, with first-drop Batch 2 and second-drop Batch 1 action priority
- Display-wake ownership that follows only an actively roasting foreground page and is always released on pause, drop, recovery, or page exit
- Focused Roast Console tests for representative presentation transitions, repeating elapsed/cooling geometry boundaries, weight validation, reset, and drop-time correction
- `IRoastSessionService`: the single writer of roast-session state, owning Start, Pause, Resume, Mark 1C, Drop, Discard, weigh-in, Mark Unweighed, Finish session and recovery as lock-scoped atomic mutations
- Bean inventory now moves inside the same mutation that appends the roast, so a failed write can never consume beans without a matching log entry, and a retried or double-tapped Drop applies exactly once
- `IRoastQueryService` projections: carry-forward setup values, the newest **completed** result as the reference roast, and the open-work queue ordered oldest drop first
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
- Persistence change notifications now return to the app synchronization context before updating UI-bound subscribers
- Loading screen referenced `cafemaestro_logo.svg` and a non-existent `Primary` colour, so the logo and spinner never picked up their intended appearance
- Repeated theme switches no longer stack theme dictionaries: dictionaries added in code have no `Source`, so they were never removed again
- Android system chrome now follows semantic theme tokens with contrast-safe status/navigation icons and no template purple
- Small Bean/New batch, Drop, Cooling, Weigh and Drum glyphs no longer collapse into ambiguous silhouettes
- Android file operations now read picker results through streams, including content:// documents
- Removed the delayed settings request-message race that produced No response was received errors
- Invalid backup JSON is rejected without modifying active data or the selected source

### Changed
- Version bumped to 1.7.0
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
