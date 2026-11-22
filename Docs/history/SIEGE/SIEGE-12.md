# SIEGE-12 | 2026-11-22 | Siege - Gate Machicolations (Shooting Down at the Wall Base)

Branch: `feature/siege-12`. Owner: SeongminHwang. Claim synchronized to develop (`docs: claim SIEGE-12`). Implementation commit `59d0911`. Design record: [WallsAndSiege](../../design/WallsAndSiege.md) (Machicolations), [Combat](../../design/Combat.md) (Line of sight). Decision log: [Decisions-Resolved](../../plan/Decisions-Resolved.md). Code map: [WallsAndSiege codemap](../../codemap/WallsAndSiege.md), verification scene in [PrototypeScenes](../../codemap/PrototypeScenes.md). Follows BUG-wall-archer-shoots-through-footing (the shooter's own wall piece blocks shots at the wall base).

## Decisions (user, 2026-11-22)

| Item | Choice |
|---|---|
| Where | Main gate top only, fixed, free, present from the start; machicolations on other wall pieces are a later extension |
| Who | Allied AI units and the player |
| Attack methods | Swappable modules; this task ships arrows straight down only (stones, oil later) |
| Method choice | Automatic by attacker type, no UI |
| AI use | Allied ranged unit in `Hold` near a free opening walks to it when an enemy is below and nothing else is visible, returns to its post when none is left; one unit per opening |

Agent defaults (temporary data in `Machicolations.asset`, open to revision after playtest): one opening per gate cell on each outward face at the face middle (the crenel), so the 2-cell gate has 2; stand spot 0.85m inside the face, used within 0.6m; shaft covers the whole 3m cell width, from 1.2m inside the face (short of the door) to 2m outside, down to the ground; AI search radius 12m from the post; the walk must not drop below the post (`HoldMaxDescent`); an opening someone else (the player included) stands on is not taken. Placeholder look: an overhang slab under the walkway on the outer face plus a dark hole marker, with no collider and no NavMesh effect.

## Implemented scope

- Pure rules: `Combat/MachicolationShaft` (shaft box in the opening's frame, `Contains`, `IsBelow`), `Combat/MachicolationRules` (`Rank`, `ShouldRelease`, `IsClaimFree`, `PassesThrough`), `Walls/MachicolationLayout` (spots on gate-cell outward faces via `BattlementRules.Faces`, top surface when walls are stacked on the gate).
- Combat: `ShotOpening` (stand spot, shaft, exempt gate-column colliders, attack modules, claim), `MachicolationAttack` / `ArrowMachicolationAttack` modules. `CombatPhysics.TryGetShotPoint` passes exempt colliders whose entry and exit both lie in the shaft; `CombatProjectile` ignores them when the contact lies in the shaft. `UnitCombatAI.UpdateOpening` / `ClaimOpening` / `ReleaseOpening` (released on new directive, death and disable).
- Walls/data: `GateMachicolations` (on the `ConstructionWorld` object; rebuilt on `Changed` and when a column piece's construction state flips), `MachicolationSettings`.
- Editor: `MachicolationSetup` (`ProjectNeo/Setup/Add Machicolations To Game Scene`) created `Data/GameFlow/Build/Machicolations.asset`, `MachicolationArrow.asset`, `Art/Placeholder/MachicolationHole.mat` and wired `Game.unity`. `MachicolationVerificationSetup` (`ProjectNeo/Setup/Create Machicolation Verification Scene`) created `Scenes/MachicolationVerification.unity` and `Data/SiegePrototype/SiegeMachicolationVerification.asset`.

## Verification

- EditMode `MachicolationRulesTests` 11/11; PlayMode `MachicolationPlayTests` 5/5 (archer on the opening hits an enemy hugging the gate; an archer away from it is still blocked by its footing; an enemy archer on the opening cannot use it; a Hold archer walks to the free opening, kills the enemy below and returns to its post; an opening someone stands on is not taken).
- Full `unity-verify-local.ps1 -Mode all`: EditMode **701/701** (4.21s), PlayMode **309/309** (166.19s). Before the commit: EditMode **701/701**, `MachicolationPlayTests` **5/5**. `code-rules-check.ps1`: 0 violations. Dotnet precheck skipped (the generated csproj still listed a SIEGE-11 file after the branch switch).
- `Game.unity` Play check (agent): 2 openings on the gate's north (outer) face at walkway height 3m.
- `MachicolationVerification.unity` Play run (agent, Run In Background on): two archers claimed both north openings when the 3 gate breakers reached the gate, all breakers died within about 35s of game time, all four archers returned to their posts.
- Defect found and fixed: the first `MachicolationSetup.Apply` left the scene component's settings reference empty because opening the scene unloaded the just-created asset; the setup now reloads it after `OpenScene` (same as `BattlementSetup`).

User playtest (2026-11-22) of `Game.unity`: it was unclear whether archers used the openings. Cause (agent inspection): at battle start the archer squad holds in the courtyard about 33m from the gate (outside the 12m search radius and on another level), and the first wave has no gate breakers. To see it in `Game.unity`, place an archer squad on or next to the gate top during preparation and reach a wave with gate breakers (`Stage02` wave 2, `Stage05` wave 3); the verification scene was added for a direct check.

Notes: this touches the shared combat core (`CombatPhysics`, `CombatProjectile`, `UnitCombatAI`) and `Game.unity`; tell the teammate before merging. In the verification scene the single wall line counts as outside on both sides, so the gate also gets 2 unused south openings.

Not verified: the user's own run of `MachicolationVerification.unity`, the player shooting through an opening with a bow in play, how the placeholder overhang reads in `Game.unity`, standalone build.
