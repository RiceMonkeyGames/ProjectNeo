# QA-build-zoom-ui-hover | 2026-11-14 | Construction - Wheel Zoom over UI

Last Updated: 2026-11-14 15:14:13 (KST)

## Scope

- Owner: 신용준. Branch: `feature/qa-build-zoom-ui-hover`. Claim synchronized to develop in `9290c41`.
- Verify the remaining UI-hover scope from [BUG-build-camera-zoom](../BUG/BUG-build-camera-zoom.md) in `Assets/_Project/Scenes/Game.unity`.
- Agent operated the live Windows Unity Editor using OS mouse-wheel input through Computer Use. Entered construction via runtime `GamePhaseView.EnterConstruction()` for setup; walking to the station and F were not reverified.
- Initial QA required no changes; the user follow-up below led to a targeted setup/scene fix. Status: Done. User authorized completion/archive/commit/push, develop merge and local/remote task-branch deletion on 2026-11-14.

## Initial results (before the bottom-panel fix)

Unity 6000.6.8f1; existing Game view, Free Aspect, 1.5x scale. Each wheel action was followed by a screenshot and a runtime read of `ConstructionCameraController.ZoomSize` and `EventSystem.current.IsPointerOverGameObject()`.

| Input location / action | Zoom before → after | UI pointer | Result |
|---|---|---|---|
| World, wheel up one tick | 55 → 46.75 | false | Pass |
| Left construction button, wheel up one tick | 46.75 → 46.75 | true | Pass |
| Same button, wheel down one tick | 46.75 → 46.75 | true | Pass |
| Empty area of left construction panel, wheel up one tick | 46.75 → 46.75 | true | Pass |
| Return to world, wheel down one tick | 46.75 → 54.9999962 | false | Pass (float rounding) |

- Live frame counter advanced from 10313 at construction entry to 51537 at the final measurement; this was an advancing Play session.
- Console inspection returned two historical compile entries from 13:02 KST, before this QA. Current ground-truth console errors were 0; compilationFailed=false. No new runtime error appeared in the retrieved error entries.
- Stopped Play mode; restored temporary Run In Background override to false. Final state: Game scene, dirty=false, playing=false, compiling=false, scriptCompilationFailed=false, runInBackground=false.
- No automated tests or fresh compile were run: this task exercised existing behavior without implementation changes. Standalone build and human physical-wheel confirmation were not performed.

## User follow-up: bottom instruction panel

- User reported zoom works over the bottom `건설 모드 · Esc 돌아가기` panel. The initial agent checks covered only the left construction UI, so the original full-pass report was corrected.
- Cause: `GameFlowSetup.cs` created `InteractionPrompt` with `Image.raycastTarget=false`; its text also disabled raycasts. `GameFlowHud` displays the construction-return hint in that prompt.
- User requested blocking zoom over this panel. Recorded in [design](../../design/PrepEconomyMeta.md) and [resolved decisions](../../plan/Decisions-Resolved.md).
- Fix: setup sets the prompt background's raycastTarget=true for both new and existing prompts. Applied the same property to Game.unity using Editor APIs and saved it. The existing camera UI guard now also detects the bottom panel. Text and layout code unchanged.
- Editor save also serialized UI layout/style fields (anchors, positions, size, text-style hash and canvas additional shader channels); diff field-name audit found these plus the intended raycast property. No gameplay configuration changes were authored.

## Post-fix verification

- `Tools/Agent/unity-verify.ps1 -Mode compile`: DOTNET_PRECHECK=PASSED, COMPILE=PASSED.
- `Tools/Agent/unity-verify.ps1 -Mode play -Filter ConstructionWheelInput -SkipDotnet`: COMPILE=PASSED, PlayMode 1/1 passed, 1s. A Unity scene-save dialog initially blocked test startup; after saving the agent's scene changes, it completed successfully.
- Live Editor OS-wheel input after reloading the saved Game scene: bottom hint text / wheel up leaves zoom 55, UI=true; bottom panel background / wheel down leaves zoom 55, UI=true; return to world / wheel up changes 55 → 46.75, UI=false. Frame count advanced 1495 → 27980.
- Current console errors=0, compilationFailed=false. Stopped Play mode and restored Run In Background override to false. No standalone build run.
- Additional OS Escape attempt did not switch views (build=true afterward); Game-view keyboard focus was not established, so physical Escape behavior is not claimed verified by this task. The passing existing wheel test does not establish this either.

## Manual repeat / reset

1. Open `Assets/_Project/Scenes/Game.unity` and enter Play mode.
2. Walk to the construction station with WASD, face it and press F to open construction.
3. Scroll up over the world outside the left construction panel: the view zooms in. If inspecting ConstructionCamera, its initial size 55 becomes 46.75 for one tick.
4. Move over a left construction button, scroll in both directions, then scroll over the panel's empty area. Repeat over the bottom return-hint text and its panel background: the view must stay fixed.
5. Return the pointer to the world and scroll down: zoom resumes and returns to approximately 55.
6. Esc returns to first-person. Stop Play mode to reset the runtime castle and camera; do not save runtime changes.
