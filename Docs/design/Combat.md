# Game Design: Combat, Units & Win/Loss

Last Updated: 2026-11-19 20:21:53 (KST)

> Purpose: Rules for perspective, squad commands, damage, unit lifecycles, and battle outcomes. | Parent: [design/README](README.md) | Related: [WallsAndSiege](WallsAndSiege.md), [MoraleAndWave](MoraleAndWave.md)

## Perspective & Squad Commanding

- Default perspective is **3D first-person** (confirmed 2026-11-11): direct player control combined with squad commanding.
- Prototype combat uses WASD movement, mouse look, and left-click frontal melee attack (jump, guard, directional attacks are follow-ups).
- Player forms squads and assigns heroes as captains. No separate commands are given directly to individual heroes. Squads without captains obey identical orders.
- **Controls** (subject to playtest tuning): F1–F4 select squads, backtick (`` ` ``) selects all, `1` Move, `2` Attack, `3` Hold.
- **Command Behaviors**:
  - **Move**: Disregards enemies and marches to screen-center target; transitions to Hold upon arrival.
  - **Hold** (revised 2026-11-12, Bannerlord style): Defends guard post (where Hold was issued or Move finished).
    - *Melee*: Advances to engage walkable enemies within 6m (`HoldEngageRadius`, data-configurable); does not pursue beyond range or broken paths; can descend stairs to engage breaches.
    - *Ranged*: Fires within range; advances toward reachable enemies within `range + 6m` (`HoldRangedChaseExtra`, data-configurable) until in firing range; shoots downward at enemies beneath walls; does not chase unreachable enemies.
    - *Return*: Returns to post when targets clear; minor displacement within 1m does not trigger return; halts nearby if post is blocked.
    - Unreachable enemies (no walkable path) are ignored so defenders push ladders instead of stalling.
  - **Attack**: Marches toward target point while engaging enemies spotted in visual range; resumes march after clearing; waits in Attack posture if arrived/empty.
  - **Vision**: Configurable detection radius; wall LoS occlusion and FoV angles omitted currently.
  - Unreachable destinations or broken paths halt at closest reachable point; clustered stalls near destination count as arrived.
  - Squads without orders at battle start begin in Hold state. Squad count/caps are data-driven. Final values (Q-57, team decision 2026-11-19): at most **10 squads** with **20 soldiers** each; the data still holds the temporary G-1 3 x 4 until PREP-09.
  - Captain hero death drops squad morale by the largest fixed amount (2026-11-12).

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
  - **Arrow flight**: An arrow flies at least as far as the aimed point of a target that passed the range check. Range is measured to the nearest point of the target's body but arrows aim at its center, so steep shots near maximum range used to vanish just short of the target.
  - **Line of sight**: Unchanged. The shooter's own footing piece never blocks its shots; other wall pieces still block both ways, so a wall edge that hides a defender also protects it. No walking to the wall edge.
  - **Hold with no shot and no path**: The unit stays on its post and re-checks every decision interval; it does not leave the wall to chase.
- No friendly fire.
- Ranged attacks fire linear projectiles upon release: dodgeable post-launch, blocked by walls, pass through allies.

## Units

| Category | Description |
|---|---|
| Player Character | Controllable hero. Possesses stats, skills, and items identical to heroes. |
| Hero | Hired at tavern. Can act as squad captain. **Permadeath** on death; cannot be revived. |
| Minion (Grunt) | Recruited at barracks. Eliminated on death; only fleeing/routed minions return next battle. |

- Surviving heroes and minions fully recover HP before the next battle (2026-11-12). Only wall HP damage carries over.
- Fallen ally corpses remain on the ground during the next preparation phase (2026-11-12), excluded from squads and run. At the next prep start fallen units leave their scene squads (bodies stay), so they no longer take squad slots (Q-50, 2026-11-13).
- **Fallen hero equipment** (Q-31, user 2026-11-19): Equipment is never lost on hero permadeath. When the battle result is applied, the fallen hero's equipped items return to the run stash with their upgrade levels and can be equipped again by the player or another hero (implementation: PREP-08).

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
