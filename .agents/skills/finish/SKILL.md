---
name: finish
description: Check ProjectNeo completion docs and verification, obtain user confirmation before archiving and task-branch commit or push, and hand develop merge to the user.
---

# ProjectNeo finish

Read [the shared teammate workflow](../../../.claude/skills/finish/SKILL.md) and follow it for this request. That file is the maintained workflow; do not duplicate its steps here.

Resolve the repository root from this skill directory and run its Tools/Agent commands from that root. In the shared instructions, `/finish` corresponds to Codex `$finish`, and `$ARGUMENTS` means the arguments supplied in the user's request.

Follow AGENTS.md for ownership and Git authorization: immediate claim synchronization is allowed; completion requires user confirmation before Done/archive/commit/push. Leave develop merges and remote branch deletion to the user. Reading this skill does not authorize a Unity compile/test session when another session is running.
