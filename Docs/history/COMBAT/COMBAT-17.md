# COMBAT-17 | 2026-11-23 | COMBAT - Melee Target Slot Congestion Penalty & Ranged Overkill Spread

Branch: `feature/combat-17`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim COMBAT-17`). Design record: [Combat](../../design/Combat.md) (COMBAT-17 bullet under Retaliation & Target Switching). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Code map: [CombatAndSquads](../../codemap/CombatAndSquads.md).

## Decisions (user, 2026-11-23)

1. Melee congestion: penalize only enemies whose inner slot ring is full (distance factor 1.5), not proportionally to occupancy.
2. Ranged overkill: fixed threshold (3 other shooters, then 1.25x distance per extra shooter), not a remaining-health estimate.
3. Focus targets that are never spread: ladder climbers, siege breakers, bosses. Enemy archers are spread.
4. Crenel reposition: the free crenel becomes the defender's new post (no return to the old post).
5. Agent choice noted for review: applies to both teams, as in COMBAT-14.

## Implementation

- New pure rules: `TargetSpreadRules` (`CongestionFactor`, `IsFocusTarget`, `OverkillFactor`) and `CrenelRepositionRules` (`ShouldReposition`, `IsCandidate`, `IsNear`).
- `MeleeSlotBoard`: `CountByTarget(ring, stillValid, output)` and a `Revision` counter on take/release/clear.
- `UnitCombatAI`: static active-unit list; `SpreadFactorOf` multiplies the threat factor in `ScoreOf`, `FindTargetInReach`, and `FindTargetNearPost` (`ScoreFactorOf`); inner-slot and ranged-shooter counts are recounted only when the board revision or any unit's `Target` changes, so units deciding in the same frame see earlier picks. `UpdateCrenelReposition` / `TryMoveToCrenel` / `IsCrenelOccupied` move a blocked `Hold` ranged unit once per blocked streak to a free crenel from the static `CrenelStandPoints` source and set it as `HoldPost`; `CrenelAnchor` keeps the ordered post.
- `CharacterActor.IsBoss` (set in `BossUnit.Setup`); `GamePrepPlacement` wires `UnitCombatAI.CrenelStandPoints` to `CastleBattlements.CollectCrenelStandPoints` and clears it on destroy.
- `UnitAISettings`: `TargetSpread`, `MeleeCongestionDistanceFactor`, `RangedOverkillThreshold`, `RangedOverkillDistanceFactor`, `CrenelRepositionDelay`, `CrenelRepositionRadius` (all in `Create`). Existing assets pick up the defaults without an asset edit.

## Verification

- `code-rules-check.ps1`: 0 violations, no serialized asset changes.
- Compile: passed (dotnet pre-check skipped: stale generated csproj).
- EditMode: 848/848 passed (new `TargetSpreadRulesTests`, 8 tests).
- PlayMode: new `TargetSpreadPlayTests` 6/6 passed (melee line spreads to the enemy behind a saturated one; spread off keeps everyone on the nearest; Hold archers split 3/3 across two grunts; a boss keeps all 6 shooters; a merlon-blocked wall archer moves to a free crenel, adopts it as its post, and shoots; no crenel source means no move). Full run: 328/347. All 19 failures also fail on develop without this change (verified with the change stashed): 15 grid/wall tests after the SIEGE-18 7.5m grid change (`GameBattlementPlayTests` 5, `StackedConstructionPlayTests` 8, `GameBattlePlayTests` 1, `GameFlowPlayTests` 1) and 4 of the Game-scene failures noted in COMBAT-16 (`GameMoralePlayTests` passed this run).
- Manual play: not run (the user asked to move on once automated behavior checks pass).
