# SIEGE-32 | 2026-11-24 | SIEGE - Flyer Target Lock Cooldown, Ranged LoS Revalidation & Shelter Edge Cases

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-24: SIEGE-32 - Flyer Target Lock Cooldown, Ranged LoS Revalidation & Shelter Edge Cases

- **Assignee**: `SeongminHwang` | **Branch**: `feature/siege-32` | **Claim commit**: `39709a2` | **Code commit**: `da2c5f7`
- **Decision** (user, 2026-11-24): a ranged flyer striking a siege weapon fires a visible projectile; the damage is guaranteed and lands when the projectile stops (no miss from aim spread). Rules: [WallsAndSiege](../../design/WallsAndSiege.md).
- **Agent defaults pending review**: `TargetLockSeconds` 1.5s, `RetargetDistanceMargin` 2m, `SightlineRecheckInterval` 0.25s. A higher-rank target (wall-top ally over weapon or courtyard ally, priority target) breaks the lock at once, so the SIEGE-16 "roofed ally becomes a target again on the next decision" rule still holds.
- **Implementation**:
  - `Runtime/Siege/FlyerTargetLockRules.cs` (new): pure lock and hysteresis wrapper around the target strategy and `FlyerPriorityTargeting`.
  - `Runtime/Siege/FlyerBrain.cs`: `Decide` passes the current target's candidate index and remaining lock time to `FlyerTargetLockRules`; a new target starts the lock. `RevalidateSightline` re-checks the shot every interval, from the current position once at the hover point or from the hover point while en route, and drops an occluded hover point so the existing hover search picks a visible one. `CanReachAnyEnemy` counts live siege weapons. `StrikeWeapon` uses `CharacterActor.TryAttackPoint` aimed at the weapon centre; damage is computed at attack start and applied in the callback only if the weapon is still live.
  - `Runtime/Siege/FlyerSettings.cs`: `TargetLockSeconds`, `RetargetDistanceMargin`, `SightlineRecheckInterval` (serialized defaults, so existing assets are unchanged).
  - `Runtime/Combat/CharacterActor.cs`: `TryAttackPoint(aimPoint, stopRadius, impact)`; same cooldown, impact delay and interrupt rules as unit attacks. Melee calls `impact` at the impact delay; projectile fires a no-spread point shot.
  - `Runtime/Combat/CombatProjectile.cs`: `LaunchAtPoint(...)` no-damage point shot that calls back once when it stops.
  - Behaviour change: melee flyers now damage a cannon at the attack's impact delay instead of at attack start, and an untargeted swing no longer also hits a nearby ally.
- **Automated Verification**:
  - Unity compile passed.
  - EditMode `907/907` PASSED, including the new `FlyerTargetLockTests` (9).
  - PlayMode `Flyer*` `16/16` PASSED; `FlyerSiegePlayTests` `7/7`, including the new `AllAlliesSheltered_LiveCannonKeepsFlyerReachable_UntilDestroyed`, `RangedFlyer_StrikesCannon_WithVisibleProjectile_DamageOnArrival`, `RangedFlyer_OccludedHoverPoint_IsRecomputedWithClearShot`.
  - Full PlayMode `368/372`. The 4 failures (`GameOrganizationPlayTests.편성_키를_다시_누르면_창이_닫힌다`, `GamePerkPlayTests.StatPerks_ApplyToTheirTargets_IncludingLaterRecruits`, `GameUnlockPlayTests.NewRun_GetsOpenedStartingBonuses`, `PlayerHealthHudPlayTests.Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores`) fail the same way with the SIEGE-32 changes stashed, so they are pre-existing and unrelated.
  - The full PlayMode run exceeds the wait in `unity-verify.ps1`, so results were read from `unity command test status`.
- **Manual Verification**: not yet performed.
  - Open `Assets/_Project/Scenes/EnemyCompositionVerification.unity`, then Play and start the battle.
  - With two wall-top defenders at similar distances, a flyer should commit to one and not swap back and forth mid-air.
  - With a bastion cannon and no wall-top allies, the high projectile flyer should fire visible arrows at the cannon, and the cannon HP should drop as each arrow lands.
  - When a merlon or pillar hides the target from a ranged flyer's hover point, it should move to a point with a clear view within about a second.
  - Reset: stop Play Mode; do not save the scene.
- **Known limits**: cannon arrows aim at the weapon centre with no spread and may stop on the mount structure first; damage still lands.
