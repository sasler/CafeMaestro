# Changelog

All notable changes to CafeMaestro will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Hover feedback for buttons to improve user interaction experience
- Visual feedback for card elements during hover interaction

### Changed
- Removed unnecessary Debug.WriteLine statements to improve code quality
- Fixed potential null reference exceptions in RoastPage.xaml.cs
- Replaced reflection-based AppDataService notification suppression with IDisposable-based suspension during roast imports
- Replaced page-level service locator initialization with constructor injection across app pages and shell startup flow
- Added model-level validation and test coverage for bean, roast, and roast level data
- Extracted shared CSV parsing into CsvParserService and updated import flows to use DI
- Centralized Shell navigation through NavigationService and shared route constants
- Converted MainPage to a CommunityToolkit.Mvvm view model with command bindings and unit tests
- Converted BeanInventoryPage, BeanEditPage, and RoastLogPage to CommunityToolkit.Mvvm view models with command bindings and unit tests
- Converted SettingsPage to a CommunityToolkit.Mvvm view model with command bindings, messenger-backed prompts, and unit tests
- Converted RoastPage to a CommunityToolkit.Mvvm RoastPageViewModel with timer, roast editing, bean selection, save workflows, and unit tests

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
