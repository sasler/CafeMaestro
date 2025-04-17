# CafeMaestro

A modern, cross-platform coffee roasting companion app built with .NET MAUI.

![CafeMaestro Logo](CafeMaestro/Resources/Images/cafemaestro_logo.svg)

## Overview

CafeMaestro is a comprehensive tool designed for coffee enthusiasts and professional roasters to track, manage, and optimize their coffee roasting process. The application provides tools for managing bean inventory, timing roasts, recording roast data, and analyzing results.

## Features

- **Bean Inventory Management**: Track green coffee beans, including origin, variety, processing method, and remaining quantity.
- **Roast Timer**: Precision timer with digital display for accurate roast timing.
- **Roast Logging**: Record all aspects of each roast including temperature, batch weight, final weight, and calculated weight loss.
- **Roast Level Analysis**: Automatic classification of roast levels based on weight loss percentage.
- **Data Import/Export**: Import and export bean and roast data for backup and analysis.
- **Cross-Platform**: Built with .NET MAUI for compatibility with Android, iOS, macOS, and Windows.

## Getting Started

### Prerequisites

- .NET 9.0 SDK or higher
- Visual Studio 2022 or higher with the .NET MAUI workload installed

### Installation

1. Clone the repository:
   ```
   git clone https://github.com/yourusername/CafeMaestro.git
   ```

2. Open the solution file (`CafeMaestro.sln`) in Visual Studio.

3. Build and run the application for your desired platform.

## Usage

### Managing Beans

Use the Beans section to add, edit, and track your green coffee beans. Record:
- Bean variety and origin
- Processing method
- Quantity in kilograms
- Cupping notes and characteristics

### Recording Roasts

The Roast Coffee feature helps you:
- Select beans from your inventory
- Time your roast with the built-in digital timer
- Record roasting temperature
- Track batch and final weights
- Automatically calculate weight loss percentage
- Classify roast level based on weight loss

### Reviewing Roast Logs

The Roast Log section allows you to:
- View history of all recorded roasts
- Filter and search by bean type or date
- Analyze roasting trends over time

## Development

CafeMaestro is built using:
- .NET MAUI for cross-platform UI
- C# for business logic
- JSON for data storage

### Project Structure

- `Models/`: Data models for beans, roasts, and application data
- `Services/`: Business logic services for data management
- `Resources/`: Application resources including images and fonts
- `Platforms/`: Platform-specific implementations

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [.NET MAUI](https://dotnet.microsoft.com/apps/maui) for the cross-platform framework
- Coffee roasters everywhere for inspiration