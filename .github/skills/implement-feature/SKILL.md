---
name: implement-feature
description: >
  Standard workflow for implementing any feature, bug fix, or change in CafeMaestro.
  Enforces TDD, multi-model code review, version bumping, changelog updates,
  PR creation, and GitHub Copilot auto-review follow-up.
  Use this skill whenever asked to implement, fix, or change functionality.
---

# Implement Feature Workflow

This skill defines the mandatory workflow for every code change in CafeMaestro. Follow each step in order. Do not skip steps.

---

## Step 1 — Create a Branch

- Create a new git branch from `main`:
  - For GitHub issues: `issue-<number>-<short-description>`
  - For features without an issue: `feature/<short-description>`
- Never work directly on `main`.

```bash
git checkout main && git pull
git checkout -b issue-<N>-<description>
```

---

## Step 2 — Research

Before writing any code, **research** the best way to implement the requested feature or change:

- Look up .NET MAUI 10 documentation and best practices (NOT older versions)
- Review the existing codebase to understand current patterns
- Check how similar features are implemented in the project
- Identify which services, models, and ViewModels will be affected
- Plan the approach before touching any code

Use **GPT 5.4** for research tasks (unless UX-related, then use **Claude Opus 4.6**).

---

## Step 3 — Write a Failing Test (TDD)

Write a test **before** implementing the feature. The test must:

- Be meaningful — only test behavior that matters for the app's functionality
- No boilerplate filler tests
- Use xUnit, Moq, and FluentAssertions
- Follow existing test patterns in `CafeMaestro.Tests/`
- Live in the appropriate subdirectory (`ViewModels/`, or root for services/models)

```bash
dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj
# The new test should FAIL (red phase)
```

If the change is purely cosmetic/XAML (no testable logic), document why no test was written and skip to Step 4.

---

## Step 4 — Implement

Write the code to make the test pass.

### Model routing:
- **UX/UI work** (XAML, themes, layouts, animations): Use **Claude Opus 4.6**
- **Everything else** (services, ViewModels, models, logic): Use **GPT 5.4**

### Implementation checklist:
- [ ] Follow MVVM pattern with DI
- [ ] Use `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm
- [ ] Use `INavigationService` for navigation (never `Shell.Current`)
- [ ] Use `IAlertService` for dialogs (never `DisplayAlert`)
- [ ] Use resource dictionary colors (never hardcode)
- [ ] Reference images as `.png` (not `.svg`)
- [ ] Use `CultureInfo.InvariantCulture` for number formatting in storage
- [ ] Register new services/pages in `MauiProgram.cs`
- [ ] Register new routes in `AppShell.xaml.cs` and `Navigation/Routes.cs`

---

## Step 5 — Verify

Run **all** tests and builds. Nothing proceeds until everything passes.

```bash
# Run all tests (including smoke tests)
dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj

# Build for Windows
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-windows10.0.19041.0

# Build for Android
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-android
```

**If anything fails:**
1. Fix the issue
2. Re-run ALL verification commands
3. Do NOT proceed to Step 6 until everything passes

---

## Step 6 — Code Review

Request a code review using a **DIFFERENT AI model** than the one that wrote the code.

- **Always use Claude Sonnet 4.6** for code review
- The reviewer must examine:
  - Correctness and edge cases
  - MVVM pattern adherence
  - Error handling completeness
  - Memory leak risks (event subscriptions)
  - Cross-platform compatibility
  - Accessibility considerations
  - Naming conventions compliance
  - Common pitfalls from the project guidelines

The reviewer should NOT comment on:
  - Pure style preferences that don't affect correctness
  - Minor formatting that follows existing patterns

---

## Step 7 — Address Review Issues

For each issue raised by the code review:

1. Evaluate if the issue is valid (the reviewer is not always right)
2. If valid: fix the issue
3. If not valid: document why you disagree

After addressing all issues, **go back to Step 5** (re-verify everything).

---

## Step 8 — Loop Until Clean

Repeat Steps 5–7 until:
- ✅ All tests pass
- ✅ All builds succeed (Windows + Android)
- ✅ All valid code review issues are addressed

---

## Step 9 — Version Bump

Update the version numbers in `CafeMaestro/CafeMaestro.csproj`:

```xml
<!-- Bump according to Semantic Versioning -->
<ApplicationDisplayVersion>X.Y.Z</ApplicationDisplayVersion>
<!-- Increment by 1 for each release -->
<ApplicationVersion>N</ApplicationVersion>
```

- **Major** (X): Breaking changes
- **Minor** (Y): New features
- **Patch** (Z): Bug fixes

---

## Step 10 — Update Documentation

### CHANGELOG.md
Add changes under `[Unreleased]` using the established format:

```markdown
## [Unreleased]
### Added
- Description of new features

### Changed
- Description of changes to existing functionality

### Fixed
- Description of bug fixes

### Removed
- Description of removed features
```

### README.md
Update if the change affects:
- Features list
- Getting started instructions
- Architecture documentation
- Build or test commands

### Other docs
Update any other documentation that references changed behavior.

---

## Step 11 — Commit and Create PR

### Commit
- Use [Gitmoji](https://gitmoji.dev/) in the commit message
- Format: `<emoji> <type>(<scope>): <description>`
- Examples:
  - `✨ feat(roast): Add first crack tracking timer`
  - `🐛 fix(import): Handle empty CSV columns gracefully`
  - `♻️ refactor(services): Extract shared CSV parsing logic`

```bash
git add -A
git commit -m "✨ feat(scope): Description

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push -u origin HEAD
```

### Create PR
- Title: Gitmoji + clear description
- Body:
  - Summary of changes
  - Components modified (Models, Services, UI, etc.)
  - Impact on existing functionality
  - Testing instructions
  - Cross-platform considerations

---

## Step 12 — Poll GitHub Copilot Auto-Review

After creating the PR, GitHub Copilot will automatically review it. This can take a while to start and complete.

1. **Poll** the PR periodically for the Copilot review status
2. **Wait** until the review is fully complete — do not proceed early
3. **Fetch** all inline comments and suggestions once the review appears

---

## Step 13 — Address Copilot Review Comments

> **This step is ATOMIC.** Do not yield, stop, or mark the task complete until ALL comments have been both addressed in code AND replied to on the PR. Partial completion (e.g., replying to some comments but not others) is NOT acceptable.

For each comment/suggestion from the GitHub Copilot review:

1. **Read carefully** — understand what the reviewer is flagging
2. **Evaluate critically** — the automatic reviewer is NOT always right
3. **If you agree:** Implement the fix or improvement
4. **If you disagree:** Prepare a clear explanation of why

After implementing changes:
- Run all tests and builds again (Step 5)
- Reply to **every single** inline comment explaining what you did to address it
- If you disagreed, reply with your reasoning
- **Verify** that no unreplied comments remain before proceeding

---

## Step 14 — Final Verification

One last check before considering the task complete:

```bash
dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-windows10.0.19041.0
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-android
```

✅ All tests pass
✅ All builds succeed
✅ All review comments addressed
✅ Version bumped
✅ CHANGELOG updated
✅ PR is clean and ready for merge
