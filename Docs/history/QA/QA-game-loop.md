# QA-game-loop | 2026-11-13 | Game Flow - First User Playtest

Last Updated: 2026-11-14 20:58:08 (KST)

## Evidence and scope

User manually exercised Game construction and battle and supplied a construction screenshot. Exact run settings, input values and defeat details were not captured. This is user-reported playtest evidence plus source inspection, not an agent reproduction, automated test pass or standalone build verification. Victory → reward → next preparation was later verified by QA-victory-flow; the remaining construction/combat recheck closed on 2026-11-14 (see below).

## Findings

| Finding | Follow-up | Evidence / next check |
|---|---|---|
| Wheel zoom changes view too little | BUG-build-camera-zoom | ConstructionCameraController divides wheel y by 120; input-unit mismatch is a hypothesis. Verify actual wheel values before choosing fix. |
| Grid repeatedly loses rows | BUG-build-grid-lines | Screenshot and user report. BuildGridView draws 0.09m world-space strips at local y=0.02; aliasing/depth overlap are hypotheses, not confirmed causes. |
| Stairs look unnaturally shaded; prefer flat wall-like appearance | ART-01 | Resolved 2026-11-13: shared ramp vertices smoothed normals; mesh rebuilt with flat per-face normals. User verified. [History](../ART/ART-01.md) |
| Prefer stairs in one-cell units | Q-42 | Setup currently specifies 1×2 footprint. Full-tier rise vs composable rise not yet decided. |
| Soldiers cast no visible shadows | ART-02 | Resolved 2026-11-13: scene Sun lights had shadows disabled; all scenes now use Soft shadows. User verified. [History](../ART/ART-02.md) |
| Starting battle below wall is disadvantageous | Q-40 | User requests another approach; no new trigger/deployment rule finalized. |
| Could not win because enemies felt strong | QA-opening-battle-balance | Do not infer a numeric balance fix from this alone. Victory-flow verification still outstanding. |
| Victory → reward → next prep unverified (could not win) | QA-victory-flow | Resolved 2026-11-13: user manually won with the 12-enemy debug roster; reward claim entered preparation once and next-stage checks were normal; defeat → `Return to Title` also confirmed. [History](QA-victory-flow.md) |
| Allies/player cannot use ladders | Q-41 | SiegeLadder climbing/queue API takes SiegeAttackerBrain. SiegeAttackerBrain disables agent/AI and interpolates base → edge → wall top before resuming navigation. Defenders have push/topple handling but no climb control. |

## Manual recheck after fixes

Open `Assets/_Project/Scenes/Title.unity`, Play → New Game. Approach construction station → F; scroll both ways, pan with middle mouse, Q/E rotate, Home reset. Confirm useful zoom and continuous grid at near/far views; inspect stair shading. Esc returns to first-person. Approach gate banner → F using current controls; note player location and combat difficulty. Check F1–F3 selection and 1/2/3 orders. After a victory verify `Claim Reward & Next Stage` enters preparation once; on defeat verify `Return to Title`. Record unexercised branches explicitly. Exit Play Mode and reopen Title to reset.

## Recheck 2026-11-14 (Done)

- Setup: on `feature/qa-game-loop` (develop `6118ba2` + claim commit), the agent confirmed the open Editor was `ready` with no compile failure and opened `Assets/_Project/Scenes/Title.unity` through the Editor API so the user only had to press Play → `New Game`.
- Checklist given to the user: construction (station F entry, wheel zoom, middle-drag pan, Q/E rotate, Home reset, continuous grid near/far, one-cell stairs and flat shading, soldier shadows, Esc return); battle (hold B 1s start at current position, left-click melee, Ctrl crouch / Shift sprint, F1–F3 selection and 1/2/3 orders, ladder tap F boarding / hold F toppling / W-S climbing, cannon F manning if placed, squad morale HUD, difficulty with the 12-enemy debug roster); outcome (`Claim Reward & Next Stage` enters preparation once, `Return to Title` on defeat).
- Result: user reported everything normal ("전부 정상") and did not flag any item as skipped or failing. No per-item notes, run settings or screenshots were captured.
- Evidence scope: user-reported manual Editor play only. No code, scene or asset changes; no automated tests were re-run for this task (latest baseline is COMBAT-03: EditMode 394/394, PlayMode 193/193). Standalone build unverified.
- Manual repeat: open `Assets/_Project/Scenes/Title.unity`, Play → `New Game`, walk the checklist above; stop Play Mode to reset.
