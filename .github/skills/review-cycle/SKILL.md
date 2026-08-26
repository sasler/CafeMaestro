---
name: review-cycle
description: >
  Request GitHub Copilot review on a PR and address all comments.
  This is an ATOMIC step — do not yield until all comments are addressed and replied to.
  Use after pr-workflow.
---

# Review Cycle

## Step 1 — Request Copilot Review

After the PR is created, **request a review** from GitHub Copilot on the PR. Wait until the review is fully complete before proceeding.

## Step 2 — Address Review Comments

> **ATOMIC:** Do not yield, stop, or mark the task complete until ALL comments have been both addressed in code AND replied to on the PR. Partial completion is NOT acceptable.

For each comment/suggestion:

1. **Read carefully** — understand what the reviewer is flagging
2. **Evaluate critically** — the reviewer is NOT always right
3. **If you agree:** Implement the fix or improvement
4. **If you disagree:** Prepare a clear explanation of why

After implementing changes:
- Run all tests and builds again
- Reply to **every single** inline comment explaining what you did
- If you disagreed, reply with your reasoning
- **Verify** that no unreplied comments remain before proceeding

## Step 3 — Final Verification

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
