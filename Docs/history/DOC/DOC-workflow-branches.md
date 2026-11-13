# DOC-workflow-branches | 2026-11-12 | Workflow - Task Branch Lifecycle

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-12: DOC-workflow-branches - Task Branch Lifecycle

- User decision: Select and claim a backlog task, sync latest shared `develop`, then create `feature/<task-description>` and record the branch in the backlog before implementation.
- After user-owned code merge into `develop` is confirmed remotely, delete the corresponding remote task branch. Retain unmerged work. Purpose: Keep branch references manageable across agents. Immediate backlog claim/completion synchronization remains the existing exception to user-owned commits and merges.
- Updated AGENTS.md, Backlog.md, Context.md Section 3, and ImplementationPlan.md Sections 2 and 7. No game rules changed.
- Verification: Documentation consistency and `git diff --check`. No Unity execution or tests required; no actual branches created or deleted for this documentation update.
- Manual verification: Read the workflow in AGENTS.md, Backlog.md, Context.md Section 3, and ImplementationPlan.md Sections 2 and 7. Confirm claim sync -> latest develop -> feature branch -> implementation/verification -> user merge -> confirmed remote integration -> remote task branch deletion. No runtime reset required.
