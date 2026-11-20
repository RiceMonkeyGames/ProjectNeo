# BUG-hired-unit-not-joining-squad | 2026-11-20 | Flow - Captain Walks to Its Squad

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-20: Captain Assigned in the Organization Window Walks to Its Squad

- Author: 신용준, branch `fix/bug-hired-unit-not-joining-squad`.
- Symptom (user playtest): a hero hired at the tavern and then assigned to a squad stayed in front of the tavern.
- Cause: hiring with a squad picked in the tavern window already sent the hero to the squad's gather point (`GameUnitSpawner.HireAtTavern`). Hiring as a reserve hero (the default) and then making it captain in the `O` organization window went through `GameRunLink.TrySetCaptain`, which changed the run and scene leader but gave the unit no move order. Moving a captain straight to another squad used the same path.
- Fix: `TrySetCaptain` now sends the new captain to the squad's gather point (`SquadCommandCenter.TryGetGatherPoint`) with a `Move` directive, like `TryMoveSoldier` does for grunts.
- Barracks recruits checked: a recruit joining an existing squad already walks to it; a recruit that opens a new squad has no gather point and holds in front of the barracks (unchanged).
- Not changed: a captain sent back to reserve stays where it stands (no rule yet for walking back to the tavern).
- Test: `GameOrganizationPlayTests` (reserve hero holds, then gets `Move` toward squad 0's gather point after `DropOnSquad(0)`).
- Verified: compile passed; PlayMode `GameOrganizationPlayTests` 3/3 and `GameTavernPlayTests` 1/1 passed. Full EditMode/PlayMode suites not run. User manual check in `Game.unity` (reserve hire, `O` assign to Squad 1 then Squad 2): passed. Standalone build unverified.
