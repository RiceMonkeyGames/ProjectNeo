# AUDIO-01 | 2026-11-27 | AUDIO - Audio System Foundation, Spatial SFX Pool & Mixer Buses

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-27: AUDIO-01 - Audio System Foundation, Spatial SFX Pool & Mixer Buses

- **Assignee**: `SeongminHwang` | **Branch**: `feature/audio-01` | **Claim commit**: `aff0ec4`
- **Scope**: M5 audio foundation before production SFX (`AUDIO-02`) and BGM (`AUDIO-03`) wiring. No gameplay code calls the director yet.
- **Built**:
  - Pure C# rules in `Runtime/Audio/`: bus enum, dB math, settings data and JSON store, cue clip/jitter rules, per-cue cooldown, voice allocator (cap and stealing), two-channel BGM crossfade. Details: [codemap/Audio](../../codemap/Audio.md).
  - `AudioCueDefinition` and `AudioDirectorConfig` ScriptableObjects, and the `AudioDirector` MonoBehaviour with pre-warmed 3D and 2D source pools.
  - Mixer `Assets/_Project/Audio/ProjectNeoMixer.mixer` (Master with BGM, SFX, UI, Voice) and config `Assets/_Project/Resources/Audio/AudioDirectorConfig.asset`, both built by `Editor/AudioSetup` (`ProjectNeo/Setup/Audio Foundation (AUDIO-01)`, idempotent).
- **User decisions** (see [Decisions-Resolved](../../plan/Decisions-Resolved.md), AUDIO-01 entry):
  - One `AudioDirector` lives across scenes (`DontDestroyOnLoad`), created on first use, so BGM can continue through `Title` <-> `Game`.
  - Volume settings are stored as a global JSON file (`persistentDataPath/Settings/audio.json`), not `PlayerPrefs`.
  - Sound variation uses a presentation-only `IRandomSource`, never the gameplay random stream.
- **Automated Verification**:
  - EditMode audio tests (`AudioSettingsTests`, `AudioPlaybackRulesTests`): `41/41` PASSED.
  - PlayMode `AudioDirectorPlayTests`: `9/9` PASSED.
  - Full suite (`Tools/Agent/unity-verify-local.ps1 -Mode all`): EditMode `991/991` passed. PlayMode `379/389` (the wrapper's wait ran out; the result was read from `unity command test status` after the run finished).
  - The 10 PlayMode failures do not touch audio code (no gameplay code calls `AudioDirector` yet). Each was rerun alone and failed the same way. They are the same 10 recorded in [COMBAT-15](../COMBAT/COMBAT-15.md): `OneCellGroundStairs` (2 cases), `GameOrganizationPlayTests` O-key close, the 0.22m camera eye-height checks (`CombatPrototypePlayTests`, `WallCommandPlayTests`), `GameBattlePlayTests` HUD overlap, `GameEnemyIntelPlayTests` enemy list overlap, `PrepHudPlayTests` (2), `GameFacilityPlayTests` shop lock text.
- **Manual Verification** (done by the user):
  - Mixer window shows Master, BGM, SFX, UI and Voice, with 5 exposed volume parameters.
  - `AudioDirectorConfig` asset is wired to the mixer.
- **Not verified**: audible playback by ear; standalone build.
- **Lesson**: in PlayMode tests do not unload the test runner's initial scene (it hung the runner). Only clean up scenes that a test itself loaded.
