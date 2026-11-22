# COMBAT-13 | 2026-11-22 | COMBAT - Hero Auto-Skill Targeting & Condition Improvements

Branch: `feature/combat-13`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim COMBAT-13`). P1 task from [Q-60](../Q/Q-60.md) (AI improvement pass). Design record: [Combat](../../design/Combat.md) (COMBAT-13 details). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Code map: [CombatAndSquads](../../codemap/CombatAndSquads.md).

## Decisions (user, 2026-11-22, recommended options accepted)

1. Morale threshold as a ratio of maximum morale: a morale-only skill casts only while engaged and when the lowest morale among target squads that can still gain morale is at or below `MoraleGainBelowRatio` (temporary `0.75`). Skills that also buff keep the engaged-only rule.
2. Area cluster aim: aimed-point area attacks pick the enemy position within skill range whose radius catches the most enemies; ties go nearest the current target (or the hero).
3. Own-range lookup: when the current target is missing or out of skill range, use the nearest enemy within the skill's own range; buff/morale engage checks use the nearest enemy within the engage distance.

## Implementation

- `SkillAutoSettings`: new `moraleGainBelowRatio` field (default `0.75`, clamped in `Create`). Existing `SkillAuto.asset` picks up the default without an asset edit.
- `SkillAutoRules`: `IsMoraleOnly`, `ShouldRaiseMorale`, `MoraleRatio`, and pure `BestClusterCenter` (radius + body radius, tie-break by distance to a preferred point).
- `ISkillContext.TryGetMoraleRatio` (new member); `GameSkillContext` reads `GameSquadMorale.BattleOf` and returns false for missing or routed squads.
- `HeroSkillAI`: own-range nearest-enemy fallback for `TargetUnit` offense and support engage checks, cluster aim for `AimPoint` offense, morale check for morale-only support skills. No change to `UnitCombatAI` movement or attack decisions.

## Verification

- `code-rules-check.ps1`: 0 violations, no serialized asset changes.
- Compile: no CS errors. EditMode: 725/725 passed (new `AutoRules_MoraleThreshold_COMBAT13`, `AutoRules_BestClusterCenter_COMBAT13`).
- PlayMode: `HeroSkillAIPlayTests` 8/8 (new `TargetUnit_FindsNearestEnemyInSkillRange_WhenTargetMissingOrOutOfReach`, `AimPointArea_AimsAtBestClusterInsteadOfCurrentTarget`, `MoraleOnly_WaitsUntilSquadMoraleIsAtOrBelowThreshold`; `TargetUnit_UsesCurrentTargetOnlyInRange` updated for the fallback), `SkillCasterPlayTests` 10/10, `GameSkillPlayTests` 5/5.
- Manual play: user went ahead with completion on 2026-11-22 (`Title.unity` -> new run -> `Game.unity`; rally threshold, hobble fallback, shield bash on `Hold`). No sample aimed-point area attack skill exists yet, so cluster aim is covered by PlayMode tests only.

Notes: starting morale (150 of 200) sits exactly on the 0.75 threshold, so rally can fire on the first engagement; lower the ratio in `SkillAuto.asset` after playtest if it should be saved for later.
