# Game Design: Combat, Units & Win/Loss

Last Updated: 2026-11-21 20:58:37 (KST)

> Purpose: Rules for perspective, squad commands, damage, unit lifecycles, and battle outcomes. | Parent: [design/README](README.md) | Related: [WallsAndSiege](WallsAndSiege.md), [MoraleAndWave](MoraleAndWave.md)

## Perspective & Squad Commanding

- Default perspective is **3D first-person** (confirmed 2026-11-11): direct player control combined with squad commanding.
- Prototype combat uses WASD movement, mouse look, and left-click frontal melee attack (jump, guard, directional attacks are follow-ups).
- Player forms squads and assigns heroes as captains. No separate commands are given directly to individual heroes. Squads without captains obey identical orders.
- **Controls** (subject to playtest tuning): F1–F4 select squads, backtick (`` ` ``) selects all, `1` Move, `2` Attack, `3` Hold.
- **Command Behaviors**:
  - **Move**: Disregards enemies and marches to screen-center target; transitions to Hold upon arrival.
  - **Hold** (revised 2026-11-12, Bannerlord style): Defends guard post (where Hold was issued or Move finished).
    - *Melee*: Advances to engage walkable enemies within 6m (`HoldEngageRadius`, data-configurable); does not pursue beyond range or broken paths; does not descend more than `HoldMaxDescent` below its post (see the Hold level rule below).
    - *Ranged*: Fires within range; advances toward reachable enemies within `range + 6m` (`HoldRangedChaseExtra`, data-configurable) until in firing range; shoots downward at enemies beneath walls; does not chase unreachable enemies.
    - *Return*: Returns to post when targets clear; minor displacement within 1m does not trigger return; halts nearby if post is blocked.
    - Unreachable enemies (no walkable path) are ignored so defenders push ladders instead of stalling.
  - **Attack**: Marches toward target point while engaging enemies spotted in visual range; resumes march after clearing; waits in Attack posture if arrived/empty.
  - **Vision**: Configurable detection radius; wall LoS occlusion and FoV angles omitted currently.
  - Unreachable destinations or broken paths halt at closest reachable point; clustered stalls near destination count as arrived.
  - Squads without orders at battle start begin in Hold state. Squad count/caps are data-driven. Final values (Q-57, team decision 2026-11-19): at most **10 squads** with **20 soldiers** each. The hero captain is not counted (a full squad is 20 soldiers + 1 captain; user, 2026-11-21). Squads 1-10 are selected with `F1`-`F10` (temporary until Q-11). Applied to the Game scene data by PREP-09.
  - Captain hero death drops squad morale by the largest fixed amount (2026-11-12).
- **Formations & drag placement (COMBAT-08, approved 2026-11-21, user; Bannerlord 2-inspired)**:
  - *Types*: Line and Column use the squad command asset's existing formation spacing (default 1.4m; the Game scene asset currently uses 1.1m), and Column is 2 files wide by default (`ColumnFiles`; drag width ignored). Close (0.9m), Loose (2.5m). Spacings are temporary data values. Each squad keeps its own formation and frontage width.
  - *Choosing*: `G` cycles the selected squads' formation (temporary key until Q-11/UI-08).
  - *Battle (first person)*: right mouse button pressed on the ground, dragged and released draws the front line: its length is the frontage width, so the number of ranks follows from the head count; the squad faces perpendicular to the line, away from the camera side. With several squads selected, the line is split equally, left to right in squad number order. A right click without drag (or `1` Move) moves with the current width and the camera facing. Each soldier's slot is shown as a marker while dragging.
  - *Prep squad placement screen (`P`, top-down)*: left-click drag does the same; a plain left click moves as before; right click still clears the selection. The remembered prep placement also keeps formation and width.
  - *Narrow ground*: ranks are clamped to what fits. The available depth behind the line is measured on the NavMesh (works anywhere); on wall tops the wall walkway width is also used as an upper limit. When ranks are cut, the extra soldiers extend the line sideways (along the wall); any slot that still has no ground snaps to the nearest walkable point. Example: 4 ranks requested on a 2-rank wall top → 2 ranks, twice as long.
  - Bannerlord itself only projects invalid slots to the nearest walkable point (units bunch up); the rank clamp is ProjectNeo's own rule. Crenel firing slots for ranged squads at Lv2: see COMBAT-10 below.
  - *Roster changes (approved 2026-11-21, user)*: the squad command center re-forms a squad by itself whenever its roster changes, so callers no longer need to place newcomers. Joins (recruit, organization move, captain change, return from rout) re-form at the end of that frame: a moving squad re-plans all slots; a holding squad keeps every soldier already on a slot (within 45% of the spacing) and gives newcomers the free slots. Losses (death, rout) refill only in holding squads, batched for `RefillDelay` (2s, data value): each emptied slot is taken by the nearest soldier whose slot disappeared, so one rear soldier steps up instead of the whole squad shuffling. Squads under Attack or without a destination are left alone.

## Player Health HUD

- UI-03 (user decision, 2026-11-17): display a health bar and current/maximum HP numbers in the bottom-right corner during preparation and battle gameplay. Reflect damage, potion healing and maximum-HP changes immediately. Use existing health rules; do not change combat balance.

## Basic Combat Rules

- **Prototype encounter tuning (2026-11-13, QA-opening-battle-balance)**: User directs lightweight tuning for debugging; current Game encounter is not a formal stage. Use 12 enemies (6 ladder, 2 gate breakers, 4 archers) against the existing 12 allies. This is a temporary debug roster; formal stage balance follows B-7.1. Keep unit stats and outcome rules unchanged.

- Damage formula: `Attack − Defense`, minimum damage is `1`.
- Auto-targeting acquires closest enemy, re-evaluating on target death or unreachability.
- **Retaliation & Target Switching (agreed 2026-11-14, BUG-ally-no-retaliation)**:
  - **Hit retaliation & proximity re-evaluation**: Taking damage (`CharacterActor.HitReceivedFrom` carrying the attacker) or spotting a closer reachable threat during `Engage` triggers retargeting when the current target is null or cannot be struck from the current position (e.g., chasing a fleeing target).
  - **In-range combat lock**: When already striking the current target in range (`CanStrikeFromHere`), ranged hits from other enemies do not break lock; only melee attacks (`AttackKind.Melee`) may trigger a switch.
  - **Flip-flop prevention**: A minimum target-lock cooldown (`TargetSwitchCooldown = 1.5s` in `UnitAISettings`) prevents rapid target switching.
  - **Hold retaliation leash**: In `Hold`, if hit by an attacker outside `HoldChaseRadius`, the unit pursues to retaliate up to a maximum `HoldPost` path/distance leash (`HoldRetaliationRadius = 15m` in `UnitAISettings`, data-configurable). Attackers beyond that leash (e.g., ground archers shooting wall-top defenders via long stair detours) do not pull defenders off post; ranged defenders still fire back if in range.
  - **Exceptions & extensibility**: `Move` orders, routing/retreating, ladder climbing, and active siege tasks (gate/wall/cannon breaking, raising ladders) suppress retaliation/target switching. Targeting policy is structured to support future priority/fixed-target unit types.
- **High ground and wall-top archers (BUG-wall-archer-los, 2026-11-15; choices delegated to the agent by the user, open to revision after playtest)**:
  - **High-ground range**: A grounded ranged attacker whose feet are at least `HighGroundMinimumDrop` (1.5m, same as the upward-shot rise) above the target's feet multiplies its range by `1 + HighGroundRangeBonus` (25%; `CombatProfile`, data-configurable, 0 disables). No bonus when shooting upward or level, for melee, or for flying attackers. No extra damage bonus: enemies shooting up at wall tops already deal 50%. Reason: enemy archers stop at the edge of their own range to the front-most defender, which left rear-rank wall-top archers 1–2m short and idle while their squad was being shot.
  - **Arrow flight**: Superseded by COMBAT-09 (below): arrows now fly a ballistic arc until they hit something, so they always reach the aimed point. (Old rule: an arrow flew at least as far as the aimed point of a target that passed the range check.)
  - **Line of sight**: Unchanged. The shooter's own footing piece never blocks its shots; other wall pieces still block both ways, so a wall edge that hides a defender also protects it. No walking to the wall edge.
  - **Hold with no shot and no path**: The unit stays on its post and re-checks every decision interval; it does not leave the wall to chase.
- **Hold stays on the post's level (BUG-wall-archer-leaves-wall, decided by the user 2026-11-20)**: In `Hold` (including the Hold that follows a finished `Move`), a unit does not chase or retaliate along a walking path that drops more than `HoldMaxDescent` (1.5m, `UnitAISettings`, data-configurable, 0 disables) below its post. This applies to **both melee and ranged** units, so a squad holding a wall top stays on the wall even when a breach and stairs give a path to enemies below; it still fights enemies that reach the wall top and shoots anything in range. This replaces the earlier melee note that Hold units "can descend stairs to engage breaches". `Engage` (Attack) orders keep the old behavior and may leave the wall to chase; `Move` still ignores enemies. The rule lives in the shared `Hold` logic, so enemies parked in `Hold` by the siege brain follow it too.
- No friendly fire.
- Ranged attacks fire projectiles upon release: dodgeable post-launch, blocked by walls, pass through allies.
- **Arcing arrows, range and spread (COMBAT-09, decided by the user 2026-11-21)**:
  - **Ballistic flight**: Arrows fly a gravity arc like cannon shells instead of a straight line. Launch speed `ProjectileSpeed` 18 and `ProjectileGravity` 9.81 (`CombatProfile`, data-configurable); the low-arc solution is used (about 14° at 16m). If the aim point is out of reach at that speed, the arrow is launched at 45° (the farthest possible shot). Arrows do not lead moving targets, so they stay dodgeable. An arrow that hits the ground, a wall piece or any other obstacle stops there.
  - **Range**: About 1.5× the old values — ranged soldiers (allied and enemy) 11 → 16m, hero archer 12 → 18m, Marksman boss 22 → 30m. The 25% high-ground bonus is unchanged (wall-top ranged soldier 20m).
  - **Spread**: Each shot aims at a random point inside a circle around the aim point, perpendicular to the shot line, with radius `distance × tan(AimSpread)`. `AimSpread` is a new unit stat (`StatType.AimSpread`, degrees), so items and skills can change it through the normal stat modifiers. Defaults: 3° for ranged soldiers and the hero archer (about 0.8m at 16m), 1° for the Marksman. Randomness goes through `IRandomSource`.
  - **Line of sight**: Unchanged. Target acquisition still uses the straight-line line-of-sight check; there is no lobbing over walls. A shot that passes that check can still be stopped by a wall or battlement it meets on its arc.
- **Arrow model and stuck arrows (ART-05, decided by the user 2026-11-21)**:
  - **Model**: Arrows use `PolygonFantasyKingdom` `SM_Prop_Arrow_01_Optimized` (URP material copy, no collider, no shadow). The model tip sits on the projectile point, so the collision check happens at the arrowhead.
  - **Sticking**: An arrow that hits a wall, battlement, ground or character stays stuck at the hit point with its head pushed `StuckArrowSettings.penetration` (0.12m) into the surface, instead of disappearing. Damage is unchanged. Stuck in a character it follows the nearest bone (humanoid bones, else skinned-mesh bones, else the hit collider), and it stays on the corpse after death.
  - **Removal**: A stuck arrow disappears after `StuckArrowSettings.lifetime` (30s), when the object it is stuck in is destroyed or deactivated, or when it is pushed out by the scene cap `StuckArrowSettings.maxCount` (100, oldest first). All arrows are removed when the battle ends (`GameBattleController` cleanup).
  - **First-person player**: Arrows that hit the first-person player (an actor with a camera under it) do not stick, so they never block the view.
- **Wall-top cover fire (COMBAT-10, decided by the user 2026-11-21)**:
  - **Who**: Only AI-controlled allied ranged units standing behind their own battlement (a battlement piece within `CombatProfile.coverSearchRadius`, 1.2m, of the unit). The player, enemies (even on a captured wall top) and ground archers never crouch this way.
  - **Crouch to reload**: Right after a shot the unit crouches behind the sill for most of the reload (attack cooldown). It stands up `CombatProfile.coverRiseLead` (0.5s, data value) before the next shot can fire, aims and releases standing. With no target it stays standing to watch. While crouched its hitbox and aim points shrink to `CombatProfile.crouchHeightRatio` (0.55, so 1.1m for a 2m unit): body centre 0.55m and upper body about 0.94m, both below the 1.1m sill, so a crouched defender is fully covered.
  - **Attackers**: Enemy archers keep the existing behaviour: they hold a drawn bow while the target is hidden and release as soon as the body centre or upper body is exposed (SIEGE-10 aiming, COMBAT-09 arc and spread).
  - **Archer attack speed** (user, 2026-11-21, after playtest: archers fired too often to show the crouch and were too strong): ranged soldiers (allied and enemy) and the hero archer go from 1.0 to 0.4 attacks per second (one shot every 2.5s). With the 0.3s release and 0.5s rise lead, a wall-top archer is crouched about 1.7s of each 2.5s cycle. Damage is unchanged; the Marksman boss is unchanged.
  - **Crenel firing slots**: At Lv2 (crenellated), a ranged squad (leader or most members ranged) planning slots on a wall top puts its front rank in the crenels nearest the front line (one archer per crenel, standing on the walkway behind the crenel). Archers that get no crenel form the rear ranks behind them. Lv1 parapets, gun ports and melee squads keep the normal COMBAT-08 slots.

## Units

| Category | Description |
|---|---|
| Player Character | Controllable hero. Possesses stats, skills, and items identical to heroes. |
| Hero | Hired at tavern. Can act as squad captain. **Permadeath** on death; cannot be revived. |
| Minion (Grunt) | Recruited at barracks. Eliminated on death; only fleeing/routed minions return next battle. |

- Surviving heroes and minions fully recover HP before the next battle (2026-11-12). Only wall HP damage carries over.
- Fallen ally corpses remain on the ground during the next preparation phase (2026-11-12), excluded from squads and run. At the next prep start fallen units leave their scene squads (bodies stay), so they no longer take squad slots (Q-50, 2026-11-13).
- **Fallen hero equipment** (Q-31, user 2026-11-19): Equipment is never lost on hero permadeath. When the battle result is applied, the fallen hero's equipped items return to the run stash with their upgrade levels and can be equipped again by the player or another hero (implementation: PREP-08).

## Active Skills

Team decisions 2026-11-19 (Q-62~Q-65); implementation COMBAT-06.

- **Owners & slots** (Q-62): the player and heroes have active skills; a squad uses its captain's. At most `3` slots. The player has `3` slots from the start; a hero has `1` slot and gains one each time its grade rises (`1 -> 2 -> 3`). Grade progression and hero skill sources: Q-73.
- **Activation** (Q-63): the player casts skills directly. Hero skills are cast by the hero's AI while **Auto** is on; the player can switch Auto off per hero and cast manually (manual input decided with Q-11).
- **Targeting** (Q-63): a data-driven target mode per skill, built to be extended (initial modes: self, aimed point/area, target unit, squad).
- **Cost** (Q-64): cooldown only, no mana, stamina or charges. Every skill is ready when a battle starts.
- **Effects** (Q-65): an extensible effect design supporting area damage, buff/debuff, heal, morale gain (COMBAT-07 gain rules) and stagger (COMBAT-04 poise) at a basic level; sample skills are proposed with COMBAT-06.
- **Acquisition** (Q-66, Q-72, Q-73, 2026-11-19): player skills are learned through skill entries in the Combat perk tree; they reset with the perk allocation when a run ends, and when the player knows more than 3, three are equipped during preparation. Each hero has 3 fixed skills in its data; each grade opens the next one.
- **COMBAT-06 inputs and scope** (user, 2026-11-20): player skill slots use `Z` / `X` / `C` (bindings in data, final layout Q-11). Manual hero cast (Auto off): select the squad (`F1`-`F4`) and press `Alt` + the slot key; its captain hero casts that slot toward the screen aim point. The player's 3 equipped skills and each hero's Auto toggle live in the preparation equipment window (`I`). M2 ships about 6 shared sample skills, one per effect family (area damage, attack buff, enemy slow debuff, heal, morale rally, stagger strike), reused by Combat-tree skill perks and hero fixed slots; the final skill roster waits for M4 content planning (Q-61). Equipment window behavior (COMBAT-06 step 6): a top row on the right list switches between the gear view and the skill view. The player clicks a learned skill to place it in the first empty slot and clicks a slot to empty it; empty slots refill from the remaining learned skills when the battle starts. A hero shows its fixed skills, with locked ones marked by the grade that opens them, plus an Auto on/off row. The player loadout and each hero's Auto setting are saved with the run.

## Win/Loss Conditions

- Confirmed 2026-11-12: If victory and defeat occur simultaneously, **defeat takes priority**.
- If no siege-capable enemies remain and no walkable path exists to surviving allies, remaining enemies retreat off-map and are excluded from victory kill tallies (Q-30).

| Result | Conditions |
|---|---|
| **Victory** | Every wave has spawned and all enemies are killed or retreated (Q-51) |
| **Defeat** | Main gate destroyed AND all allies killed (or routed), OR Player character dies |

Combat continues if main gate falls as long as surviving allies remain.

## Player crouch and sprint (COMBAT-01 / COMBAT-02, 2026-11-13)

- User approved: hold Ctrl to crouch, hold Shift to sprint; crouch takes priority when both are held. Crouch moves at 50% and sprint at 150% of the current MoveSpeed stat. No stamina cost.
- Crouch reduces capsule and eye height while keeping the feet fixed. Standing waits until overhead space is clear. Temporary height ratio: 60%, configurable in PlayerControlSettings.
- Both task IDs remain separate; implemented together on feature/combat-01 at the user's request. Owner: 신용준.

- **User-approved crouch view refinement (2026-11-13)**: First-person eye height transitions with SmoothStep over 0.18 seconds (PlayerControlSettings.CrouchViewTransitionSeconds); capsule changes remain immediate. Reversing Ctrl mid-transition continues from the currently displayed height. Root movement/falls follow immediately; camera re-enable resets the transition.

## Player jump (COMBAT-05, user approved 2026-11-15)

- Press Space to jump **1.2m** (PlayerControlSettings.JumpHeight), below one 3m wall tier so walls cannot be jumped onto. Jump speed is derived from the existing gravity so the peak matches the configured height.
- Air control **30%** (PlayerControlSettings.AirControl): while airborne, horizontal velocity keeps 70% of the takeoff velocity and blends in 30% of the current input velocity. A sprint jump keeps the sprint takeoff speed.
- No jump while crouched; crouch input is ignored until landing. No double jump. Hitting a ceiling stops the rise.
- Available in both preparation and battle wherever the player can move; ladders, cannon manning, construction view and falls already suspend movement, so jumping is unavailable there. Keyboard only for now (Q-15).

## Action Interrupt & Poise System (COMBAT-04, user approved 2026-11-16)

- **Model**: Single-hit threshold comparison. Each attack/damage source carries `StaggerPower` (configured on `CombatProfile`), and each active action exposes `IsInterruptible` and `Poise`. On receiving effective damage (`> 0`), if `action.IsInterruptible && incomingStagger >= action.Poise`, the active action is interrupted.
- **Initial Scope**: Ladder pushing (shared across player, allied AI, and enemies) is connected first; other actions (such as cannon operation) can adopt the same interruptible action contract later.
- **Ladder Rules**:
  - **Ladder pushing**: Interruptible (`Poise` <= ranged `StaggerPower`), so both melee and ranged hits interrupt pushing. When interrupted before passing the ladder's balance point, the ladder settles back onto the wall (Bannerlord style) just like releasing the push.
  - **Ladder raising**: Uninterruptible (`IsInterruptible = false`), so hits do not cancel raising.
- **Re-interaction Cooldown & Feedback**: Interrupted units wait `0.3s` (`SiegeSettings.LadderPushInterruptCooldown` / configurable) before re-engaging the push/interaction. For testing, only player action interruptions display a temporary HUD notice.
