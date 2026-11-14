# ProjectNeo Development Backlog

Last Updated: 2026-11-14 16:21:43 (KST)
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
| QA-game-loop | Manual Playtest of Current Game Scene | Waiting for Verification | Unassigned | Formerly `B-9.3-play`. First user playtest reported 2026-11-13; findings in [QA history](history/QA/QA-game-loop.md). Victory → reward → next-prep flow verified by user via QA-victory-flow (2026-11-13). Recheck construction and combat after follow-ups below. COMBAT-01/COMBAT-02 crouch/sprint and refined view transition confirmed by user on 2026-11-13; this does not close the remaining game-loop QA scope. |
| QA-ladder-controls | Manual Check of Shared Ladder Physical Keys & HUD | Waiting | Unassigned | From SIEGE-04 (physical-key/HUD review unverified). Verify tap F boarding, hold F toppling/raising, W/S traversal, FIFO occupancy, allied ladder orders, and HUD prompts in Game scene with real input. BUG-ladder-push-visual is Done; include its balance-point motion and occupant fall behavior. |
| QA-build-zoom-ui-hover | Manual Check of Construction Wheel Zoom over UI | In Progress | 신용준 | Branch `feature/qa-build-zoom-ui-hover`. From BUG-build-camera-zoom (UI-hover check unverified). In construction mode, scrolling over UI panels must not zoom the camera (or behaves per agreed rule); scrolling over the world zooms normally. Open a BUG task if behavior is wrong. |
| B-7.1 | Formal Wave Data & Spawning Rules | Waiting for Prerequisite | Unassigned | After enemy definitions and spawn/termination contracts are agreed upon, spawn according to configured types, counts, and timings. No duplicates on pause/restart. |
| SIEGE-01 | Link Pending Spawns to Win/Loss Evaluation | Waiting for Prerequisite | Unassigned | Formerly `B-9.2-wave` (fulfills `B-9.2` wave-spawn link). After B-7.1, prevent premature victory if enemies remain to spawn; ensure battle termination and outcomes trigger exactly once. |
| COMBAT-03 | Morale Collapse/Awaken Effects in Combat & Morale HUD | Waiting for Prerequisite | Unassigned | After B-8.2 (Q-44). Apply band/Last Stand attack modifiers (`StatSheet.AddModifier`), Fear `ShouldIgnoreOrder`, Panic retreat, Contagion to nearby squads in real combat; minimal `SquadCommandHud` morale number/state/check notice. Sequence shared `CharacterActor`/`UnitCombatAI`/`SquadCommandCenter` edits. |
| C-5 | Wave Preview & Hidden Enemy Intel | Waiting for Prerequisite | Unassigned | Following B-7.1 and run contracts, display upcoming enemy types, counts, and hidden indicators in prep UI. Scouting rules clarified first. |
| B-9.3 | M1 Full Integration, Iteration & Build Verification | Waiting for Prerequisite | Unassigned | Following B-4~8 integration, test repeated cycles of destruction, enemy variants, waves, morale, and outcomes; verify regressions and standalone build startup flow. |
| PREP-04 | Combat Consumable Use | Pending Decision | Unassigned | User deferred (2026-11-13). Decide with Q-12: input keys (combat key ownership with teammate), quick slots, effects (e.g., potion heal %), limits. Existing data: `item_potion`. |
| BUG-wall-archer-los | 성벽 위 궁수 사선·사거리 및 Hold 반격 개선 | Pending Decision | Unassigned | 2026-11-14 수동 플레이 제보: 성벽 위 `Hold` 궁수가 아래 적 궁수에게 맞아도 사선 가림(`HasClearShot` 성벽 모서리 충돌) 또는 3D 거리 경계로 반격 못 함. 구현 전 결정 필요: ① 고지대 원거리 사거리 보너스 적용 여부 및 수치 ② `HasClearShot` 성벽 윗면/모서리 예외 처리 vs 사선 막힘 시 성벽 가장자리로 이동해 사선 확보 여부 ③ `Hold` 상태에서 사선 불가·경로 없음 시 행동 규칙. |
| SIEGE-05 | 사다리 밀기 속도·무게 페널티 및 배너로드식 개선 | Pending Decision | Unassigned | 2026-11-14 사용자 피드백: 현재 사다리 밀기(2초 고정)가 너무 빠름. 구현 전 결정 필요: ① 기본 밀기/세우기 시간 ② 사다리 탑승자 유무/인원수별 밀기 속도 저하 배율 또는 완전 차단 임계 인원 ③ 아군/적 다수 참여 시 협동 가속 여부 ④ 성벽 위 상단 출구 저지(병목 대치) 포함 여부. |
| COMBAT-04 | 피격 시 진행 중인 시전/상호작용 중단(Interrupt) 시스템 | Pending Decision | Unassigned | 2026-11-14 사용자 제안: 적에게 공격받으면 사다리 밀기 등 시전 동작이 끊어져야 함. 구현 전 결정 필요: ① 중단 대상 동작 범위(사다리 밀기/세우기, 대포 조작, 전투 시작 홀드 등) ② 중단 발동 조건(모든 피해 vs 최소 피해/경직 이상, 쿨다운 유무) ③ 중단 시 진행도 처리(초기화 vs 현재 기울기에서 복귀) 및 UI/피드백 안내. |

---

## When to Add or Update Backlog Items

1. **New Feature Requests / Design Decisions**: Register necessary tasks; mark open questions `Pending Decision`.
2. **Bugs, Missing Items, or Dependencies**: Check for duplicates and register immediately with Task ID, symptoms, impact, repro/expected/actual.
3. **Issues Found in Tests / Playtests / Feedback**: Record repro, expected, and actual results.
4. **Task Completion / Integration**: Register follow-ups/unverified scope; update existing items rather than creating duplicates.
5. **Subsequent Milestones**: Break down roadmap bundles as they draw near. Keep distant items bundled; registration alone does not imply start or assignment.
