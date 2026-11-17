# ProjectNeo Development Backlog

Last Updated: 2026-11-18 02:54:33 (KST)
Source: [Google Docs (Legacy)](https://docs.google.com/document/d/1zyEvUQFRwFjPP0i5M34nC7sStyZT3s60drXyg_ctz_Y/edit?tab=t.0#heading=h.c9sft4i3oi7)

> Purpose: Active task selection, assignment, and status tracking (`Done` tasks live in [Backlog-Done.md](archive/Backlog-Done.md)). | Parent: [INDEX](INDEX.md) | Related: [Collaboration](workflow/Collaboration.md), [Status](plan/Status.md)

## Workflow & ID Summary

- **Claim & Sync**: Pick `Waiting` → mark `In Progress` + assignee here → **immediately commit & push to `develop`** → create `feature/<task-id-lowercase>` from latest `develop` and record branch here. Never modify another assignee's `In Progress` item.
- **Completion**: Update affected leaves/history and present verification; after explicit user confirmation mark Done, archive, and commit/push the task branch. The user handles develop merge and remote branch deletion. Immediate claim commit/push remains authorized.
- **Task IDs**: Allowed chars `A-Z`, `0-9`, `a-z`, `-` only (1 task = 1 ID; no `~`, `·`, `.`, or ad-hoc suffixes). M3+ features use `<DOMAIN>-<NN>` (`COMBAT`, `SIEGE`, `FLOW`, `PREP`, `META`, `CONTENT`, `ART`, `UI`, `AUDIO`); cross-cutting use `Q-NN`, `BUG-<slug>`, `PERF-<slug>`, `QA-<slug>`, `DOC-<slug>`, `TOOL-<slug>`. Full rules in [Collaboration.md](workflow/Collaboration.md).
- **Statuses**: `Waiting` → `In Progress` → `Waiting for Link` / `Waiting for Verification` → `Done` (`Pending Decision`, `Waiting for Prerequisite` for blockers).

---

## Active Backlog Tasks

| Task ID | Description | Status | Assignee | Completion Criteria & Prerequisites |
|---|---|---|---|---|
| PREP-06 | Preparation Time Clock | In Progress | Hwang Seong-min | Branch `feature/prep-06`. Registered 2026-11-18 (Q-53). 12-hour budget per preparation in data; clock HUD; clock actions (blacksmith craft/upgrade 1h, Wait 1h; scouting cost used by C-5) blocked when hours are short; free actions unchanged; unused hours lost; budget restored each preparation and kept in run save. Rules: [PrepEconomyMeta](design/PrepEconomyMeta.md). |
| C-5 | Wave Preview & Hidden Enemy Intel | Waiting | Unassigned | Rules decided 2026-11-18 (Q-53): whole-stage enemy types and counts in preparation; per-group hidden flag in stage data shown as `? × N`; 3h scouting action reveals every hidden group; revealed on appearing in battle. Preview can start now; the scouting time cost needs PREP-06. Hero trait/perk reveal follows META-02. Rules: [MoraleAndWave](design/MoraleAndWave.md). |
| META-02 | Runtime Perk Injection & Reward Routing | Pending Decision | Unassigned | Registered 2026-11-16: apply perk payloads to real run/battle stats and route victory unlock points. Blocked by Q-06 (perk allocation timing/pause) and Q-07 (unlock point distribution timing). |
| COMBAT-06 | Minimal Hero/Active Skill Execution | Pending Decision | Unassigned | Registered 2026-11-16: skills exist as identifiers only. Blocked by Q-12 (skill rules, cooldowns, targeting, perk effects). Shared combat core edits need teammate coordination. |
| SIEGE-08 | Construction Delays (Work Orders) | Waiting for Prerequisite | Unassigned | Registered 2026-11-16 (formerly roadmap bundle `C-6` construction pacing); rules decided 2026-11-18 (Q-53). Placing pieces and ordering repairs become work orders built by 2 crews in order as the PREP-06 clock advances (1h per piece; repair = missing-HP fraction of 1h); unfinished pieces enter battle with HP equal to progress and block paths; demolishing unfinished work refunds fully. Needs PREP-06. Rules: [PrepEconomyMeta](design/PrepEconomyMeta.md). |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
