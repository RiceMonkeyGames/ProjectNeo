# QA-ui07-test-drift | 2026-11-24 | QA - Game PlayMode Tests Updated for UI-07 Rule Changes

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

- **Assignee**: `SeongminHwang` | **Branch**: `feature/qa-ui07-test-drift` | **Claim commit**: `c56bfab`
- **Background**: four Game PlayMode tests had failed on `develop` since 2026-11-23. Earlier records only called them pre-existing ([SIEGE-18](../SIEGE/SIEGE-18.md), [QA-castle-kit-tests](QA-castle-kit-tests.md), [SIEGE-32](../SIEGE/SIEGE-32.md)). Found during SIEGE-33 verification.
- **Scope**: test files only. No runtime code, scenes or serialized assets changed.

## Causes and Changes

| Test | Cause | Change |
|---|---|---|
| `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits` | `86d76c3` (UI-07 work, 2026-11-23) keeps the player at top grade with the grade stat bonus (+30% attack). Percent modifiers add together in `StatSheet`, so +50% is 25 × (1 + 0.3 + 0.5) = 45, not 32.5 × 1.5. | Expected values come from `StatSheet.GetValueWith` with the same perk modifier. |
| `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses` | `762fb6d` (UI-07): new recruits become stored soldiers and are hidden (`GameUnitSpawner.Recruit` → `HideStoredSoldiers`), so counting active units gave 0. | Count `_Recruit` units with `FindObjectsInactive.Include`. |
| `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores` | `86d76c3`: the HP HUD now stays visible while a facility or equipment panel is open (user decision 2026-11-23, `PlayerHealthHud.LateUpdate` includes `IsPanelView`). | Assert the HUD stays shown and correct while a panel is open. |
| `GameOrganizationPlayTests.편성_키를_다시_누르면_창이_닫힌다` | Not a code defect. `InputSystem.settings.editorInputBehaviorInPlayMode` is `PointersAndKeyboardsRespectGameViewFocus`, so the virtual keyboard's O press only reaches the game while the Game view has focus. It fails on PCs that run PlayMode with an unfocused or minimized Editor ("Play Unfocused"), and passes with a focused Game view (it passed in earlier runs on other PCs). | Not changed. |

## Verification

- `code-rules-check`: passed (the only serialized change is the unrelated auto-generated TMP fallback font asset).
- Unity compile passed. Dotnet pre-check skipped (`-SkipDotnet`).
- Single classes: `GamePerkPlayTests` 1/1, `GameUnlockPlayTests` 3/3, `PlayerHealthHudPlayTests` 3/3 passed; `GameOrganizationPlayTests` fails only the O-key test (unfocused Game view).
- Full run (2026-11-24, Game view unfocused): EditMode 910/910; PlayMode 371/372 (the wrapper's 180s poll reported `TIMEOUT`; the final result was read with `unity command test status`). The one failure is the O-key test above.
- Manual play: not needed (test-only change).
