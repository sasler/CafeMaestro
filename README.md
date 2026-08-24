# CafeMaestro

A modern, cross-platform coffee roasting companion app built with .NET MAUI.

**ROAST - BREW - SAVOR - REPEAT**

![CafeMaestro Logo](CafeMaestro/Resources/Images/cafemaestro_logo.svg)

![CI](https://github.com/sasler/CafeMaestro/actions/workflows/ci.yml/badge.svg)

## Overview

CafeMaestro is a comprehensive tool designed for coffee enthusiasts and professional roasters to track, manage, and optimize their coffee roasting process. The application provides tools for managing bean inventory, timing roasts, recording roast data, and analyzing results.

## Features

- **Bean Inventory Management**: Track green coffee beans, including origin, variety, processing method, and remaining quantity.
- **Roast Timer**: Precision timer with digital display for accurate roast timing.
- **Roast Logging**: Record all aspects of each roast including temperature, batch weight, final weight, and calculated weight loss.
- **First Crack Tracking**: Mark the exact moment of first crack for development time analysis.
- **Roast Level Analysis**: Automatic classification of roast levels based on weight loss percentage.
- **Custom Roast Levels**: Define and customize your own roast levels based on weight loss percentages.
- **CSV Transfer**: Import bean and roast records, then save or share roast-log CSV files through the system document UI.
- **Theme Support**: Choose between light, dark, or system theme preferences.
- **Automatic Data Safety**: Keep the live dataset private inside CafeMaestro, export validated backups, and recover from the five newest automatic safety copies.
- **Cross-Platform**: Built with .NET MAUI for Android and Windows (iOS/macOS supported by framework).

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or higher
- MAUI workload: `dotnet workload install maui`
- One of the following IDEs:
  - Visual Studio 2022 (17.13+) with the .NET MAUI workload
  - Visual Studio Code with the .NET MAUI extension
  - JetBrains Rider with .NET MAUI support
- For Android builds: Android SDK with API level 36

### Building

```bash
# Clone the repository
git clone https://github.com/sasler/CafeMaestro.git
cd CafeMaestro

# Restore dependencies
dotnet restore

# Build for Windows
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-windows10.0.19041.0

# Build for Android
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-android
```

### Running Tests

```bash
dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj
```

## Architecture

CafeMaestro follows the **MVVM pattern** with constructor-based **dependency injection**, powered by [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/).

### Project Structure

```
CafeMaestro/
├── Models/               # Data models (BeanData, RoastData, RoastLevelData, AppData)
├── ViewModels/           # MVVM ViewModels using CommunityToolkit.Mvvm source generators
├── Services/             # Business logic services
│   └── Interfaces/       # Service contracts (IAppDataService, IBeanDataService, etc.)
├── Controls/             # Reusable controls (IconView)
├── Navigation/           # Centralized route constants
├── Resources/
│   ├── Styles/           # Visual system (DesignTokens, DarkTheme, LightTheme,
│   │                     #   ComponentStyles, IconGeometries) - see docs/visual-system.md
│   ├── Images/           # SVG icons (converted to PNG at build time)
│   └── Fonts/            # Custom fonts
├── Views/                # Pages that are not at the project root
├── Platforms/            # Platform-specific implementations
└── *.xaml / *.xaml.cs    # Pages (thin code-behind, logic in ViewModels)

CafeMaestro.Tests/
├── ViewModels/           # ViewModel unit tests
├── ModelValidationTests.cs
├── CsvParserServiceTests.cs
└── NavigationServiceTests.cs
```

### Key Packages

| Package | Version | Purpose |
|---------|---------|---------|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM source generators ([ObservableProperty], [RelayCommand]) |
| CommunityToolkit.Maui | 14.0.1 | MAUI community extensions |
| Microsoft.Maui.Controls | 10.0.41 | .NET MAUI framework |
| xUnit + Moq + FluentAssertions | latest | Testing |

### Service Layer

All services are registered via DI in `MauiProgram.cs` using interface-based singletons:

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| ManagedAppDataService | IAppDataService | Private canonical JSON persistence with transactional saves and legacy migration |
| BeanDataService | IBeanDataService | Bean CRUD operations, CSV import |
| RoastDataService | IRoastDataService | Roast CRUD, CSV import/export |
| RoastLevelService | IRoastLevelService | Roast level classification |
| TimerService | ITimerService | Roast timer with elapsed time events |
| PreferencesService | IPreferencesService | User preferences storage |
| NavigationService | INavigationService | Centralized Shell navigation |
| AlertService | IAlertService | ViewModel-driven dialog alerts |
| CsvParserService | ICsvParserService | Shared CSV file parsing |
| UserFileService | IUserFileService | Stream-based document selection, temporary import caching, and Save As |
| DataBackupService | IDataBackupService | Backup preview, validation, restore, export, and safety history |

## Usage

### Managing Beans

Use the Beans section to add, edit, and track your green coffee beans. Record:
- Bean variety and origin
- Processing method
- Quantity in kilograms
- Purchase price and supplier links
- Cupping notes and characteristics

### Recording Roasts

The Roast Coffee feature helps you:
- Select beans from your inventory
- Time your roast with the built-in digital timer
- Record roasting temperature
- Track batch and final weights
- Automatically calculate weight loss percentage
- Classify roast level based on weight loss
- Record the time of first crack during roasting
- View previous roast data for the selected bean type

### Reviewing Roast Logs

The Roast Log section allows you to:
- View history of all recorded roasts
- Filter and search by bean type
- Edit or delete existing roast records
- Export roast data to CSV

### Data and Backups

CafeMaestro saves the working dataset automatically in private app storage. In Settings you can:
- Start a new dataset only after confirmation and an automatic recovery copy
- Save, restore, or share JSON backup copies without modifying selected source files
- Restore one of the five most recent automatic safety backups
- Import coffee beans or roast logs from CSV, and save or share roast-log CSV exports

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

See [`.github/copilot-instructions.md`](.github/copilot-instructions.md) for coding conventions and guidelines.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [.NET MAUI](https://dotnet.microsoft.com/apps/maui) for the cross-platform framework
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) for MVVM source generators
- Coffee roasters everywhere for inspiration