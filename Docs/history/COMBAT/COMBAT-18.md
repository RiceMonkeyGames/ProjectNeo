# COMBAT-18 | 2026-11-23 | COMBAT - Hero Auto-Skill Urgency Priority, Area Heal Clustering & True Engagement Check

Branch: `feature/combat-18`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim COMBAT-18`). Follow-up to [COMBAT-13](COMBAT-13.md). Design record: [Combat](../../design/Combat.md) (COMBAT-18 details). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Code map: [CombatAndSquads](../../codemap/CombatAndSquads.md).

## Decisions (user, 2026-11-23, recommended options accepted)

1. Urgency order: critical heal > morale stabilization > high-value offense > plain heal > general; equal urgency goes to the earlier slot. Plain heal (at or below the heal threshold, above the critical ratio) sits below high-value offense.
2. Melee engagement uses a height-gap limit (`MeleeEngageMaxHeight`, temporary 2.5 m) rather than a NavMesh path query.

Agent choices noted for review: critical heal ratio `0.3`, high-value area count `4`, a stagger that catches a boss, siege breaker, or ladder climber casts even below the minimum enemy count, and a buff skill that also raises morale ranks as morale stabilization when target morale is low.

## Implementation

- `SkillAutoUrgency` (new enum, explicit values): `None`, `General`, `Heal`, `HighValueOffense`, `MoraleStabilize`, `CriticalHeal`.
- `SkillAutoSettings`: new `criticalHealBelowRatio` (0.3), `highValueEnemiesInArea` (4), `meleeEngageMaxHeight` (2.5, `0` disables), clamped in `Create`. Existing `SkillAuto.asset` picks up the defaults without an asset edit.
- `SkillAutoRules`: `HealUrgency`, `AreaOffenseUrgency`, `UnitOffenseUrgency`, `SupportUrgency`, `MostUrgent`, `WithinEngageHeight`, `HasStagger`, `HasMoraleGain`, and `BestHealCenter` (sum of missing health ratios inside the radius). `BestClusterCenter` now wraps the same weighted helper with unchanged results.
- `HeroSkillAI`: every ready slot is scored each think and the most urgent is cast; a failed cast falls through to the next. Stagger skills prefer high-value enemies (`TargetSpreadRules.IsFocusTarget`). Aimed-point heals use `BestHealCenter`. Buff and morale engagement checks apply the melee height gap. No change to `UnitCombatAI` movement or attack decisions.

## Verification

- `code-rules-check.ps1`: 0 violations, no serialized asset changes.
- Compile: no CS errors. EditMode: 852/852 passed (new `AutoRules_UrgencyOrderAndTiers_COMBAT18`, `AutoRules_MostUrgent_HighestThenEarliestSlot_COMBAT18`, `AutoRules_EffectFlagsAndEngageHeight_COMBAT18`, `AutoRules_BestHealCenter_WeightsMissingHealth_COMBAT18`).
- PlayMode: `HeroSkillAIPlayTests` 13/13 (5 new `*_COMBAT18`), `GameSkillPlayTests` 5/5, `SkillCasterPlayTests` 10/10.
- Full PlayMode suite: 333/352. All 19 failures are outside skills (`GameBattlementPlayTests`, `StackedConstructionPlayTests`, `GameFlowPlayTests`, `GameBattlePlayTests`, `GameOrganizationPlayTests`, `GamePerkPlayTests`, `GameUnlockPlayTests`, `PlayerHealthHudPlayTests`). `GameFlowPlayTests` and `StackedConstructionPlayTests` fail the same way on clean develop (`53a74ee` + claim), so they are pre-existing. The other classes were not re-run on develop. `unity-verify.ps1` stops waiting for PlayMode after 180 s, so the full suite reports `TIMEOUT`; results were read from `unity command test status`.
- Manual play: user confirmed on 2026-11-23 (`Title.unity` -> new run -> `Game.unity`). No sample aimed-point heal or area stagger skill exists yet, so heal clustering and area stagger are covered by PlayMode tests only.
