# CafeMaestro — Agent Instructions

.NET MAUI (.NET 10) cross-platform coffee roasting companion app.

**Solution:** `CafeMaestro.sln` · **App:** `CafeMaestro/CafeMaestro.csproj` · **Tests:** `CafeMaestro.Tests/CafeMaestro.Tests.csproj`

## Workflow

For any feature, bug fix, or change, follow these skills in order:

1. **implement-feature** (`.github/skills/implement-feature/SKILL.md`) — branch, research, TDD, implement
2. **verify-and-review** (`.github/skills/verify-and-review/SKILL.md`) — test/build, code review, fix loop
3. **pr-workflow** (`.github/skills/pr-workflow/SKILL.md`) — version bump, changelog, commit, PR
4. **review-cycle** (`.github/skills/review-cycle/SKILL.md`) — request Copilot review, address all comments atomically

## Architecture

MVVM with DI throughout.

- **ViewModels** — inherit `ObservableObject`, use `[ObservableProperty]` and `[RelayCommand]`. Never call `Shell.Current` (use `INavigationService`) or `DisplayAlert` (use `IAlertService`).
- **Services** — contracts in `Services/Interfaces/`, implementations in `Services/`. Register in `MauiProgram.cs`.
- **Models** — `BeanData`, `RoastData`, `RoastLevelData`, `AppData`. Built-in validation via `Validate()` / `IsValid`. Use `CultureInfo.InvariantCulture` for numeric formatting in storage.
- **Pages** — thin code-behind; constructor DI + lifecycle forwarding to ViewModel.

## Adding a Page or Service

**Page:** ViewModel → XAML + code-behind → register in `MauiProgram.cs` → route in `Navigation/Routes.cs` → register in `AppShell.xaml.cs` → unit tests.

**Service:** Interface in `Services/Interfaces/` → implementation in `Services/` → register in `MauiProgram.cs` → inject via constructor → unit tests.

## Rules

- **Never hardcode colors** — use `{StaticResource ColorName}` in XAML, `Application.Current.Resources["ColorName"]` in C#. Colors live in `Resources/Styles/LightTheme.xaml` and `DarkTheme.xaml`.
- **Images** — reference `.png` (MAUI converts `.svg` → `.png` at build time via `MauiImage`).
- **Unsubscribe from singleton events** in `OnDisappearing()` to prevent memory leaks.
- **Use `SuspendNotifications()`** on `IAppDataService` for bulk data operations.
- **Async/await** for all async operations. Structured logging + proper error handling.
- **Accessibility** — dynamic font scaling, color contrast, responsive layouts.

## Build & Test

```bash
dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-windows10.0.19041.0
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-android
```

- Framework: xUnit + Moq + FluentAssertions
- TDD: failing test → implement → green
