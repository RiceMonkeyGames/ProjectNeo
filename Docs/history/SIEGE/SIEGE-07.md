# SIEGE-07 | 2026-11-15 | Siege - Cannon Usability: Fire-Ready Feedback & Aim Rotation

Last Updated: 2026-11-15 16:12:06 (KST)

Branch: `feature/siege-07`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim SIEGE-07`). Design record: [WallsAndSiege](../../design/WallsAndSiege.md) (Cannon mechanics, Usability). Builds on [SIEGE-02](SIEGE-02.md).

## Problem (user feedback, 2026-11-14)

- The player could not tell whether a manned cannon could fire or was reloading. The reload text existed but sat at the end of a long line in the battle status panel, away from the crosshair.
- The player wanted to turn the cannon while manning it. The view was clamped to ±45° and the cannon model never moved, so aiming gave no visible response.

## Decision (delegated to the agent by the user, 2026-11-15; open to revision after playtest)

| Item | Choice | Reason |
|---|---|---|
| Fire-ready feedback | Crosshair color (green can fire / amber reloading / red blocked), reload bar and one-line state under the crosshair, one pulse when reload completes | Feedback belongs where the player is looking; no new art or audio needed |
| Rotation method | The barrel follows the aim; no rotate keys | Mouse aim already exists; extra keys would compete with combat/command keys (Q-11 undecided) |
| Limits / speed | Manned traverse ±90° from the placement facing; 180°/s barrel swivel that is cosmetic only | Covers the whole outward half-plane without firing back into the castle; responsiveness is not reduced |
| Auto-fire | Arc ±45°, range 6–60m and targeting unchanged; the barrel turns to each shot | Keeps SIEGE-02 balance and tests; manning stays the stronger option |
| Sound / on-cannon effects | Deferred to the audio/art passes | No audio pipeline yet |

## Implemented scope

- `CannonSettings`: `manualAimHalfAngle` 90 (never below `aimHalfAngle`), `swivelSpeed` 180; `Create` parameters added. Existing `.asset` files were not edited; the new fields use code defaults.
- `CannonAim`: `StepSwivel`; `CannonAimContext.Manual`.
- `CannonModule`: `Facing` is now the fixed placement facing; `BarrelFacing`, `Swivel`, `SetSwivel`, `SwivelToward`, `ManualAimContext`. The swivel rotates the weapon root about the world up axis (the root has non-uniform scale, so a child rotation would shear the placeholder). `FireAt` snaps the barrel to the shot, disarming resets it, and an external re-pose (support settling) becomes the new placement facing. Range/arc checks use the un-swivelled muzzle so results do not depend on barrel direction.
- `CannonOperator`: view clamp and `FireAt` use the manual arc; `ApplyLook` drives the barrel; `AimState` (`CannonAimState`: Ready 0, Reloading 1, NoTarget 2, OutOfRange 3, OutOfArc 4) refreshed on manning and every manned frame; crosshair raycast shared with firing. `CannonOperatorRules.EvaluateAim` holds the priority rule.
- `CannonOperatorSettings`: state colors and ready-pulse values.
- `UI/CannonReticleView` + `GameFlowHud`: runtime-built bar and state line next to the existing reticle; hidden and restored when not manning. No scene, prefab or asset edits.

## Verification

Compile passed. Full `unity-verify-local.ps1 -Mode all -SkipDotnet` passed EditMode **406/406** (3.33s) and PlayMode **202/202** (125.87s). Dotnet precheck skipped (stale generated `.csproj` does not list the two new files).

New cases: EditMode `CannonRulesTests` — `Aim_SwivelMovesTowardTargetAtSpeed` (5), `Aim_ManualContextWidensArcOnly`, `Settings_ManualArcNeverNarrowerThanAutoArc`, `Operator_AimStatePriority_ReloadThenPointThenRangeThenArc`, `OperatorSettings_StateColorsDiffer`. PlayMode `CannonPlayTests` — `Operator_ManualFire_ReachesWideArc_AndBarrelSwivelsWithoutMovingFacing` (auto-fire refuses a ~63° target, manual fire hits it; swivel speed, ±90° clamp, placement facing unchanged, reset on disarm), `Operator_AimState_TracksReloadAndUnmannedState`; `GameCannonPlayTests.Player_HasCannonOperator_AndHudShowsHintWhenManned` extended for the real Game HUD (bar full when loaded, color/state line, view and barrel to 60°, view stop at 90°, reloading display, hidden after leaving).

One defect found and fixed during verification: the aim state was first computed one frame after manning, so the crosshair showed the blocked color for a frame; `TryMan` now refreshes it immediately.

Not verified: how the crosshair display looks on screen (layout, readability, colors) and physical mouse/key play in `Game.unity`; standalone build.

## Manual check

1. Open `Assets/_Project/Scenes/Game.unity` and press Play.
2. Walk to the construction station, press F, place a `Cannon` on an outer wall top (R rotates it to face north/outward), Esc to return.
3. Hold B for 1 second to start the battle. Walk within 3m of the cannon, look at it, press F.
4. Expected while manned: the crosshair is green with `Ready` under a full bar when aiming at ground 6–60m away; red with `Out of range` when aiming at the wall base or beyond 60m; left click fires, the crosshair turns amber, the bar refills over 6 seconds with `Reloading N.Ns`, then pulses once and returns to green.
5. Turn the mouse left/right: the view and the cannon barrel turn together up to 90° each side of the placement facing and stop there. A shot at 60–80° to the side lands where aimed.
6. Press F or Esc to leave: the crosshair display disappears; the unmanned cannon keeps auto-firing only within ±45°.
7. Reset: stop Play mode. Nothing is saved to the scene or assets.

After the results were presented, the user authorized Done, archive, and commit/push of the task branch on 2026-11-15 without a manual playtest; the manual check above remains open and develop merge remains with the user.
