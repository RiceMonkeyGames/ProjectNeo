# ProjectNeo Development Backlog

Last Updated: 2026-11-13 20:57:51 (KST)
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
| BUG-ladder-fall-clipping | 낙하 시 적이 사다리를 관통하는 연출 개선 | Waiting | Unassigned | 사용자 수동 확인 (2026-11-13): 사다리에서 떨어지는 적이 사다리 외형을 관통함. 기대: 분리 위치·낙하 경로·외형 간격을 다듬어 관통이 눈에 띄지 않도록 개선. 사용자가 세부 연출은 추후 작업으로 명시하고 BUG-ladder-push-visual 현재 범위 완료 승인. 기존 균형점·낙하 피해·착지 규칙 유지 여부는 구현 전 확인. |
| COMBAT-01 | 플레이어 웅크리기 (Ctrl) | In Progress | 신용준 | Branch `feature/combat-01`. 사용자 요청으로 COMBAT-02와 함께 구현.  사용자 요청 (2026-11-13): Ctrl 키로 웅크리기 추가. 유지/토글 방식, 자세·충돌체·시점 높이 및 이동 속도는 구현 전 결정. |
| COMBAT-02 | 플레이어 달리기 (Shift) | In Progress | 신용준 | Branch `feature/combat-01`. 사용자 요청으로 COMBAT-01과 함께 구현.  사용자 요청 (2026-11-13): Shift 키로 달리기 추가. 유지/토글 방식, 달리기 속도 및 웅크리기와 동시 입력 시 동작은 구현 전 결정. |
| QA-game-loop | Manual Playtest of Current Game Scene | Waiting for Verification | Unassigned | Formerly `B-9.3-play`. First user playtest reported 2026-11-13; findings in [QA history](history/QA/QA-game-loop.md). Victory → reward → next-prep flow verified by user via QA-victory-flow (2026-11-13). Recheck construction and combat after follow-ups below. |
| BUG-ally-no-retaliation | Allies ignore out-of-range attackers | Waiting | Unassigned | Found by user in Game manual play (2026-11-13, during B-8.2 check): an enemy hits an ally squad member from outside that ally's reach (e.g. archers), and the ally stands still taking hits instead of chasing/attacking back. Likely cause: `UnitCombatAI` only engages within Hold radius (Q-22: melee `HoldEngageRadius` 6m, ranged range + 6m) and does not react to `CharacterActor.HitReceived` (event carries no attacker). Must be fixed. Before coding, agree with user on response rule (which directives retaliate, chase limit/return to post, wall-top units vs ground archers, interaction with Move/rout which must keep ignoring enemies). Shared combat core: sequence `CharacterActor`/`UnitCombatAI` edits. |
| QA-ladder-controls | Manual Check of Shared Ladder Physical Keys & HUD | Waiting | Unassigned | From SIEGE-04 (physical-key/HUD review unverified). Verify tap F boarding, hold F toppling/raising, W/S traversal, FIFO occupancy, allied ladder orders, and HUD prompts in Game scene with real input. BUG-ladder-push-visual is Done; include its balance-point motion and occupant fall behavior. Remaining ladder visual penetration is tracked as BUG-ladder-fall-clipping. |
| QA-build-zoom-ui-hover | Manual Check of Construction Wheel Zoom over UI | Waiting | Unassigned | From BUG-build-camera-zoom (UI-hover check unverified). In construction mode, scrolling over UI panels must not zoom the camera (or behaves per agreed rule); scrolling over the world zooms normally. Open a BUG task if behavior is wrong. |
| B-7.1 | Formal Wave Data & Spawning Rules | Waiting for Prerequisite | Unassigned | After enemy definitions and spawn/termination contracts are agreed upon, spawn according to configured types, counts, and timings. No duplicates on pause/restart. |
| SIEGE-01 | Link Pending Spawns to Win/Loss Evaluation | Waiting for Prerequisite | Unassigned | Formerly `B-9.2-wave` (fulfills `B-9.2` wave-spawn link). After B-7.1, prevent premature victory if enemies remain to spawn; ensure battle termination and outcomes trigger exactly once. |
| COMBAT-03 | Morale Collapse/Awaken Effects in Combat & Morale HUD | Waiting for Prerequisite | Unassigned | After B-8.2 (Q-44). Apply band/Last Stand attack modifiers (`StatSheet.AddModifier`), Fear `ShouldIgnoreOrder`, Panic retreat, Contagion to nearby squads in real combat; minimal `SquadCommandHud` morale number/state/check notice. Sequence shared `CharacterActor`/`UnitCombatAI`/`SquadCommandCenter` edits. |
| C-3 | Run Carry-over of Troop Casualties & Wall HP | Waiting for Prerequisite | Unassigned | Following C-1a and rout outcome contracts, carry over deaths, rout returns, and wall damage into subsequent battles without duplicate accounting. |
| C-5 | Wave Preview & Hidden Enemy Intel | Waiting for Prerequisite | Unassigned | Following B-7.1 and run contracts, display upcoming enemy types, counts, and hidden indicators in prep UI. Scouting rules clarified first. |
| B-9.3 | M1 Full Integration, Iteration & Build Verification | Waiting for Prerequisite | Unassigned | Following B-4~8 integration, test repeated cycles of destruction, enemy variants, waves, morale, and outcomes; verify regressions and standalone build startup flow. |
| D-8 | Squad Organization | Waiting for Prerequisite | Unassigned | Prerequisites: D-0, D-0b. Decisions: Q-32, Q-36. Completion: Squad reorganization UI (reassign grunts, assign captains, display headcount limits) reflects directly into battle commands and combat inputs. |
| D-3 | Tavern Hero Hiring | Waiting for Prerequisite | Unassigned | Prerequisites: D-0, D-0b, D-8. Decisions: Q-34, Q-36 resolved (2026-11-13). Completion: Hire heroes from unlocked pools per tier; pick squad to captain at hire (swaps existing captain to reserve); hero scene prefab, tavern spawning and captain squad orders (moved from D-0b, 2026-11-13); permadeath applies. |
| D-6 | Blacksmith Fixed-Recipe Crafting & Sales | Waiting | Unassigned | Prerequisites: D-0, D-1, D-7 (Done). Decision: Q-35 resolved (2026-11-13). Completion: Smithy pop-up lists recipes allowed by blacksmith level (data asset per level); crafting spends Gold + Materials and adds the item to the stash; equipment sells at the Shop ratio. Upgrades split to PREP-01. |
| PREP-01 | Blacksmith Equipment Upgrade Levels | Waiting for Prerequisite | Unassigned | Prerequisite: D-6 (shares the Smithy panel). Decision: Q-35. Completion: Per-item upgrade level stored on `ItemInstance` and kept through equip/unequip and run state; +10% equip modifiers per level; max +1/+3/+5 by blacksmith Lv1/2/3; step cost 50% gold price × target level + materials (data); sell refund includes upgrade spend at the sell ratio. |
| PREP-02 | Shop Gold Re-roll | Waiting | Unassigned | Decision: Q-46 (2026-11-13). Completion: Re-roll button in Shop pop-up; cost rises per re-roll within a prep phase (data, e.g., 10/20/30g) and resets next prep; re-roll is seed-reproducible via `IRandomSource`. Coordinate with PREP-03 (same files). |
| PREP-03 | Shop Offer Slot Locks | Waiting | Unassigned | Decision: Q-46 (2026-11-13). Completion: Free lock toggle per offer, max locked = Shop level; locked offers survive re-roll (PREP-02) and next prep restock; buying clears the lock. |
| PREP-04 | Combat Consumable Use | Pending Decision | Unassigned | User deferred (2026-11-13). Decide with Q-12: input keys (combat key ownership with teammate), quick slots, effects (e.g., potion heal %), limits. Existing data: `item_potion`. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
