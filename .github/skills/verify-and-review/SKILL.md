---
name: verify-and-review
description: >
  Verify all tests and builds pass, then run a code review.
  Loop until everything is green and all review issues are addressed.
  Use after implement-feature.
---

# Verify and Review

## Step 1 — Verify

Run **all** tests and builds. Nothing proceeds until everything passes.

```bash
dotnet test CafeMaestro.Tests\CafeMaestro.Tests.csproj
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-windows10.0.19041.0
dotnet build CafeMaestro\CafeMaestro.csproj -f net10.0-android
```

**If anything fails:**
1. Fix the issue
2. Re-run ALL verification commands
3. Do NOT proceed until everything passes

## Step 2 — Code Review

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

The reviewer should NOT comment on:
  - Pure style preferences that don't affect correctness
  - Minor formatting that follows existing patterns

## Step 3 — Address Review Issues

For each issue raised:

1. Evaluate if the issue is valid (the reviewer is not always right)
2. If valid: fix the issue
3. If not valid: document why you disagree

After addressing all issues, **go back to Step 1** (re-verify everything).

## Step 4 — Loop Until Clean

Repeat Steps 1–3 until:
- ✅ All tests pass
- ✅ All builds succeed (Windows + Android)
- ✅ All valid code review issues are addressed
