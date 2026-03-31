# CafeMaestro Copilot Guidelines

## Project Overview
CafeMaestro is a cross-platform coffee roasting companion app built with .NET MAUI for .NET 10.  
GitHub repository: <a href="https://github.com/sasler/CafeMaestro">CafeMaestro</a>

## Architecture
- Follow MVVM pattern with dependency injection.
- **ViewModels** are in `ViewModels/` using `CommunityToolkit.Mvvm` source generators:
  - Inherit from `ObservableObject`
  - Use `[ObservableProperty]` for bindable properties (camelCase private fields with `_` prefix)
  - Use `[RelayCommand]` for commands
  - Use `partial void OnXxxChanged()` for property change reactions
- **Services** use interface-based DI registered in `MauiProgram.cs`:
  - Contracts in `Services/Interfaces/` (e.g., `IAppDataService`, `IBeanDataService`)
  - Implementations in `Services/` (e.g., `AppDataService`, `BeanDataService`)
- **Navigation** uses `INavigationService` + route constants in `Navigation/Routes.cs`.
  - `Shell.Current` is confined to `Services/NavigationService.cs` — never use it directly in pages or ViewModels.
- **Alerts/Dialogs** from ViewModels use `IAlertService` — never call `DisplayAlert` from a ViewModel.
- **Models** are in `Models/` (BeanData, RoastData, RoastLevelData, AppData) with `Validate()` and `IsValid`.
- **Pages** have thin code-behind: constructor DI + lifecycle forwarding to ViewModel. Keep UI-only logic (animations, file pickers) in code-behind.

## Naming Conventions
- **Classes/Methods/Properties**: PascalCase
- **Private Fields**: camelCase with underscore prefix (`_fieldName`)
- **Local Variables**: camelCase
- **File Names**: PascalCase (e.g., `LightTheme.xaml`, `DarkTheme.xaml`)

## Important Technical Guidelines

### Adding a New Page
1. Create ViewModel in `ViewModels/XxxPageViewModel.cs` inheriting `ObservableObject`
2. Create Page XAML and thin code-behind (constructor injects ViewModel, sets `BindingContext`)
3. Register both in `MauiProgram.cs` (`AddTransient<XxxPageViewModel>()`, `AddTransient<XxxPage>()`)
4. Add route constant in `Navigation/Routes.cs`
5. Register route in `AppShell.xaml.cs`
6. Add unit tests in `CafeMaestro.Tests/ViewModels/`

### Adding a New Service
1. Create interface in `Services/Interfaces/IXxxService.cs`
2. Create implementation in `Services/XxxService.cs`
3. Register in `MauiProgram.cs` (`AddSingleton<IXxxService, XxxService>()`)
4. Inject via constructor in ViewModels/services that need it
5. Add unit tests

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
- Use JSON for data storage via `IAppDataService`.
- Store user preferences using `IPreferencesService`.
- Models have built-in validation (`Validate()` returns `List<string>`, `IsValid` property).
- Implement fallback mechanisms for read/write failures.

### Testing
- Test project: `CafeMaestro.Tests/` using xUnit, Moq, FluentAssertions
- Run tests: `dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj`
- Build Windows: `dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-windows10.0.19041.0`
- Build Android: `dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-android`
- Write tests for all new ViewModels and services using Moq for dependencies
- Use TDD where possible: write tests first, then implement

## Git Workflow
- Whenever starting work on a **GitHub issue**, create a **new Git branch** specific to that issue.
  - Branch naming convention: `issue-<number>-<short-description>` (e.g., `issue-42-fix-ui-layout`).
- **Do not create a commit until satisfied with the changes.** Ensure the code is stable, follows project guidelines, and is reviewed if needed.
- **Always update the CHANGELOG.md** when submitting a pull request:
  - Add your changes under the `[Unreleased]` section.
  - Follow the established format: Added/Changed/Fixed/Removed categories.
  - Be clear and concise about what was changed.
  - Example: `- Added version tracking functionality using .NET MAUI's built-in capabilities`
- For **commit messages and pull requests**, use <a href="https://gitmoji.dev/">Gitmoji</a> for clarity and consistency.  
  - Example: `✨ Add new feature for roast tracking`
  - Helps visually categorize changes and improves commit readability.
- When working in **Agent mode**, check if there are **any outstanding issues or problems** before completing a task.
  - Address errors, warnings, or inconsistencies before finalizing the work.
- Ensure commits are meaningful and describe the changes clearly.

## Best Practices
- Follow **async/await** principles for efficient async operations.
- Use structured logging for debugging and monitoring.
- Apply proper error handling across data operations.
- Use `CultureInfo.InvariantCulture` when formatting/parsing numeric values for storage.
- Unsubscribe from singleton service events in `OnDisappearing()` to prevent memory leaks.
- Use `SuspendNotifications()` on `IAppDataService` for bulk data operations.

## Common Pitfalls
- **Hardcoding colors** instead of using resource dictionaries.
- **Referencing `.svg` directly** instead of the `.png` output.
- **Neglecting accessibility** (ensure UI elements work on all screen sizes).
- **Skipping validation** in data persistence—always handle potential failures.
- **Using `Shell.Current` directly** in pages or ViewModels — use `INavigationService`.
- **Calling `DisplayAlert` from ViewModels** — use `IAlertService`.
- **Subscribing to singleton events without unsubscribing** — causes memory leaks with transient ViewModels.
- **Mixing cultures in number formatting/parsing** — always use InvariantCulture consistently.

## Domain Terms
- **First Crack**: When beans audibly crack during roasting.
- **Development Time**: Time between first crack and end of roast.
- **Weight Loss**: Percentage of weight lost during roasting (determines roast level).
- **Batch/Final Weight**: Before/after roasting weights.
