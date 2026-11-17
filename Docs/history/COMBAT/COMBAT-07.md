# COMBAT-07 | 2026-11-17 | Morale - Hooks for morale skills, captains, and perk effects

Last Updated: 2026-11-17 16:29:15 (KST)

Owner: youngho. Branch: `feature/combat-07` (claim `f9d1a25`, fast-forwarded to develop `de26c16` before work). Normalized from M1 roadmap `B-8.3`. Status: Done 2026-11-17. The user confirmed the manual check and authorized completion, archive and commit/push of the task branch; develop merge is left to the user.

## Decisions (user, 2026-11-17)

- A morale gain never triggers the Breaking Point Check, even when it crosses `100`.
- A routed squad (morale `0`) receives no gain; a gain never cancels a rout.
- Sample hook: a battle-start perk effect (`PerkTrigger.BattleStart`), firing once per battle.
- Recorded in [MoraleAndWave](../../design/MoraleAndWave.md) (Gain Rules) and [Decisions-Resolved](../../plan/Decisions-Resolved.md).

## Implementation

- `BattleSquadMorale.ApplyGain(amount)`: single gain entry for perks/skills. Ignored when routed, disposed or non-positive; capped at max; suppresses the breaking point/rout evaluation while applying; returns the applied amount.
- `GameSquadMorale` implements `IPerkEffectContext`:
  - `AddMoraleToAllySquads(amount)` applies `ApplyGain` to every battle squad, only while a battle is active (no-op after battle end, disable or destruction).
  - `AddMorale(squad, amount)` for future targeted skills (COMBAT-06).
  - `UsePerks(PerkLoadout)`: at the end of `BeginBattle` (after morale wrappers and attack modifiers are ready) raises `PerkTrigger.BattleStart` once. No events are subscribed on the loadout, so nothing accumulates across battles.
  - `UseCaptainAwakenBonus(Func<UnitCombatAI, float>)`: called once per squad with a living captain at battle start; sets `AwakenChanceBonus`. Captain traits themselves do not exist yet.
  - Nearby Rally gains now go through `ApplyGain` (Contagion losses unchanged).
- Not done here: feeding the run's real `PerkLoadout` (META-02, blocked by Q-06/Q-07), player skills and `PlayerSkillUsed` (COMBAT-06, blocked by Q-12), `AllySquadMemberDied` trigger raising, captain trait data. No scene, prefab, asset, package or persistent enum changes.

## Verification

- `Tools/Agent/unity-verify.ps1 -Mode all`: dotnet precheck skipped (no csproj), compile passed, **EditMode 457/457** (7.85s), **PlayMode 219/219** (129.11s).
- New EditMode: `MoraleRulesTests.Gain_RaisesMorale_CappedAtMax_AndReturnsAppliedAmount`, `Gain_AcrossBreakingPoint_DoesNotRunOrUseTheCheck`, `Gain_OnRoutedSquad_IsIgnored_AndRoutStays`, `Gain_AfterDispose_IsIgnored`.
- New PlayMode (Game scene): `GameMoralePlayTests.BattleStartPerk_RaisesEverySquadOnce_SkipsRouted_AndStopsAfterBattle`, `CaptainAwakenBonus_AppliesOnlyToSquadsWithLivingCaptain`.
- No human manual play and no standalone build claimed. There is no in-game way to allocate a perk yet, so the hook is only reachable from code/tests until META-02.

## Manual verification

- Re-run only the new tests: Unity Test Runner → PlayMode → filter `GameMoralePlayTests`, and EditMode → filter `MoraleRulesTests.Gain`. Expected: all green.
- Regression play (`Assets/_Project/Scenes/Game.unity`): start a battle (hold B in prep), confirm squad morale HUD values, Rally/rout behavior and attack bands behave as before. Without a perk assigned, morale at battle start is unchanged from previous builds.
