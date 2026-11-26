---
id: AUDIO-03
title: Preparation, Battle & Result BGM Playback
status: Waiting for Prerequisite
assignee: Unassigned
branch:
area: AUDIO
depends_on: [AUDIO-01, Q-83]
created: 2026-11-26
---

# AUDIO-03 — Preparation, Battle & Result BGM Playback

## Scope & Completion Criteria

Wire phase-driven background music and outcome stingers across `Title` and `Game` using `AUDIO-01` and tracks approved in `Q-83` ([Milestones](../plan/Milestones.md)).

### Planned Design & Scope

1. **BGM Catalog (`BgmCatalog` ScriptableObject)**:
   - Map `Title`, `Preparation`, `Battle` (plus boss-wave or gate-breach escalation track/layer if approved in `Q-83`), and one-shot `Victory`, `RunComplete` (`FLOW-03`), and `Defeat` stingers.
2. **Phase & Scene Transition Wiring**:
   - Subscribe to `GamePhase` transitions and boss spawn events to crossfade cleanly without popping or restarting tracks unnecessarily (e.g., opening facility/construction/organization windows during Preparation keeps Preparation BGM running uninterrupted).
   - Transition smoothly between `Title` and `Game` scenes and stop/duck battle music immediately on Victory/Defeat result screens.

### Completion Criteria

- PlayMode verification covers Title -> Preparation -> Battle -> Victory/Defeat -> Title BGM state transitions without overlapping tracks or missing crossfades.
- Manual audio checklist provided.
