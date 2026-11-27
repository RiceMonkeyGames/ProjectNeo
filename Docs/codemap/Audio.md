# Runtime Codemap: Audio

> Purpose: Class map for the audio foundation: mixer buses, volume settings, cue rules, the pooled spatial SFX player and the BGM crossfade. | Parent: [codemap/README](README.md) | Related: [Principles](../workflow/Principles.md), [Decisions-Resolved](../plan/Decisions-Resolved.md) (AUDIO-01 entry)

Base path: `Assets/_Project/Scripts/Runtime/Audio/`. Assembly: `ProjectNeo.Runtime`. Namespace: `ProjectNeo.Audio`.

Status: no gameplay code calls the director yet. Production SFX wiring is `AUDIO-02` and BGM wiring is `AUDIO-03`. The director lives across scenes (`DontDestroyOnLoad`), so `Title` <-> `Game` loads do not duplicate it or its `AudioListener`.

| Folder | Key Classes | Role |
|---|---|---|
| Audio (pure rules) | `AudioBus` | Persistent enum: `Master = 0`, `Bgm = 1`, `Sfx = 2`, `Ui = 3`, `Voice = 4` (reserved for `Q-83`). Values are saved, so do not renumber. |
| Audio (pure rules) | `AudioVolumeMath` | Linear 0-1 <-> decibel conversion. 0 maps to -80 dB (silent). |
| Audio (pure rules) | `AudioSettingsData` | Per-bus volume and mute flags (JsonUtility fields), `Decibels(bus)`, `EffectiveLinear(bus)` (master x bus, used on the no-mixer path), `Sanitize()` for out-of-range values. Ready for `UI-11` sliders. |
| Audio (pure rules) | `AudioSettingsStore` | Global `audio.json` (not per save slot, not `PlayerPrefs`). Default folder `persistentDataPath/Settings`, saved through a temp file and swap. A missing or broken file gives defaults and sets `LastError`. |
| Audio (pure rules) | `AudioCueRules` | Clip pick that never repeats the previous clip, volume and pitch jitter. Uses a presentation-only `IRandomSource`, never the gameplay random stream, so sound cannot change battle or reward results. |
| Audio (pure rules) | `CueCooldownGate` | Per-cue cooldown: a cue plays only after its cooldown seconds have passed. |
| Audio (pure rules) | `VoiceAllocator` | Fixed-slot voice policy. A per-cue cap replaces the oldest voice of that cue. A full pool steals the lowest priority, then the farthest, then the oldest voice, only when the victim is less important (or equally important and at least as far). Otherwise the new sound is dropped. |
| Audio (pure rules) | `BgmCrossfade` | Two-channel linear crossfade and fade-out. The caller ticks time; the class holds no `AudioSource`. |
| Audio (data) | `AudioCueDefinition` | ScriptableObject for one sound event: clips, bus, volume and pitch with jitter ranges, `spatialBlend` (2D UI to 3D world), min/max distance, `priority` 0-255 (higher = more important), `cooldownSeconds`, `maxVoices` (0 = unlimited). `Create(...)` factory. |
| Audio (data) | `AudioDirectorConfig` | ScriptableObject holding the mixer and the five bus groups, `spatialVoices` 32, `flatVoices` 8. `VolumeParameter(bus)` gives the exposed names `MasterVolume`, `BgmVolume`, `SfxVolume`, `UiVolume`, `VoiceVolume`. `ResourcePath = "Audio/AudioDirectorConfig"`. |
| Audio (runtime) | `AudioDirector` | MonoBehaviour, single instance created on first `Instance` access (or `Create(config, store, random)` in tests), `DontDestroyOnLoad`. Pre-warmed 3D and 2D `AudioSource` pools (no per-shot allocation). `Play` / `PlayAt` (3D sounds beyond max distance from the `AudioListener` are skipped), `PlayBgm` (crossfade; the same cue is not restarted) / `StopBgm` / `PlayStinger`, `SetVolume` / `SetMuted` / `ApplySettings` / `SaveSettings`, `TryGetMixerDecibels`. Uses unscaled time. Never adds an `AudioListener`. A null cue or clip is a silent no-op that returns `false`. |
| Audio (assets) | `ProjectNeoMixer.mixer` (`Assets/_Project/Audio/`) | `Master` with `BGM`, `SFX`, `UI`, `Voice` children and the five exposed volume parameters. |
| Audio (assets) | `AudioDirectorConfig.asset` (`Assets/_Project/Resources/Audio/`) | Config loaded by `AudioDirector` through `Resources.Load`, wired to the mixer above. |
| Editor | `Editor/AudioSetup` | Menu `ProjectNeo/Setup/Audio Foundation (AUDIO-01)`. Idempotently builds the mixer and the config asset. Uses the internal `UnityEditor.Audio.AudioMixerController` API by reflection, because Unity has no public mixer-creation API. `AddGroupToCurrentView` is not called because it throws on a new mixer with no views. |
| Tests (EditMode) | `AudioSettingsTests`, `AudioPlaybackRulesTests` (`Assets/Tests/EditMode/`) | dB math, settings sanitize and JSON round trip, store defaults and broken-file handling, cue clip pick without repeats, jitter ranges, cooldown gate, voice cap, priority and distance stealing. |
| Tests (PlayMode) | `AudioDirectorPlayTests` (`Assets/Tests/PlayMode/`) | Pool reuse and reclaim, cooldown, cap and distance skip, priority stealing, null no-ops, crossfade, mixer routing and dB, no-mixer volume, and `Title` -> `Game` -> `Title` persistence with one `AudioListener`. |
