---
name: pr-workflow
description: >
  Version bump, changelog update, commit, and PR creation for CafeMaestro.
  Use after verify-and-review is clean.
---

# PR Workflow

## Step 1 — Version Bump

Update version numbers in `CafeMaestro/CafeMaestro.csproj`:

```xml
<ApplicationDisplayVersion>X.Y.Z</ApplicationDisplayVersion>
<ApplicationVersion>N</ApplicationVersion>
```

- **Major** (X): Breaking changes
- **Minor** (Y): New features
- **Patch** (Z): Bug fixes

## Step 2 — Update Documentation

### CHANGELOG.md

Add changes under `[Unreleased]`:

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

Update if the change affects: features list, getting started, architecture, build/test commands.

### Other docs

Update any other documentation that references changed behavior.

## Step 3 — Commit and Create PR

### Commit

- Use [Gitmoji](https://gitmoji.dev/) in the commit message
- Format: `<emoji> <type>(<scope>): <description>`

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
