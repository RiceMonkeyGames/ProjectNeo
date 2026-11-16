# COMBAT-04 | 2026-11-16 | Action Interrupt & Poise System on Hit

Last Updated: 2026-11-16 11:59:22 (KST)

Branch: `feature/combat-04`. Owner: youngho. Claim synchronized to develop (`2383a08`, `docs: claim COMBAT-04`). Design record: [Combat](../../design/Combat.md) (Action Interrupt & Poise System).

## Decision (user, 2026-11-16)

| Item | Choice |
|---|---|
| Model | Single-hit threshold comparison (`ActionPoise.IsInterruptible && incomingStagger >= ActionPoise.Poise`) |
| Initial scope | Ladder pushing/raising (shared across player, allies, enemies); extensible via `IInterruptibleAction` |
| Ladder rules | Ladder pushing interrupted by both ranged and melee hits (settles back onto wall before balance point); ladder raising uninterruptible |
| Cooldown & feedback | `0.3s` re-interaction cooldown after interruption; temporary test HUD notice on player action interruption only |

## Implemented scope

- **Combat poise rules**: `ActionPoise`, `IInterruptibleAction`, `ActionInterruptRules` (pure C#), `CombatProfile.StaggerPower` (default `10f` on serialized assets; `10f` projectile / `25f` melee in `Create(...)`), and `CharacterActor` interrupt registration/evaluation (`RegisterInterruptible`, `UnregisterInterruptible`, `ActionInterrupted` on effective damage).
- **Ladder integration**: `SiegeSettings.LadderPushPoise` (`10f`) and `LadderPushInterruptCooldown` (`0.3f`); `LadderTraveller` implements `IInterruptibleAction` for player ladder pushing with `0.3s` retry block and `1.5s` `HasInterruptNotice` test flag; `LadderPushInterruptHandle` + `SiegeLadder` manage allied AI push poise and `0.3s` cooldown.
- **Test HUD notice**: `GameFlowHud` displays `"피격으로 사다리 밀기가 중단되었습니다!"` / `"Ladder push interrupted by hit!"` when `LadderTraveller.HasInterruptNotice` is active.

## Verification

Compile passed. Full `Tools/Agent/unity-verify.ps1 -Mode all` passed EditMode **398/398** (7.3s) and PlayMode **202/202** (106.49s). New tests: EditMode `CharacterTests.ActionInterruptRules_ComparesStaggerAgainstPoiseAndInterruptibility`; PlayMode `SiegePrototypePlayTests.Ladder_AllyPushInterruptedByRangedHit_SettlesBackAndEnforcesCooldown` and `Ladder_EnemyRaiseNotInterruptedByHit`. Dotnet precheck skipped (no csproj or dotnet).

Physical-key manual play (`F` hold ladder pushing interrupted by enemy hit in `Game.unity` / `SiegePrototype.unity`) and standalone build are not claimed as verified.
