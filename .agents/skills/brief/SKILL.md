---
name: brief
description: Run a single-call ProjectNeo session bootstrap digest (branch, submodule status, dirty files, Unity readiness, and active Backlog locks) via Tools/Agent/session-brief.ps1.
---

# ProjectNeo brief

Read [the shared teammate workflow](../../../.claude/skills/brief/SKILL.md) and follow it for this request. That file is the maintained workflow; do not duplicate its steps here.

Resolve the repository root from this skill directory and run its Tools/Agent commands from that root. In the shared instructions, `/brief` corresponds to Codex `$brief`, and `$ARGUMENTS` means the arguments supplied in the user's request.

Follow AGENTS.md for submodule handling and Git authorization: initializing or syncing the recorded `Assets/ThirdParty` commit is allowed; do not commit inside `Assets/ThirdParty` or run general commits/pushes without user confirmation.
