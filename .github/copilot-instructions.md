# CafeMaestro Copilot Guidelines

## Project Overview
CafeMaestro is a cross-platform coffee roasting companion app built with .NET MAUI for .NET 9.

## Architecture
- Follow MVVM pattern with dependency injection.
- Business logic is located in `Services/`, UI in XAML/code-behind.
- Data models are defined in `Models/`.

## Naming Conventions
- **Classes/Methods/Properties**: PascalCase
- **Private Fields**: camelCase with underscore prefix (_fieldName)
- **Local Variables**: camelCase
- **File Names**: PascalCase (e.g., `LightTheme.xaml`, `DarkTheme.xaml`)

## Important Technical Guidelines

### UI
- Maintain a **consistent design** across screens.
- Use round `ImageButton` components where possible.
- Always use resource dictionary colors instead of hardcoded values:
  - XAML: `{StaticResource ColorName}`
  - C#: `Application.Current.Resources["ColorName"]`
  - Colors are defined in `LightTheme.xaml` and `DarkTheme.xaml`.
- Prioritize **accessibility**:
  - Support dynamic font scaling.
  - Ensure proper color contrast for readability.
  - Use responsive layouts for optimal experience across devices.

### SVG Image Handling
- .NET MAUI converts `.svg` files to `.png` at build time using `MauiImage` build actions.
- Always reference images with `.png` extension in code:
  - Project file: `<MauiImage Include="Resources\Images\logo.svg" />`
  - XAML: `<Image Source="logo.png" />`
  - C#: `ImageSource.FromFile("logo.png")`

### Data Persistence
- Use JSON for data storage via `AppDataService`.
- Store user preferences using `PreferencesService`.
- Implement validation and fallback mechanisms for read/write failures.

## Best Practices
- Follow **async/await** principles for efficient async operations.
- Use structured logging for debugging and monitoring.
- Apply proper error handling across data operations.

## Common Pitfalls
- **Hardcoding colors** instead of using resource dictionaries.
- **Referencing `.svg` directly** instead of the `.png` output.
- **Neglecting accessibility** (ensure UI elements work on all screen sizes).
- **Skipping validation** in data persistence—always handle potential failures.

## Domain Terms
- **First Crack**: When beans audibly crack during roasting.
- **Development Time**: Time between first crack and end of roast.
- **Weight Loss**: Percentage of weight lost during roasting (determines roast level).
- **Batch/Final Weight**: Before/after roasting weights.
