---
name: implement-feature
description: >
  Implement a feature, bug fix, or change in CafeMaestro.
  Covers branch creation, research, TDD, and implementation.
  Follow with verify-and-review when done.
---

# Implement Feature

## Step 1 — Create a Branch

```bash
git checkout main && git pull
git checkout -b issue-<N>-<description>  # or feature/<description>
```

Never work directly on `main`.

## Step 2 — Research

Before writing any code:

- Look up .NET MAUI 10 docs (NOT older versions)
- Review existing codebase patterns
- Identify affected services, models, ViewModels
- Plan the approach before touching code

## Step 3 — Write a Failing Test (TDD)

Write a test **before** implementing. It must:

- Be meaningful — only test behavior that matters
- No boilerplate filler tests
- Use xUnit, Moq, FluentAssertions
- Follow existing patterns in `CafeMaestro.Tests/`
- Live in the appropriate subdirectory (`ViewModels/`, or root for services/models)

```bash
dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj
# The new test should FAIL (red phase)
```

If the change is purely cosmetic/XAML (no testable logic), document why no test was written and skip to Step 4.

## Step 4 — Implement

Write the code to make the test pass.

### Model routing

- **UX/UI work** (XAML, themes, layouts, animations): Use **Claude Opus 4.6**
- **Everything else** (services, ViewModels, models, logic): Use **GPT 5.4**

### Implementation checklist

- [ ] Follow MVVM pattern with DI
- [ ] Use `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm
- [ ] Use `INavigationService` for navigation (never `Shell.Current`)
- [ ] Use `IAlertService` for dialogs (never `DisplayAlert`)
- [ ] Use resource dictionary colors (never hardcode)
- [ ] Reference images as `.png` (not `.svg`)
- [ ] Use `CultureInfo.InvariantCulture` for number formatting in storage
- [ ] Register new services/pages in `MauiProgram.cs`
- [ ] Register new routes in `AppShell.xaml.cs` and `Navigation/Routes.cs`
