# COMBAT-03 | 2026-11-14 | Morale - Combat effects and squad HUD

Last Updated: 2026-11-14 20:31:18 (KST)

Owner: 신용준. Branch: `feature/combat-03`. Claim synchronized to develop in `caca459`. Done on user authorization, 2026-11-14. The user approved completion/archive/commit/push, develop merge and local/remote task-branch deletion.

## Decisions and shared scope

- The user chose Panic movement to a retreat point followed by waiting until battle end. New orders are ignored during this state; units stay active and do not become Fled. Morale-0 rout retains its existing exit behavior. The runtime `PanicRetreatPoint` is initially placed at the existing courtyard retreat location, with existing retreat arrival settings and normal Hold self-defense.
- The user confirmed no other active edits to SquadCommander, SquadCommandCenter, GameSquadMorale and SquadCommandHud. CharacterActor and UnitCombatAI remain unchanged; their existing Stats/SetDirective APIs are used.
- [Design](../../design/MoraleAndWave.md), [decision record](../../plan/Decisions-Resolved.md).

## Implementation

- `GameSquadMorale`: applies the existing `AttackMultiplier` through source-owned Percent Attack modifiers to living grunts and captains. Updates immediately after morale changes and checks for casualty-only changes each frame; removes only its own modifiers at battle end/disable/destruction. Existing equipment modifiers stay intact. Captain-only squads also receive a battle wrapper.
- `SquadCommander.OrderRequested`: a per-selected-squad veto before the saved order changes; rejected orders leave prior order and applied directives intact. Arrival completion bypasses the new-order filter. Fear rolls once per squad per issued order. Panic/routing squads reject commands without a Fear roll.
- `SquadCommandCenter.Detach`: recognizes leaders as well as members, so a panicking/routing captain cannot be pulled out by new formation orders.
- `GameSquadMorale.Update`: ticks the existing Panic/Contagion rules. Panic detaches the squad, moves it to its point and holds there without exit/time-out; repeated ticks do not restart movement. At 0 morale Panic escalates to existing rout; battle end reattaches surviving panicked units and clears transient state. Contagion uses the existing nearby-squad propagation and can cause checks/routs.
- `SquadCommandHud`: extends the existing TMP label with ally morale, localized Collapse/Awaken/Rout state, and the latest one-line check/order/Panic notice. Prototype scenes without GameSquadMorale retain their existing display. The source is resolved once at Start, not searched every frame.
- No serialized asset, prefab, scene, package or persistent enum changes.

## Verification

- Focused GameMoralePlayTests: compile passed, **9/9** passed (4.56s) before the final captain/weak-effect/Rally/Steadfast additions.
- Full `Tools/Agent/unity-verify.ps1 -Mode all -SkipDotnet`: compile passed, **EditMode 394/394** (7.24s), **PlayMode 193/193** (101.22s), including 11 GameMoralePlayTests and the new SquadCommander veto regression.
- API-driven live Game/HUD check: frames/time advanced (11832/22.50s to 19773/37.95s), Panic state and notice displayed without TMP overflow, screenshot `combat-03-panic-hud.png` saved outside Assets. All four panicked members reached the point and held within 1.85–2.44m, remained active, and had Fled=false. Current console ground truth had 0 errors and compilationFailed=false. Play stopped and run-in-background restored to false; no serialized files changed. This is not a human physical-input playtest.
- Dotnet precheck skipped because generated project files are stale (recorded in SIEGE-05). No standalone build or human physical-input manual play claimed.

## Manual verification and reset

1. Open `Assets/_Project/Scenes/Game.unity`, enter Play, hold `B` for one second to start battle. The existing top-left command overlay should show each allied squad's morale and state.
2. Use **Window > General > Test Runner > PlayMode**, filter `GameMoralePlayTests` and run the fixture for deterministic effect checks. Added tests cover high/low attack bands and equipment-preserving cleanup across battles, Last Stand casualty scaling, Fear veto/one roll/arrival completion, Panic waiting and Fled distinction, Panic-to-rout escalation, actual frame-driven nearby Contagion, HUD text, captain effects, weak starting Collapse, Rally and Steadfast.
3. Expected: ignored orders preserve previous orders; Panic members and captain remain visible/alive at the point until battle end; morale-0 rout still exits; attack modifiers do not accumulate or remain after battle; nearby Contagion affects only nearby allies.
4. CLI: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/unity-verify.ps1 -Mode play -Filter GameMoralePlayTests -SkipDotnet`. For command veto rules: `-Mode edit -Filter SquadCommanderTests -SkipDotnet`. Run sequentially in an idle editor.
5. Stop Play to reset all runtime morale, the Panic point and temporary state; tests unload their scenes and reset GameSession/capture delta time. If interrupted, stop Play and reopen Game. Do not save temporary inspection state to assets.
