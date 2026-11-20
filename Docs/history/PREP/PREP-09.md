# PREP-09 | 2026-11-21 | Preparation - Apply Final Squad Caps (10 Squads x 20)

Last Updated: 2026-11-21 01:28:01 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [Combat](../../design/Combat.md), [Decisions-Resolved](../../plan/Decisions-Resolved.md), [Backlog](../../Backlog.md)

Branch: `feature/prep-09`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim PREP-09`).

## Background

Q-57 (team meeting, 2026-11-19) set the final caps: at most 10 squads with 20 soldiers each. The backlog row described the data as the temporary G-1 3 x 4, but the Game scene actually used the shared `Data/SquadCommandPrototype/SquadConfig.asset` (10 squads x 10) and `Data/WallCommandPrototype/Command.asset` (select keys `F1`-`F4`). Both assets are also used by the teammate prototype scenes (WallCommand, SquadCommand). The code already left the captain out of the member cap (`Squad<T>.Capacity` counts members only).

## Decisions

- User, 2026-11-21: the hero captain does not count toward the 20 (a full squad is 20 soldiers + 1 captain; current code kept).
- User, 2026-11-21: squads 1-10 use `F1`-`F10` until Q-11 sets the final keys.
- User, 2026-11-21: the Game scene gets its own squad config and command key assets; the shared prototype assets stay unchanged.
- User, 2026-11-21: the 200-ally performance check moves to Q-10 (not measured here).
- Recorded in [Combat](../../design/Combat.md) and the Q-57 row of [Decisions-Resolved](../../plan/Decisions-Resolved.md).
- Agent defaults (open to change): the squad organization window (`O`) card area now scrolls both ways (mouse wheel scrolls vertically; a thin horizontal bar under the cards and dragging move sideways), and each card is as tall as its rows; the top-left squad list grows to 236 px (reference 1280x720) and the enemy intel window moves down to stay below it; the Game command asset copies the WallCommand formation (2 columns, 1.1 m spacing), so a full squad stands 10 ranks deep until COMBAT-08.

## Changes

- Data via Editor APIs: new `Editor/SquadCapsSetup` (menu `ProjectNeo/Setup/Apply Final Squad Caps (PREP-09)`) creates `Data/GameFlow/SquadConfig.asset` (10 x 20) and `Data/GameFlow/Command.asset` (copy of the WallCommand settings with `F1`-`F10`), links them to the Game scene `SquadCommandCenter`, resizes the squad list, moves `EnemyIntel`, changes the help text to `F1-F10`, and applies the organization scrolling. New assets are saved and reloaded before linking (the first run otherwise left the scene `config` reference empty).
- `SquadOrganizationSetup.ApplyScrolling` (also called by `BuildPanel`): vertical + horizontal scrolling, top-anchored content with a vertical `ContentSizeFitter`, horizontal scrollbar.
- `GameSquadOrganization`: `FitCardHeights` sets each card's `LayoutElement.minHeight` from its row count (at least the visible area); `CardAt(index)` for tests.
- `SquadCommandHud.SelectKeys` (key range from the settings asset); the placement hint in `GameFlowHud` uses it instead of the fixed `F1-F4`.
- `GameBattleSetup.SquadListHeight` (236) and `EnemyIntelHudSetup.Offset` (y -272) so re-running those setups keeps the new layout; `GameBattleSetup` help text `F1-F10`.
- Tests: PlayMode `GameSquadCapsPlayTests` (Game scene caps and keys; a squad filled to 20 at the barracks shows all 21 rows in its organization card and the last row comes into view when scrolled to the bottom).

## Verification

- `Tools/Agent/unity-verify-local.ps1 -Mode play -SkipDotnet -Filter GameSquadCapsPlayTests`: compile passed, 2/2 passed.
- `Tools/Agent/unity-verify-local.ps1 -Mode all -SkipDotnet`: compile passed, EditMode 604/604, PlayMode 284/284.
- `Tools/Agent/code-rules-check.ps1`: passed (0 violations). `git diff --check` reports only Unity's blank `m_Name:` lines in `Game.unity`.
- Manual play: not done yet.
- Performance with 200 allies: not measured (Q-10).

## Remaining

- Manual check in `Game.unity`, then finish.
