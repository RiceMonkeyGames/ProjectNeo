# SIEGE-34 | 2026-11-24 | Siege - Enemy Siege Role Coordination, Focused Wall Breach & Archer Blocked-Target Switch

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-24: SIEGE-34 - Enemy Siege Role Coordination, Focused Wall Breach & Archer Blocked-Target Switch

- **Assignee**: `SeongminHwang` | **Branch**: `feature/siege-34` | **Claim commit**: `300eb97` (`docs: claim SIEGE-34`, synced to `develop`) | **Depends on**: SIEGE-17
- **Scope**: Enemy siege roles (archers, ladder grunts, wall breakers) coordinate with each other and with the castle layout. Rules: [WallsAndSiege](../../design/WallsAndSiege.md) bullet "Siege role coordination (SIEGE-34)". Code map: [WallsAndSiege codemap](../../codemap/WallsAndSiege.md).
- **Decisions** (user, 2026-11-24, all recommended options):
  - Archer cover: defenders within 4m of the upper exit of a raised or raising ladder, or at a machicolation stand, get a squared-distance factor of 0.5 (about 0.7x as far). A much closer defender still comes first.
  - Blocked-target switch: after 2 failed side-step searches in a row against the same fully hidden nearest defender, the archer avoids that defender for 15s.
  - Focused breach: score is distance minus up to 15m for missing health, minus 8m per other breaker already attacking that piece (cap 3). It is the default of the shared `WallBreaker.asset`, so all scenes use it.
  - Ladder choice: 8m bonus for an already raised ladder, 6m penalty per defender within the block radius (3m) of its upper exit.
  - Agent follow-up found in testing: far (AI LOD 2) archers skip side-step searches, so each skipped search now counts as a failed one (also recorded in the design bullet). Otherwise they stayed pinned forever.
- **Implementation**:
  - `Runtime/Siege/SiegeAttackerBrain.cs` (`DecideArcher`):
    - Candidates come from `CombatSpatialIndex.QueryEnemies` within 2x the maximum attack range (`ArcherSearchRangeMultiplier`). `CharacterActor.ActiveActors` is used only when nothing is found (still approaching).
    - In-range candidates are ranked by `ArcherCoverRules.Score`. `IsCoverTarget` means a raised or raising ladder `TopPoint` within `archerCoverRadius` and height tolerance, or a same-team `ShotOpening.IsAtStand`.
    - When the first clear-shot candidate is a cover target and the current `UnitCombatAI.Target` is not, it calls the new `UnitCombatAI.TrySwitchHoldTarget` (Hold only, strikeable from where it stands). The shot target is chosen by `UnitCombatAI`, not the brain.
    - The approach target is the nearest defender not avoided by `ArcherBlockedTargetTracker`. `IsAvoidingBlockedTarget` is exposed for tests.
    - `ChooseLadder` uses the new `LadderChoiceRules.Score` overload with `LadderPhase.Up` and `SiegeLadder.DefendersAtTop` (allied actors at the upper exit within `LadderBlockRadius`, from the spatial grid, cached per frame).
  - Pure rules:
    - `Runtime/Siege/ArcherBlockedTargetTracker.cs` (`ArcherBlockedTargetTracker<T>`): consecutive failures per target, `limit`, `avoidSeconds`; remembers up to 4 avoided targets. `Reset` keeps avoided entries; `Clear` drops all.
    - `Runtime/Siege/ArcherCoverRules.cs`: `Score`, `IsActiveLadder`, `IsNearLadderTop`.
    - `LadderChoiceRules.Score` overload: raised bonus and defender penalty. It equals the old score when the ladder is not raised and has no defenders.
  - Wall breakers:
    - `WallTargetCandidate` gains `HealthRatio` (clamped 0..1, default 1) and `EngagedBreakers` (default 0). The old constructor is kept.
    - `WallBreakerAttack.Decide` fills both fields (`EngagedOn` counts other active breakers in reach of that piece) and exposes `Settings`.
    - `Runtime/Siege/FocusedBreachWallTargetStrategy.cs`: new ScriptableObject (`damageBonus` 15, `engagedBonus` 8, `engagedCap` 3) with `Score`, `Select` (lower `StructureId` tie break), and `Create`.
    - `Editor/WallBreakerSetup.cs`: menu `ProjectNeo/Setup/Apply Focused Breach Wall Strategy (SIEGE-34)` (`ApplyFocusedBreach`) creates `Data/SiegePrototype/FocusedBreach.asset` and sets it as the `WallBreaker.asset` `targetStrategy`. It is idempotent, and new setups also use it. `NearestWall.asset` stays for other uses.
  - `Runtime/Siege/SiegeLadder.cs`: `DefendersAtTop` (allied actors at the upper exit within `LadderBlockRadius`).
  - `Runtime/Combat/UnitCombatAI.cs`: `TrySwitchHoldTarget` (Hold only).
  - `Runtime/Siege/SiegeSettings.cs` new fields: `archerCoverPriorityFactor` 0.5, `archerCoverRadius` 4, `archerBlockedSearchLimit` 2, `archerBlockedAvoidSeconds` 15, `ladderRaisedBonus` 8, `ladderDefenderPenalty` 6. Existing assets use these defaults.
- **Automated Verification**:
  - Code rules check passed (0 violations). Two serialized asset changes were made through the Editor API: `Data/SiegePrototype/FocusedBreach.asset` (new) and `Data/SiegePrototype/WallBreaker.asset`.
  - EditMode `SiegeCoordinationRulesTests` `11/11` PASSED.
  - Full EditMode `898/898` PASSED.
  - PlayMode new tests all PASSED:
    - `WallBreakerPlayTests.FocusedBreach_IsTheSharedDefault_AndPicksDamagedWallOverNearest`
    - `WallBreakerPlayTests.FocusedBreach_SecondBreakerJoinsTheWallAlreadyUnderAttack`
    - `SiegePrototypePlayTests.EnemyArcher_CoversLadder_SwitchesToDefenderAtRaisedLadderTop`
    - `SiegePrototypePlayTests.EnemyArcher_FullyHiddenNearestDefender_SwitchesToAnotherDefender`
    - `SiegePrototypePlayTests.LadderChoice_EnemyPrefersRaisedLadderWithFewerDefendersAtTheTop`
  - Full PlayMode `365/369`. The 4 failures are pre-existing and fail identically on `develop` without the SIEGE-31/34 changes (baseline taken during SIEGE-31):
    - `GameOrganizationPlayTests.편성_키를_다시_누르면_창이_닫힌다`
    - `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits`
    - `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses`
    - `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores`
- **Manual Verification** (not run by the agent):
  - Open `Assets/_Project/Scenes/SiegePrototype.unity`, Play, place squads on the wall top during preparation, then `Start Battle`.
  - Expected: once a ladder is raised, enemy archers shoot defenders near its top first (a clearly closer defender still goes first).
  - Expected: ladder grunts prefer an already raised ladder with fewer defenders at its top.
  - Expected: wall breakers gather on a damaged wall or one a partner is already hitting.
  - Expected: an archer whose nearest defender is fully hidden moves on to another defender instead of standing still.
  - Open `Assets/_Project/Scenes/Game.unity`, run a battle: breakers still reach and hit walls when none are damaged, and do not all stall on one piece.
  - Reset: stop Play Mode; do not save the scene.
