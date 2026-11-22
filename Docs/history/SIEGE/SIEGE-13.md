# SIEGE-13 | 2026-11-23 | Siege - Synty-Scale Battlements, Sloped Stair Battlements & 6m Main Gate (Superseded)

Status: **Superseded** without implementation (2026-11-23, youngho). Branch `feature/siege-13` has no commits beyond the claim. Decision: [Decisions-Resolved](../../plan/Decisions-Resolved.md) ("Game adopts the 5m castle kit").

## Original scope

Registered 2026-11-22 during ART-06 under the Q-01 choice to keep 3m cells and tiers: slice `SM_Bld_Castle_Battlements_01` into repeat modules tiled on the 3m pattern, add stepped battlements along wall-top stairs, raise the main gate to a two-tier 6m `Wall_Gate_01`, raise the default walls to two tiers, and place ruined-wall assets in collapsed cells.

## Why it closed

The team decided on 2026-11-23 to move the Game to 5m cells and tiers with the `PolygonFantasyKingdom` castle kit at its native proportions ([SIEGE-14](SIEGE-14.md) spike). The spike already covers the replacements: kit battlements with exact colliders and allied `ShotCover`, a one-cell `Wall_Gate_01` with outer and inner frames, portcullis and a two-storey gatehouse, automatic wall stairs (straight and quarter-turn), and V-shaped breach ruins. Nothing was implemented under this ID.

## Carried forward

- Cover heights and crenel stand points on the kit battlements: `SIEGE-20`.
- Main gate and gatehouse in the Game: `SIEGE-22`.
- Battlements beside wall-top stairs: the kit stairs sit on the walkway between existing battlements, so no sloped battlements are needed; revisit in `SIEGE-19` if a gap shows up in the Game.
- Ruined-wall assets in collapsed cells: done in the spike, ported with `SIEGE-19`.
