---
name: sync-summary
description: Summarize remote origin/develop changes, Docs/backlog/ task updates, and Runtime public/internal API signature diffs since HEAD in bounded output. Use before resuming work or running /sync-summary.
disable-model-invocation: false
---

# /sync-summary — Remote Change Digest

Summarize teammate/upstream changes on `origin/develop` without reading full files or long diffs.

## Arguments
- `$ARGUMENTS`: Optional `-NoFetch` or `-BaseRef <ref>` (defaults to `origin/develop`).

## Steps

1. Run the summary wrapper:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/sync-summary.ps1
   ```
2. Read the bounded sections:
   - `BASE_REF`, `COMMITS_BEHIND`, `COMMITS_AHEAD`, `UP_TO_DATE`
   - `CHANGED FILES` list
   - `BACKLOG DIFF` entries (added/modified/removed `Docs/backlog/<TASK-ID>.md` summaries)
   - `RUNTIME PUBLIC API CHANGES` (`Assets/_Project/Scripts/Runtime/` `public`/`internal` signatures)
3. Report concisely in Korean: whether rebase/merge is needed, any shared-file or API conflicts with current work, and whether `Docs/plan/Team.md` coordination is affected.
