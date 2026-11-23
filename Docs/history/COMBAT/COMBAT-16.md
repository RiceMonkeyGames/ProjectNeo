# COMBAT-16 | 2026-11-23 | COMBAT - Multi-Direction Ranged Melee-Threat Fallback Along Wall Walkways

Branch: `feature/combat-16`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim COMBAT-16`). Follow-up to [COMBAT-14](COMBAT-14.md). Design record: [Combat](../../design/Combat.md) (COMBAT-14 melee-threat response bullet). Code map: [CombatAndSquads](../../codemap/CombatAndSquads.md).

## Implementation

- `RangedMeleeThreatRules`: `FallbackCandidateCount` (5), `FallbackAngle(index)` (0, +45, -45, +90, -90 deg; out of range = 0), an angle overload of `FallbackPoint` (the old overload is angle 0), and `IncreasesSeparation` (strict horizontal distance increase).
- `UnitCombatAI.TryBeginFallback` tries the candidates in order through `TryFallbackPath` (NavMesh sample within 1m, same level, Hold leash, strict separation, complete short path, `HoldMaxDescent`) and starts the first one that passes (`StartFallback`). When every candidate fails it sets `RangedFallbackCooldown` (3s) instead of the old `DecisionInterval * 2` retry. Early exits (cooldown, attacking, on ladder) do not start the cooldown.
- No settings or serialized asset changes.

## Verification

- `code-rules-check.ps1`: 0 violations, no serialized asset changes.
- Compile: passed (dotnet pre-check skipped: the generated csproj was stale after pulling develop and still listed a removed file).
- EditMode: 835/835 passed (4 new `RangedThreatRulesTests`: angle order, rotated points, all candidates increase separation, strict separation).
- PlayMode: `RangedThreatPlayTests` 9/9 passed (new: `HoldArcherOnWalkway_ThreatFromSide_SidestepsAlongWalkway`, `HoldArcherOnPillar_NoRoomInAnyDirection_StaysAndShoots`). Full run: 335/341. The 6 failures were 5 Game-scene tests (`GameMoralePlayTests`, `GameOrganizationPlayTests`, `GamePerkPlayTests`, `GameUnlockPlayTests`, `PlayerHealthHudPlayTests`), which also fail on develop without this change, plus the pillar test, which was then fixed (the 2.5m pillar was too small for a NavMesh; now 3m).
- Manual play: see the manual checks presented at completion (archers on Hold on a wall walkway against a melee enemy coming from the side).
