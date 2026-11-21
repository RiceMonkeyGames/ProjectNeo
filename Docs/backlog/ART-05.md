---
id: ART-05
title: Arrow Model & Arrows Stuck on Hit
status: Waiting
assignee: Unassigned
branch:
area: ART
depends_on: []
created: 2026-11-21
---

# ART-05 — Arrow Model & Arrows Stuck on Hit

## Scope & Completion Criteria

Registered 2026-11-21 from user request after COMBAT-09. Replace the placeholder sphere projectile with an arrow model (Synty or another licensed asset in `Assets/ThirdParty`) that points along its flight arc (`CombatProjectile` already faces its velocity). When an arrow hits a wall, battlement, ground or character it stays stuck at the hit point instead of disappearing (stuck in a character it follows the body and is cleared on death or after a while). Open before start: asset choice, how long stuck arrows stay and a cap on their number (performance), and cleanup between battles (B-9.3 checks that no projectile remains in the scene). Touches `CombatProjectile` and the projectile prefabs: coordinate with the teammate.
