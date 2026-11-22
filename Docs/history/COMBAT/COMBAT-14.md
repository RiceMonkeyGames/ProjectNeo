# COMBAT-14 | 2026-11-22 | COMBAT - Ranged Target Priority, Melee-Threat Response & Hold Facing Restore

Branch: `feature/combat-14`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim COMBAT-14`). P2 task from [Q-60](../Q/Q-60.md) (AI improvement pass). Design record: [Combat](../../design/Combat.md) (COMBAT-14 bullet under Retaliation & Target Switching). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Code map: [CombatAndSquads](../../codemap/CombatAndSquads.md).

## Decisions (user, 2026-11-22)

1. Melee-threat response: both an immediate target switch and a short fallback when there is room (not only one of them).
2. Threat priority applies to ranged units of both teams (enemy archers too), not only allies.
3. Agent choices noted for review: threat factors are linear distance fractions squared into the score (ladder climber 0.5, siege breaker 0.6, ranged 0.75); Hold finders keep ignoring the boss preference scale as before; Hold facing restore uses only directives that carry a formation facing (squad `Move` slot orders and `Hold` re-slots), so plain `Hold` and siege-brain parking never fight logic that turns units itself.

## Implementation

- New pure rules: `TargetThreat`, `ITargetThreatSource`, `RangedTargetPriorityRules` (classify, factor clamp, score, strict upgrade check), `RangedMeleeThreatRules` (threat radius, fallback point, level/post-leash check, short-path check), `HoldFacingRules` (shortest-way turn step).
- `CharacterActor.Threat` / `ThreatSource`; `SiegeAttackerBrain` registers itself and reports `LadderClimber` (Raising/Climbing) or `SiegeBreaker` (ToGate/ToWall/ToCannon).
- `UnitAISettings`: threat factors, `RangedThreatPriority`, `RangedMeleeThreatRadius`, fallback distance/cooldown/max time/Hold leash, `HoldFacingRestore` (all extended in `Create`). Existing assets pick up the defaults without an asset edit.
- `UnitCombatAI`: threat-weighted scoring in `FindTarget` / `FindTargetInReach` / `FindTargetNearPost` / `PriorityOf`, throttled `TryUpgradeThreatTarget`, `UpdateMeleeThreat` + `ContinueFallback` (runs before the decision step so the approach path cannot overwrite the fallback path), `SetDirective(directive, destination, facing)`, `RestorePostFacing`, `HoldFacing` / `IsFallingBack` read-outs.
- `SquadCommandCenter` passes `order.Facing` for `Move` slot orders and `Hold` re-slots; `LadderTraveller.Defer` carries the facing through a deferred directive.

## Verification

- `code-rules-check.ps1`: 0 violations, no serialized asset changes.
- Compile: passed (dotnet pre-check skipped: stale generated csproj did not list the new files; Unity compile passed).
- EditMode: 735/735 passed (new `RangedThreatRulesTests`, 10 tests).
- PlayMode: 324/324 passed (new `RangedThreatPlayTests`, 7 tests: ladder-climber preference, upgrade after lock, Engage switch + fallback, fallback disabled, Hold facing restore, plain Hold no turn, Move arrival facing). The full run takes about 179s, at the wrapper's 180s wait limit; the final result was read from `unity command test status`.
- A defect found during verification was fixed: the Engage decision step replaced the fallback path with an approach path, so the archer walked into the melee enemy.
- Manual play: not run before completion (user requested `/finish` on 2026-11-22); checks to do in `Game.unity`: archers on Hold against close melee enemies, ladder climber / gate breaker priority, formation facing after a line Move order.

Notes: all values are temporary until playtest. Teammate-owned files touched minimally: `SiegeAttackerBrain` (threat report) and `LadderTraveller` (facing on deferred directives).
