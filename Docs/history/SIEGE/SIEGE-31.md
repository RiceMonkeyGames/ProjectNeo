# SIEGE-31 | 2026-11-24 | Siege - Autonomous Ally Wall-Top Ladder Push & Upper-Exit Chokepoint Defense

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-24: SIEGE-31 - Autonomous Ally Wall-Top Ladder Push & Upper-Exit Chokepoint Defense

- **Assignee**: `SeongminHwang` | **Branch**: `feature/siege-31` | **Claim commit**: `5612ba0` (`docs: claim SIEGE-31`, synced to `develop`)
- **Scope**: Q-60 item originally tracked as `SIEGE-14`, renumbered after `SIEGE-14` was reused for the 5m grid spike. Rules: [WallsAndSiege](../../design/WallsAndSiege.md) bullet "Autonomous wall-top ladder defense". Code map: [WallsAndSiege codemap](../../codemap/WallsAndSiege.md).
- **Decisions** (user, 2026-11-24, all recommended options):
  - Detection radius 12m from the Hold post at the same wall level.
  - Nearest up to 3 pushers per ladder, one ladder per defender.
  - During duty, only enemies on the wall top within the block radius (3m) of the upper exit are targets.
  - Chokepoint: up to 5 spots on a 2.5m inner half circle, used when a hostile climber is at least half way up, or a hostile on the top blocks pushing.
  - Duties end on topple or a new order.
  - QA follow-up: the relaxed push-count rule applies only to `Hold` units. `Engage`/`Move` and ranged units keep the old rule (any target = not a pusher).
- **Implementation**:
  - Combat (generic, no siege dependency):
    - `Runtime/Combat/HoldDuty.cs`: point, face point, focus, target radius, height tolerance; `Covers`, `SamePlace`.
    - `Runtime/Combat/HoldDutyRules.cs`: `IsNear`, `HasArrived`.
    - `Runtime/Combat/UnitCombatAI.cs`: `TrySetHoldDuty` (Hold only; samples the point on the NavMesh within 0.75m, needs a complete path that stays above `HoldMaxDescent`; same place = no new path), `ClearHoldDuty`, `HasHoldDuty`, `CurrentHoldDuty`. `UpdateHold` branches to `UpdateHoldDuty` (fights only duty targets via `FindDutyTarget`, otherwise walks to the duty point and faces the ladder edge). `OnHitReceived` ignores attackers outside the duty zone. `ResetTransientState` (new order, death, disable) clears the duty. `HoldPost` is unchanged, so units walk back afterwards.
  - Siege:
    - `Runtime/Siege/LadderDefenseRules.cs` (pure): `PushSpot` along the wall, alternating sides at `ladderPushSpotSpacing`; `ChokepointSlot` on the inner half circle (-90..+90 deg); `NeedsChokepoint`; `ClimbFraction`; `CountsAsAiPusher(attacking, hasTarget, ranged, holding, targetAtExit)`; `Side`.
    - `Runtime/Siege/LadderDefensePlanner.cs` (pure): pairs post-in-radius defenders with sites. Greedy by current distance minus `ladderDefenseStickiness` for the current ladder; push spots first, then chokepoint. Keeps a held spot index, otherwise takes the nearest free spot. Reusable buffers.
    - Data types: `LadderDuty`, `LadderDutyRole` (None=0, Push=1, Chokepoint=2), `LadderDefenseSite`, `LadderDefender` (64-bit excluded-site mask).
    - `Runtime/Siege/LadderDefenseDirector.cs` (static, runtime): ticked by enabled `SiegeLadder`s once per frame and replans every `ladderDefenseInterval` (0.5s). Gathers defendable ladders (`SiegeLadder.IsDefendable`: Up, not settling, wall present) and eligible allies from `CombatSpatialIndex` (alive, `Hold`, melee, not on or traversing a ladder). Hands out `HoldDuty`s, records path rejections per unit and ladder for `ladderDefenseRejectSeconds` (3s), and drops duties of units no longer gathered. `Release(ladder)` on topple, disable and `ResetBattle`. Statics reset on `SubsystemRegistration`. `TryGetDuty` is for tests.
    - `Runtime/Siege/SiegeLadder.cs`: `NeedsChokepoint` (hostile occupant climb fraction by height from `BasePoint` to `EdgePoint`, or last frame's hostile-at-top block), `FillDefenseSite` (NavMesh-sampled push spots within `LadderPushRadius`, plus chokepoint slots), `FindPushers` uses `CountsAsAiPusher` (a Hold melee ally with a distant or below-wall target still pushes; a target within the block radius of the exit, an attacking or ranged unit with a target, or a non-Hold unit with a target does not).
  - `Runtime/Siege/SiegeSettings.cs` (header "SIEGE-31"): `ladderDefenseRadius` 12, `ladderDefenseMaxPushers` 3, `ladderPushSpotSpacing` 0.8, `ladderChokepointClimbFraction` 0.5, `ladderChokepointRadius` 2.5, `ladderChokepointSlots` 5, `ladderDefenseInterval` 0.5, `ladderDefenseRejectSeconds` 3, `ladderDefenseStickiness` 1.5.
  - Existing assets use the defaults. No asset or scene was changed.
- **Automated Verification**:
  - EditMode `LadderDefenseRulesTests` `14/14` PASSED.
  - PlayMode `SiegePrototypePlayTests` `34/34` PASSED, including the new `LadderDefense_HoldMeleeWalkToNearbyLadder_PushItDown_AndReturnToPosts`, `LadderDefense_HitFromEnemyBelowWall_DoesNotPullPusherOffTheLadder`, and `LadderDefense_EnemyAtTop_SurplusDefendersRingTheUpperExit`.
  - PlayMode `AiAllocAndSplitPlayTests` `7/7` PASSED.
  - Code rules check passed (0 violations).
  - Full EditMode `887/887` PASSED.
  - Full PlayMode before the Hold-only push fix: `345/364`. The 19 failures are pre-existing:
    - 15 are listed in `QA-castle-kit-tests` (StackedConstruction 8, GameBattlement 5, GameBattle 1, GameFlow 1).
    - 4 fail identically with the SIEGE-31 changes stashed: `GameOrganizationPlayTests.편성_키를_다시_누르면_창이_닫힌다`, `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits`, `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses`, `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores`.
  - Final full run before commit (with the Hold-only push fix): EditMode 887/887, PlayMode 345/364; the 19 failures are the same pre-existing tests listed above, no new failures.
- **Manual Verification** (not run by the agent):
  - Open `Assets/_Project/Scenes/SiegePrototype.unity`, Play, place a melee squad on the wall top one cell beside a ladder with Hold (`3`), then `Start Battle`.
  - Expected: up to 3 nearest melee defenders walk to the raised ladder and push it down together.
  - Expected: posts farther than 12m, ranged squads, and Attack/Move squads do not go.
  - Expected: archer hits from below do not stop the push.
  - Expected: when a climber is half way up or reaches the top, spare melee defenders stand on a half circle inside the upper exit.
  - Expected: after the topple, units return to their posts.
  - Expected: hold-`F` player push is unchanged.
  - Reset: stop Play Mode; do not save the scene.
