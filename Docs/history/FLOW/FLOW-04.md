# FLOW-04 | 2026-11-22 | Flow/Run - Save and Return to Title During Preparation

Branch: `feature/flow-04`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim FLOW-04`, `d26d8ac`). Decision source: Q-13 ([Decisions-Resolved](../../plan/Decisions-Resolved.md)). Rules: [PrepEconomyMeta](../../design/PrepEconomyMeta.md).

## Background

- Registered from Q-13: during preparation the player can save and go back to Title, saving at the same moment quitting during preparation already saves (FLOW-02), and `Continue` resumes the same preparation. Not offered in battle or on the reward screen.
- User decisions at task start (2026-11-22): (1) FLOW-04 builds a minimal preparation-only `Esc` pause menu; UI-11 (`신용준`'s UI work) adds Options and battle use later; (2) one click, no confirmation; (3) `Esc` opens the menu and frees the cursor in the same press only when no other window or view is open, while other windows keep closing on `Esc` first.

## Changes

- `Runtime/Flow/GamePauseMenu` (new): runtime-built uGUI overlay (canvas sort order 60) with `일시정지`, `계속하기` and `저장 후 타이틀로`. `Open` goes through `GamePhaseView.EnterPanel`, so movement/look freeze and `PreparationInteraction` closes it on `Esc` like other panels. Runs at execution order 110 and opens only when the previous frame was also exploration, so the `Esc` that closes another window does not reopen the menu. `SaveAndReturnToTitle` calls `GameBattleController.SaveRun(RunSaveTrigger.QuitDuringPreparation)`, saves meta, then `GameFlow.ReturnToTitle`; without a slot (Game scene played directly) or with a refused save it stays open and shows `NoSlotText` / `SaveFailedText` (plus a developer warning).
- `GameBattleController`: adds the menu at runtime (`PauseMenu`), no `Game.unity` edit.
- `UI/KeyListHud`: adds `Esc - 메뉴 (저장 후 타이틀로)` to the H key list.
- Docs: rule in `PrepEconomyMeta` (Run Save & Continue), decision in `Decisions-Resolved`, codemap row in `FlowRunEconomy`.
- Implementation detail beyond the approved spec: closing the menu returns to exploration with the cursor captured, the same as other preparation windows.

## Verification

- Code rules check: passed (0 violations, no serialized asset changes).
- EditMode: 688/688 passed. PlayMode: 304/304 passed (169.9s), including new `GamePauseMenuPlayTests` (save, return to Title and Continue restore stage and gold; refused save keeps the menu open in the Game scene; no-slot refusal; virtual-keyboard `Esc` opens/closes, does not reopen on the `Esc` that closes the perk window, lists in the key list and does not open in battle).
- Not verified: manual play with a physical keyboard and mouse, menu look, standalone build.
