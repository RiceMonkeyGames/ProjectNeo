# BUG-build-camera-zoom | 2026-11-13 | Construction - Wheel Zoom Input Units

Last Updated: 2026-11-13 14:07:40 (KST)

## Scope and cause

- Owner: Codex. Branch: `fix/bug-build-camera-zoom`, based on synced develop `88699a8` (claim and playtest feedback pushed before coding). User updated policy on 2026-11-13: AI may commit, merge and push within authorized task scope.
- Live Editor inspection confirmed Input System 1.20.1 with `scrollDeltaBehavior=UniformAcrossAllPlatforms`. Package source documents normalized ticks as 1; Windows platform-specific ticks as 120.
- ConstructionCameraController unconditionally divided scroll by 120. A normalized positive tick changed orthographic size from 55 to 54.92556 instead of the configured 46.75 (15% zoom).
- Fix: use normalized deltas directly; divide by 120 only for Windows editor/player with KeepPlatformSpecificInputRange. Preserve fractional/multiple steps, existing sensitivity, clamps, UI exclusion and navigation-disabled behavior. No serialized assets/settings changed.

## Verification

- Added `GameFlowPlayTests.ConstructionWheelInput_UsesNormalizedStepsAndRestoresCamera`, queuing MouseState and KeyboardState through the actual Update/input path using FocusIndependentInputScope. Restores original input behavior/devices in finally.
- Before fix: 1/1 failed at first normalized tick (expected 46.75, actual 54.92556), reproducing the report.
- After fix: GameFlowPlayTests 8/8 passed (3.30s), including normalized positive/negative/fractional steps, both zoom clamps, Home reset of zoom/focus, and Windows native 120-unit ticks.
- Full EditMode: 216/216 passed (0.75s); compilation reported failed=False, compiling=False.
- Full PlayMode: 113/113 passed (46.50s). Final Editor check: playing=False, failed=False; Title scene restored and scroll behavior restored to UniformAcrossAllPlatforms.
- User confirmed physical wheel zoom works on 2026-11-13 at 14:05 KST; grid still breaks (separate BUG-build-grid-lines). UI-hover behavior and standalone builds have not been manually verified. Implementation and physical wheel verification complete; proceeding to commit and develop integration under the updated AI Git policy. UI-hover manual check and standalone build are not claimed as verified.

## Manual verification / reset

1. Open `Assets/_Project/Scenes/Title.unity`, Play → New Game (or open `Assets/_Project/Scenes/Game.unity` directly).
2. Approach construction station, F; put cursor over world, outside the left HUD. One forward wheel tick should visibly zoom in (~15% reduction of orthographic size); reverse tick restores that size away from limits.
3. Scroll repeatedly in both directions: stop cleanly at sizes 20 and 180. Middle-drag pan and press Home: restore initial zoom 55 and focus.
4. Hover HUD buttons and scroll: camera must not zoom. Move back to world and confirm zoom resumes. Esc returns to first-person.
5. Stop Play Mode and reopen Title to reset. No asset/settings changes are needed for this verification.

CLI: `unity command editor set_auto_tick --caller plugin --skill unity-cli`, then `unity command test run --caller plugin --skill unity-cli --mode playmode --filter ConstructionWheelInput --async-tests --result-only`; poll `unity command test status --caller plugin --skill unity-cli --result-only` until completed and verify isPlaying=false before other tests.
