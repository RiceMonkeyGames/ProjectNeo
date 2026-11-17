# QA-physical-input | 2026-11-17 | QA - Physical-Input Manual Play Sweep

Last Updated: 2026-11-17 14:04:07 (KST)

> Purpose: Record user-reported physical-input verification. | Parent: [history/README](../README.md) | Related: [Backlog](../../Backlog.md), [Scenes](../../codemap/Scenes.md)

## Evidence and scope

On 2026-11-17, after receiving the six-item checklist in this chat, the user explicitly reported that every item passed. This is user-reported Unity Editor manual play evidence; the agent did not independently replay the checks. The checklist targeted `Assets/_Project/Scenes/Game.unity`. No new compile, automated tests or standalone build were run for this documentation update.

| Task | User-confirmed result |
|---|---|
| PREP-01 | Pass: Smithy upgrades, caps/payment protection, equip persistence/stats and sale refund. |
| PREP-04 | Pass: physical Q input, potion count, full-HP refusal, healing/cooldown and next-preparation retention. |
| SIEGE-07 | Pass: cannon interaction, crosshair readiness/reload feedback, traverse and leaving the cannon. |
| COMBAT-03 | Pass: morale HUD readability and Panic retreat/wait/order refusal behavior. |
| SIEGE-04 | Pass: physical ladder keys, contextual HUD, raising/boarding/traversal/toppling and combat restrictions. |
| FLOW-01 | Pass: hold-B start/cancellation, position/view preservation and modal input guards. |

The user supplied an overall confirmation, with no per-step measurements or screenshots. Earlier task histories retain their original automated/API verification evidence; this record adds the later human play result.

## Follow-ups

- `BUG-popup-cursor`: user reports visible cursor remaining after a building popup closes; also requests automatic capture when first-person gameplay begins. Registered separately, reproduction pending.
- `UI-03`: user requests a player HP HUD. Registered separately, not implemented by this QA task.
- Owner: 신용준. Done/archive authorized by the user on 2026-11-17 after confirming every checklist passed. Published with the subsequent UI-03 claim documentation.

## Repeat and reset

Open `Assets/_Project/Scenes/Game.unity` and enter Play. Check Smithy F / equipment I, battle-start hold B (1 second), potion Q, cannon F / left click / mouse / F or Esc, ladder tap F / hold F / W / S, and squad F1-F3 / 1-3 with morale/Panic HUD. Expected outcomes follow the individual task histories and the checklist provided in this chat. Stop Play and reopen Game to reset temporary runtime state; do not save play changes to scenes/assets. Standalone verification remains separate.
