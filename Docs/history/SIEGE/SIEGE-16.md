# SIEGE-16 | 2026-11-23 | SIEGE - Flyer Cannon Targeting & Hover Slot Separation

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-23: SIEGE-16 - Flyer Cannon Targeting & Hover Slot Separation

- **Assignee**: `SeongminHwang` | **Branch**: `feature/siege-16` | **Claim commit**: `89d8a58`
- **Decision** (user, 2026-11-23): flyer target order is nearest ally atop walls, then nearest mounted siege weapon (bastion tops and tower roof mounts alike), then nearest remaining ally. The literal "only when no ally is reachable" rule used by walking attackers would almost never fire for flyers, since every living ally is reachable from the air. Rules: [WallsAndSiege](../../design/WallsAndSiege.md), [Decisions-Resolved](../../plan/Decisions-Resolved.md).
- **Implementation**:
  - `Runtime/Siege/FlyerBrain.cs`: `WeaponTarget` and `HasHoverPoint`; a static active-flyer list shares occupied hover points per target; re-selects immediately when a weapon is destroyed; flyers strike a cannon like wall-top climbers (weapon size as reach, no defense, `BossUnit.StructureDamage`).
  - `Runtime/Siege/FlyerHoverQuery.cs` (new): pure hover-point query. `BodyFits` capsule overlap check against structures (merlons, walls, corbels) and `CombatPhysics.HasClearShotFrom` gate candidate points.
  - `Runtime/Siege/FlyerEngagement.cs`: `HoverPoint(in FlyerHoverQuery)`; the old 6-argument overload is kept.
  - `Runtime/Siege/HoverBesideEngagement.cs`: searches angle slots spaced by `HoverSeparation`, nearer points first, then outer rings within attack range. Fallbacks: crowded valid point, then the old beside-the-approach point.
  - `Runtime/Siege/FlyerSettings.cs`: `TargetsSiegeWeapons` (default on; a variant can opt out) and `HoverSeparation` (default 1.2m, never less than the body diameter).
  - `Runtime/Siege/FlyerTargetCandidate.cs`: `IsSiegeWeapon` with the `SiegeWeapon(...)` factory.
  - `Runtime/Siege/WallTopFirstFlyerTargetStrategy.cs`: ranks wall-top ally, then weapon, then other ally.
  - `Editor/EnemyCompositionVerificationSetup.cs`: passes the two new settings to the high projectile flyer variant.
  - Existing assets are unchanged; the new fields use their defaults.
- **5m kit notes**:
  - Tower roof mounts are covered through `ConstructionWorld.Weapons`.
  - Hover heights and clearances come from colliders and data, not hard-coded 3m values.
  - Tower roof occupants count as wall-top allies through the 1m rule, so they stay the first priority.
  - Full 5m layout verification waits for SIEGE-18.
- **Automated Verification**:
  - Unity compile passed.
  - EditMode `807/807` PASSED.
  - PlayMode `Flyer*` `10/10` PASSED, including `FlyerSiegePlayTests`: `NoWallTopAlly_FlyerStrikesCannon_BeforeCourtyardPlayer`, `WallTopAlly_IsPreferredOverCannon`, `TwoFlyers_OnSameTarget_HoverApart`.
  - PlayMode `EnemyComposition*` `4/4` PASSED; `CannonAttack*` `2/2` PASSED.
  - Full PlayMode `332/333`. The one failure, `GameMoralePlayTests.ContagionTicksInScene_OnlyNearbyAlliesLoseMorale_AndHudShowsCheck` (expected 145, was 150), also fails on the develop baseline without SIEGE-16 changes, so it is pre-existing and unrelated.
  - The full PlayMode run (~184s) exceeds the 180s wait in `unity-verify.ps1`, so results were read from `unity command test status`.
- **Manual Verification**: not yet performed.
  - Open `Assets/_Project/Scenes/EnemyCompositionVerification.unity` (or the Game battle with a bastion cannon), then Play.
  - With no allies on wall tops, flyers should fly to and damage the cannon.
  - With several flyers on one wall-top ally, they should hover spread around it, not stacked.
  - A high projectile flyer should not hover inside merlons or walls.
  - Reset: stop Play Mode; do not save the scene.
- **Known limits**: ranged flyer cannon hits have no projectile visual for now.
