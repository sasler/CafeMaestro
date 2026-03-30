# Changelog

All notable changes to CafeMaestro will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] - Complete Architecture Refactor
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
