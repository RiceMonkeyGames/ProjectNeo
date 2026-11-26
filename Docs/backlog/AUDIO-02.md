---
id: AUDIO-02
title: Combat, Siege & UI SFX Wiring
status: Waiting for Prerequisite
assignee: Unassigned
branch:
area: AUDIO
depends_on: [AUDIO-01, Q-83]
created: 2026-11-26
---

# AUDIO-02 — Combat, Siege & UI SFX Wiring

## Scope & Completion Criteria

Wire production SFX cues across combat, siege structures, squad commanding, morale, and UI interactions using `AUDIO-01` and assets approved in `Q-83` ([OpenQuestions](../design/OpenQuestions.md), [Goal](../plan/Goal.md)).

### Planned Design & Scope

1. **Combat SFX**:
   - Melee weapon swing, hit impact (flesh/armor/stagger), unit death, player damage/low-HP warning.
   - Bow draw/release, arrow flight/impact/stick (`ART-05`), active skill cast & effect impacts (`COMBAT-06`), potion drink (`PREP-04`).
2. **Siege & Castle SFX**:
   - Cannon fire-ready chime/cue (`SIEGE-07` follow-up in [OpenQuestions](../design/OpenQuestions.md)), cannon fire boom, shell explosion, barrel swivel/reload tick.
   - Ladder raise/settle/topple, gate/wall hit & structural breach/collapse rumble, preparation gate open/close, construction placement/demolish/repair hammer cues.
3. **Squad, Morale & UI SFX**:
   - Squad selection, `Move`/`Attack`/`Hold`/formation-cycle confirmation cues (plus voice barks if approved in `Q-83`).
   - Morale `Awaken` / `Collapse` / rout stingers.
   - Facility popup open/close, button hover/click, gold spend/earn, craft/upgrade anvil hit, perk/unlock activation, prep-clock tick/wait.

### Completion Criteria

- All SFX triggers bind via decoupled events/presentation adapters (zero audio references inside pure C# rules).
- Mass-battle PlayMode/manual test verifies voice caps and cooldowns prevent clipping during heavy multi-wave combat.
- Asset list documented and manual audio checklist provided.
