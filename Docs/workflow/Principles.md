# Coding, Verification & Art-Swap Principles

Last Updated: 2026-11-13 13:17:37 (KST)

> Purpose: Architecture standards, pure C# vs MonoBehaviour split, and Synty art-swap decoupling rules. | Parent: [INDEX](../INDEX.md) | Related: [UnityCLI](UnityCLI.md), [codemap/README](../codemap/README.md)

## 1. Coding & Verification Principles

- **Paths & Assemblies**:
  - Runtime: `Assets/_Project/Scripts/Runtime/<System>/`, Assembly `ProjectNeo.Runtime`, Namespace `ProjectNeo.<System>`.
  - Editor: `Assets/_Project/Scripts/Editor/`, Assembly `ProjectNeo.Editor`.
  - Tests: `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/`, Namespace `ProjectNeo.Tests`.
- Decouple rules and calculations into pure C# classes; verify via EditMode. Verify movement, scene integration, and destruction via PlayMode and manual play.
- Store numerical parameters in ScriptableObjects. Provide static `Create(...)` factory methods for testing.
- Maintain one class per file for ScriptableObjects and MonoBehaviours; match file name to class name.
- Explicitly assign numeric values to persistent enums; append new entries to the end.
- Route random generation through `ProjectNeo.Core.IRandomSource`. Identical seeds and inputs must yield deterministic results.
- Distinguish runtime instance IDs from static definition asset IDs.
- Never persist editor/scene object references into serialized run state. Include data lifecycles, duplicate reward guards, and event unsubscriptions in integration tests.

## 2. Designing for Art Asset Swaps (Synty)

Rules for decoupling mechanics from visuals to allow seamless Synty asset drops:

- **Split prefabs into Root (Logic) and `Visual` Child.** Root handles rules, colliders, and navigation; aesthetics belong exclusively to the child GameObject. Swap only the child object.
- **Do not derive colliders or dimensions from meshes.** Unit radius, height, attack range, and grid size are configured via data assets. Mesh scale changes do not affect combat math.
- **Rules never call animations directly.** Logic broadcasts signals (`AttackStart`, `Hit`, `Death`, `SpeedChange`); visual components listen to drive animations, VFX, and SFX.
- **Attack impact timings are data-driven.** Do not rely on animation events. Define hit frame delays in data assets.
- **Assume standard Humanoid rigs for bipeds.** Enables swapping models and animations freely. Pre-configure named empty GameObjects for equip sockets (`RightHand`, `LeftHand`, `Back`).
- **Reference visuals via data assets.** Definitions store references to visual prefabs and icons; code never references prefab names as strings.
- **Wall pieces conform strictly to grid cell dimensions.**
- **Decouple UI layout from graphics.** Centralize fonts, colors, and slice sprites in shared style palettes.
- **Placeholder isolation**: Store placeholder assets in `Assets/_Project/Art/Placeholder/` for easy cleanup. Keep imported 3rd-party packs under root `Assets/` unmodified; place project adaptations in `_Project`.
