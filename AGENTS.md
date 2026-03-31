# CafeMaestro — AI Agent Instructions

## Project Overview

CafeMaestro is a cross-platform coffee roasting companion app built with **.NET MAUI for .NET 10**.

> **CRITICAL:** This is a .NET 10 MAUI app. Do **NOT** use APIs, patterns, or NuGet packages from older .NET versions. Always verify that any API or package you reference is compatible with .NET 10 and MAUI 10.

- **Repository:** [github.com/sasler/CafeMaestro](https://github.com/sasler/CafeMaestro)
- **Solution:** `CafeMaestro.sln`
- **App project:** `CafeMaestro/CafeMaestro.csproj`
- **Test project:** `CafeMaestro.Tests/CafeMaestro.Tests.csproj`

## Model Routing

Use the correct AI model for each type of work:

| Task type | Model | Rationale |
|---|---|---|
| **UX/UI design & implementation** | Claude Opus 4.6 | Superior aesthetic judgment and design coherence |
| **Code review** | Claude Sonnet 4.6 | Fast, thorough, detail-oriented review |
| **Coding (non-UX), research, everything else** | GPT 5.4 | Strong general-purpose coding and reasoning |

When delegating to sub-agents, always use the model specified above. The code review agent **must** be a different model than the one that wrote the code — fresh eyes catch more issues.

## Standard Workflow

For any feature, bug fix, or change, follow the **implement-feature** skill (`.github/skills/implement-feature/SKILL.md`). The workflow enforces:

1. New git branch per task
2. Research-first approach (best practices for .NET MAUI 10)
3. TDD: write a failing test → implement → green
4. Full verification (tests + builds for all targets)
5. Code review by a different AI model
6. Version bump + changelog + docs update
7. PR creation with gitmoji
8. GitHub Copilot auto-review polling and follow-up

> **CRITICAL — Atomic Review Cycle:** Steps 7–8 form an atomic unit of work. After receiving the GitHub Copilot auto-review, you **must** complete ALL of the following before yielding, stopping, or marking the task complete:
>
> 1. Read and evaluate **every** review comment
> 2. Fix all valid issues in code
> 3. Re-run tests and builds to verify fixes
> 4. Commit and push the fixes
> 5. Reply to **every single comment** on the PR explaining what you did (or why you disagree)
> 6. Confirm no unreplied comments remain
>
> **Do NOT** stop partway through the review cycle. Replying to some comments but not others is unacceptable — treat the review follow-up as a single indivisible operation.

---

## Architecture

Follow **MVVM** with **dependency injection** throughout.

### ViewModels (`ViewModels/`)

- Inherit from `ObservableObject` (CommunityToolkit.Mvvm)
- Use `[ObservableProperty]` for bindable properties — private fields with `_` prefix, camelCase
- Use `[RelayCommand]` for commands
- Use `partial void OnXxxChanged()` for property-change reactions
- **Never** call `Shell.Current` — use `INavigationService`
- **Never** call `DisplayAlert` — use `IAlertService`

### Services

- Contracts in `Services/Interfaces/` (e.g., `IAppDataService`, `IBeanDataService`)
- Implementations in `Services/` (e.g., `AppDataService`, `BeanDataService`)
- Register in `MauiProgram.cs` via `AddSingleton<IInterface, Implementation>()`
- Inject via constructor

### Navigation

- Route constants in `Navigation/Routes.cs`
- `INavigationService` wraps `Shell.Current` — only `Services/NavigationService.cs` touches Shell directly
- Register new routes in `AppShell.xaml.cs`

### Models (`Models/`)

- `BeanData`, `RoastData`, `RoastLevelData`, `AppData`
- Built-in validation: `Validate()` returns `List<string>`, `IsValid` property
- Use `CultureInfo.InvariantCulture` for numeric formatting/parsing in storage

### Pages

- Thin code-behind: constructor DI + lifecycle forwarding to ViewModel
- UI-only logic (animations, file pickers) stays in code-behind

---

## Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes, Methods, Properties | PascalCase | `RoastPageViewModel` |
| Private fields | `_camelCase` | `_beanDataService` |
| Local variables | camelCase | `roastLevel` |
| File names | PascalCase | `LightTheme.xaml` |

---

## Adding a New Page

1. Create ViewModel in `ViewModels/XxxPageViewModel.cs` inheriting `ObservableObject`
2. Create Page XAML + thin code-behind (constructor injects ViewModel, sets `BindingContext`)
3. Register both in `MauiProgram.cs` (`AddTransient<XxxPageViewModel>()`, `AddTransient<XxxPage>()`)
4. Add route constant in `Navigation/Routes.cs`
5. Register route in `AppShell.xaml.cs`
6. Add unit tests in `CafeMaestro.Tests/ViewModels/`

## Adding a New Service

1. Create interface in `Services/Interfaces/IXxxService.cs`
2. Create implementation in `Services/XxxService.cs`
3. Register in `MauiProgram.cs` (`AddSingleton<IXxxService, XxxService>()`)
4. Inject via constructor in ViewModels/services that need it
5. Add unit tests

---

## UI Guidelines

- Maintain **consistent design** across screens
- Use round `ImageButton` components where possible
- **Always** use resource dictionary colors — never hardcode:
  - XAML: `{StaticResource ColorName}`
  - C#: `Application.Current.Resources["ColorName"]`
  - Colors defined in `LightTheme.xaml` and `DarkTheme.xaml`
- Prioritize **accessibility**:
  - Support dynamic font scaling
  - Ensure proper color contrast
  - Use responsive layouts for all device sizes

### SVG Image Handling

.NET MAUI converts `.svg` → `.png` at build time via `MauiImage` build actions.

- Project file: `<MauiImage Include="Resources\Images\logo.svg" />`
- XAML: `<Image Source="logo.png" />` ← always `.png`
- C#: `ImageSource.FromFile("logo.png")` ← always `.png`

---

## Data Persistence

- JSON storage via `IAppDataService`
- User preferences via `IPreferencesService`
- Models have built-in validation — always validate before persisting
- Implement fallback mechanisms for read/write failures

---

## Testing

- **Framework:** xUnit + Moq + FluentAssertions
- **Test project:** `CafeMaestro.Tests/`
- **Run tests:** `dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj`
- **Build Windows:** `dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-windows10.0.19041.0`
- **Build Android:** `dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-android`
- Write tests for all new ViewModels and services using Moq for dependencies
- Use TDD: write failing tests first, then implement
- Only generate tests that make sense for the app's functionality — no boilerplate filler tests

---

## Git Workflow

- **Branch per task:** `issue-<number>-<short-description>` (e.g., `issue-42-fix-ui-layout`)
- **Do not commit** until code is stable, tested, and reviewed
- **Gitmoji** for commit messages and PR titles ([gitmoji.dev](https://gitmoji.dev/))
  - Example: `✨ Add new feature for roast tracking`
- **CHANGELOG.md:** Always update under `[Unreleased]` using Added/Changed/Fixed/Removed categories
- **Version bump:** Update `ApplicationDisplayVersion` and `ApplicationVersion` in `CafeMaestro.csproj` before creating a PR
- **PR creation:** Include clear description with summary, modified components, testing instructions, and cross-platform impact
- Before completing a task, check for and address any outstanding errors, warnings, or inconsistencies

---

## Best Practices

- Follow **async/await** patterns for all async operations
- Use structured logging for debugging and monitoring
- Apply proper error handling across data operations
- Use `CultureInfo.InvariantCulture` for numeric formatting/parsing in storage
- Unsubscribe from singleton service events in `OnDisappearing()` to prevent memory leaks
- Use `SuspendNotifications()` on `IAppDataService` for bulk data operations

---

## Common Pitfalls — Do NOT

- ❌ Hardcode colors — use resource dictionaries
- ❌ Reference `.svg` directly — use `.png` output
- ❌ Neglect accessibility — test on all screen sizes
- ❌ Skip validation — always handle potential failures
- ❌ Use `Shell.Current` in pages/ViewModels — use `INavigationService`
- ❌ Call `DisplayAlert` from ViewModels — use `IAlertService`
- ❌ Subscribe to singleton events without unsubscribing — causes memory leaks
- ❌ Mix cultures in number formatting — always use `InvariantCulture`
- ❌ Use .NET 9 or older APIs/patterns — this is .NET 10
- ❌ Leave review comments unaddressed or unreplied — complete the full review cycle atomically
- ❌ Mark a task complete with pending review follow-ups — all comments must be replied to first

---

## Domain Terms

| Term | Definition |
|---|---|
| **First Crack** | When beans audibly crack during roasting |
| **Development Time** | Time between first crack and end of roast |
| **Weight Loss** | Percentage of weight lost during roasting (determines roast level) |
| **Batch Weight** | Weight of beans before roasting |
| **Final Weight** | Weight of beans after roasting |
