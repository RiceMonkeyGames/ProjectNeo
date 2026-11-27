---
id: AUDIO-01
title: Audio System Foundation, Spatial SFX Pool & Mixer Buses
status: In Progress
assignee: SeongminHwang
branch: feature/audio-01
area: AUDIO
depends_on: []
created: 2026-11-26
---

# AUDIO-01 — Audio System Foundation, Spatial SFX Pool & Mixer Buses

## Scope & Completion Criteria

Build the reusable runtime audio infrastructure (M5 foundation) before wiring production SFX/BGM clips (`AUDIO-02`, `AUDIO-03`), keeping all audio strictly decoupled from pure C# gameplay rules ([Principles](../workflow/Principles.md), [Milestones](../plan/Milestones.md)).

### Planned Design & Scope

1. **Mixer & Volume Settings**:
   - Create a shared `AudioMixer` asset (via Editor API) with buses: `Master`, `BGM`, `SFX`, `UI` (and optional `Voice` reserved for `Q-83`).
   - Implement a pure C# `AudioSettingsData` model + persistence helper (normalized 0–1 volumes, mute flags, decibel conversion) ready for `UI-11` Options Window sliders.

2. **Data-Driven Audio Events (`AudioCueDefinition` ScriptableObject)**:
   - Supports clip array (random variation via `IRandomSource` or non-gameplay RNG), volume/pitch jitter ranges, spatial blend (2D UI vs 3D world), min/max 3D distance, priority, per-cue cooldown seconds, and max simultaneous voice cap (essential so 100+ unit siege battles do not saturate or clip audio).

3. **Pooled 3D SFX & BGM Player (`AudioDirector`)**:
   - Pre-warmed `AudioSource` pool for 3D spatial SFX and 2D UI SFX with zero per-shot GameObject allocation, automatic cooldown gating, and lowest-priority/farthest voice stealing.
   - Dedicated dual-source BGM controller supporting smooth crossfade, fade-out, and one-shot phase stingers.
   - Safe no-op / null-clip handling so EditMode and headless/PlayMode tests never throw errors or depend on audio hardware.

### Completion Criteria

- Pure C# EditMode tests verify volume-to-dB math, per-cue cooldown gating, voice-cap/stealing policy, and settings serialization.
- PlayMode verification confirms pooled playback, crossfade transitions, mixer bus routing, and clean scene transitions (`Title` <-> `Game`) without duplicate `AudioListener` warnings or leaked sources.
- `Docs/codemap/` updated with the new audio module layout; manual verification steps provided.
