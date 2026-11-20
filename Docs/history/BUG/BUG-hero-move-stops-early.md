# BUG-hero-move-stops-early | 2026-11-20 | Squads - Hero Captain Stops Before Reaching a Move Destination

Last Updated: 2026-11-20 12:50:17 (KST)

> Purpose: Task history. | Parent: [history/README](../README.md) | Related: [CombatAndSquads codemap](../../codemap/CombatAndSquads.md), [Combat](../../design/Combat.md)

Branch: `fix/bug-hero-move-stops-early`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim BUG-hero-move-stops-early`).

## Report (user play, 2026-11-20)

- Repro: hire a hero at the tavern as a squad captain, place the squad, then give that squad a move order.
- Expected: the hero walks to its front-row formation slot with the soldiers.
- Actual: the hero stops partway before reaching the destination.

## Cause

`SquadCommandCenter.Apply` sends the captain (`Squad.Leader`, kept apart from `Squad.Members`) and the soldiers to their formation slots. `SquadCommandCenter.Update` then completes a squad's move as soon as no `Members` are still on a Move directive, but it never looked at the `Leader`. When the soldiers arrived first, which is common because a newly hired hero starts in front of the tavern, `CompleteMove` turned the order into Hold, and `Apply(Hold)` set the still-walking captain to Hold where it stood.

## Fix

- `SquadCommandCenter.Update` counts the leader as moving too (new `IsMoving` helper: alive, not detached by morale, directive Move), so the squad move completes only after the captain and every soldier have arrived.
- New PlayMode `GameSquadMovePlayTests.SquadMove_WaitsForTheCaptainBeforeCompleting`: a captain hired at the tavern and a squad sent to where its soldiers already stand; the order must stay Move after the soldiers arrive, the captain must reach the formation (within 4 m of the destination), and the squad then holds.

## Coordination

- Teammate task `BUG-hired-unit-not-joining-squad` (신용준) was merged into develop first; it changed `GameRunLink.TrySetCaptain` (a new captain walks to its squad), not `SquadCommandCenter.cs`. This branch was fast-forwarded onto that develop before testing, so both fixes are tested together.

## Verification

- Regression check: with the fix set aside, `GameSquadMovePlayTests` fails (the captain stopped 42.1 m from the destination after starting 44.5 m away); with the fix it passes.
