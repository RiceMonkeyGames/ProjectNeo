---
name: sync-summary
description: Summarize teammate changes on origin/develop, backlog updates, and runtime API changes for ProjectNeo.
---

# ProjectNeo sync-summary

Read [the shared teammate workflow](../../../.claude/skills/sync-summary/SKILL.md) and follow it for this request. That file is the maintained workflow; do not duplicate its steps here.

Resolve the repository root from this skill directory and run its Tools/Agent commands from that root. In the shared instructions, `/sync-summary` corresponds to Codex `$sync-summary`, and `$ARGUMENTS` means the arguments supplied in the user's request.

Follow AGENTS.md for ownership and Git authorization: immediate claim synchronization is allowed; completion requires user confirmation before Done/archive/commit/push. Leave develop merges and remote branch deletion to the user. Reading this skill does not authorize a Unity compile/test session when another session is running.
