---
id: QA-ui07-test-drift
title: Update Game PlayMode Tests for UI-07 Rule Changes
status: In Progress
assignee: SeongminHwang
branch: feature/qa-ui07-test-drift
area: QA
depends_on: []
created: 2026-11-24
---

# QA-ui07-test-drift — Update Game PlayMode Tests for UI-07 Rule Changes

## Scope & Completion Criteria

Four Game PlayMode tests have failed on `develop` since 2026-11-23 (first noted in [SIEGE-18](../history/SIEGE/SIEGE-18.md)). Three of them assert rules that UI-07 (`762fb6d`, `86d76c3`) changed on purpose, so only the tests change, not game code:

- `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits`: the player is now always top grade with the grade stat bonus (+30% attack), and percent modifiers add together (`StatSheet`), so a +50% perk is not a ×1.5 of the current value. Compute the expected value with `StatSheet.GetValueWith`.
- `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses`: recruits now become hidden stored soldiers, so counting only active `UnitCombatAI` objects gives 0. Count inactive stored soldiers too.
- `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores`: the HP HUD now stays visible while a panel is open (user decision 2026-11-23). Assert it stays shown.
- `GameOrganizationPlayTests.편성_키를_다시_누르면_창이_닫힌다`: the close-with-O logic looks correct; the failure is likely virtual keyboard input dropped while the Editor is unfocused or minimized. Record the finding; no change unless it is confirmed as a real defect.

Done when the three tests pass and the full suite shows no new failures.
