# ProjectNeo Development Backlog

Last Updated: 2026-11-16 19:51:02 (KST)
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
| TOOL-asset-submodule | Third-Party Asset Submodule (Private Repo) | In Progress | youngho | Branch `feature/tool-asset-submodule`. Registered 2026-11-16: keep licensed third-party assets (e.g. Synty) out of the main repo so ProjectNeo can be published later without redistributing them. Private repo `RiceMonkeyGames/ProjectNeo-Assets` (LFS, initialized) mounted as a git submodule at `Assets/ThirdParty`; document clone/update workflow. No game code changes. |
| COMBAT-07 | Hooks for Morale Skills, Captains, and Perk Effects | In Progress | youngho | Branch `feature/combat-07`. Normalized from M1 roadmap `B-8.3` (prerequisites B-8.1, B-8.2, COMBAT-03 Done). Verify that a sample morale boost applies once to a real squad and unsubscribes cleanly (full skills in `COMBAT-06` / `META-02`). |
| C-5 | Wave Preview & Hidden Enemy Intel | Waiting for Prerequisite | Unassigned | B-7.1 Done 2026-11-16 (stage data readable without a running battle). Following run contracts, display upcoming enemy types, counts, and hidden indicators in prep UI. Scouting rules clarified first. |
| B-9.3 | M1 Full Integration, Iteration & Build Verification | Waiting for Prerequisite | Unassigned | Following B-4~8 integration, test repeated cycles of destruction, enemy variants, waves, morale, and outcomes; verify regressions and standalone build startup flow. |
| QA-physical-input | Physical-Input Manual Play Sweep | Waiting | Unassigned | Registered 2026-11-16: user-driven Editor play pass over the manual checks still recorded as unverified in history — PREP-01 (smithy upgrades), PREP-04 (`Q` potion), SIEGE-07 (cannon crosshair/traverse), COMBAT-03 (morale HUD/Panic), SIEGE-04 (ladder keys/HUD), FLOW-01 (hold `B`). Record pass/fail per item; register `BUG-<slug>` rows for failures. No code changes expected. |
| META-02 | Runtime Perk Injection & Reward Routing | Pending Decision | Unassigned | Registered 2026-11-16: apply perk payloads to real run/battle stats and route victory unlock points. Blocked by Q-06 (perk allocation timing/pause) and Q-07 (unlock point distribution timing). |
| COMBAT-06 | Minimal Hero/Active Skill Execution | Pending Decision | Unassigned | Registered 2026-11-16: skills exist as identifiers only. Blocked by Q-12 (skill rules, cooldowns, targeting, perk effects). Shared combat core edits need teammate coordination. |
| SIEGE-08 | Construction Delays | Pending Decision | Unassigned | Registered 2026-11-16 (formerly roadmap bundle `C-6` construction pacing). No delay rule is recorded in `Docs/design/`; the user must decide whether builds take time, how long, and whether it applies in prep only. |
| FLOW-02 | Run Save & Continue From Title | Pending Decision | Unassigned | Registered 2026-11-16 from the QA-standalone-build playtest: the Title screen has no "Continue" although the slot file remains, because META-01 saves meta progression only and the run is discarded. Blocked by Q-13 (mid-run save/resume). Decisions needed: save timing (suggested: start of each preparation), what a defeat or return to Title does to the run save, one run per slot or separate. Needed implementation: (1) serializable run snapshot of `RunState` (stage number, seed, wallet, inventory and item instances, heroes/grunts/squads, facilities, shop/tavern offers and locks, castle record, fallen heroes) using definition ids instead of asset references; (2) id → asset lookup for items, heroes, soldiers, facilities, pieces; (3) versioned run file per slot in `MetaSaveStore`'s directory with the same backup/quarantine rules; (4) `GameFlow` entry that resumes at preparation of the saved stage and `GameRunLink` rebuild of scene units/castle from the snapshot; (5) Title `Continue` (enabled only when a run save exists) next to `New Game`, which must confirm before discarding a saved run; (6) delete the run save on defeat/run end; (7) EditMode round-trip tests and PlayMode Title → Continue test. |
| UI-02 | Single-Language UI Text (Remove Runtime Korean Swap) | Waiting | Unassigned | Registered 2026-11-16 on user feedback: authoring UI in English and swapping to Korean at runtime is pointless; use one language (English, per the user) with no runtime translation. Scope: remove `RuntimeSystemFont` caption swapping and its serialized caption lists in `Title`/`Game`/prototype scenes (Editor API), and the 37 `korean ? … : …` branches in `GameFlowHud`, `GameRewardController`, `GameConsumables`, `CannonReticleView`, `TitleScreen` plus the setup scripts (`GameFlowSetup`, `GameFacilitySetup`, `GameBattleSetup`, `SquadOrganizationSetup`) and Korean literals in `GameFacilities`/squad HUD messages; update PlayMode tests that assert Korean strings. Touches teammate-owned HUD/combat UI files and shared scenes, so coordinate first ([Team](plan/Team.md)). Done when no UI string depends on the system Korean font and full EditMode/PlayMode pass. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
