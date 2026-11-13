# BUG-build-grid-lines | 2026-11-13 | Construction - Subpixel Grid Lines

Last Updated: 2026-11-13 14:28:55 (KST)

## Claim and cause

- Owner: Codex; branch `fix/bug-build-grid-lines` from develop `f7400a1`. Claim committed/pushed before code changes. Final commit `4041358` was made/pushed directly on develop: final Git output revealed that develop was current, while the task branch still pointed at its base. This was not a task-branch merge; the pre-commit branch guard was missed. Temporary local branch removed after remote ancestry verification; no remote task branch published. Future commits must check current branch immediately before staging/committing. User confirmed zoom-in restores visible grid lines.
- Actual Game scene reproduction: 0.09m strips projected to approximately 0.372px at initial zoom 55 and camera viewport 1469×719. Captured dotted/missing lines before change. This supports subpixel rasterization as the cause; no geometry rows were actually omitted by grid generation.
- Fix: cache one grid mesh and vertex buffer, compute perpendicular screen-pixel width for both grid axes in URP beginCameraRendering, and widen only as needed to maintain BuildCameraSettings.MinimumGridLinePixels (default 1.5). Account for zoom, viewport resolution, transform scale and changing camera angle. Keep line center/spacing and world-width floor; non-construction game cameras restore baseline width. Unsubscribe on disable; destroy mesh on destruction.
- No serialized assets, materials, build layout, occupancy, colliders or NavMesh changes. New numerical view setting lives in the existing ScriptableObject; default applies to existing settings.

## Verification

- `GameFlowPlayTests.ConstructionGrid_RenderedLinesKeepPixelThicknessAcrossZoomAndAngles`: renders into 1280×720 and 1920×1080 targets at zoom 20/55/180 and yaw 0/22.5/45/90/135 (30 combinations). Checks projected perpendicular width of both line directions >=1.5px, line centers/spacing unchanged, mesh reused. 1/1 passed (0.85s).
- Full EditMode 216/216 passed (6.30s); full PlayMode 114/114 passed (47.88s). Final Editor check: playing=False, failed=False, background=False; Title scene restored.
- Agent captured actual Game view before/after; corrected captures show continuous grid lines. Captures are visual evidence at their displayed framing, not controlled golden-image comparisons at identical zoom. Evidence saved outside Assets/Temp/Logs under `C:/Users/JUNGLE/.codex/visualizations/2026/11/13/01a1d329-3d8d-7532-baa4-dd5715e8f993/` (`grid-before.png`, `grid-after.png`, `grid-after-far.png`). File labels alone do not establish the exact captured zoom, since screen capture reads the Game view's last rendered buffer.
- User confirmed the requested manual visual recheck on 2026-11-13 at 14:28 KST; mark Done. No detailed per-control report was supplied. Standalone build remains unverified.

## Manual verification / reset

Open `Assets/_Project/Scenes/Title.unity`, Play → New Game, approach construction station → F. Outside HUD, wheel to zoom out, middle-drag pan, Q/E rotate; check both grid directions remain continuous through rotation and at distant views. Home restores initial view. Confirm line centers match placement previews and a click places on the intended 3m cell. Esc returns to first-person; confirm the grid does not become permanently thick there. Stop Play Mode and reopen Title to reset.

CLI: `unity command editor set_auto_tick --caller plugin --skill unity-cli`, then `unity command test run --caller plugin --skill unity-cli --mode playmode --filter ConstructionGrid_RenderedLines --async-tests --result-only`; poll `unity command test status --caller plugin --skill unity-cli --result-only` and ensure isPlaying=false before other Editor operations.
