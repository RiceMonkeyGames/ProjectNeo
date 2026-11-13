# TOOL-claude-read-deny | 2026-11-12 | Tooling - Claude Code Read Exclusions

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-12: TOOL-claude-read-deny - Claude Code Read Exclusions

- Added shared `.claude/settings.json` with `permissions.deny` `Read(...)` rules to cut token usage across three Claude Code users. `.claudeignore` is not supported by Claude Code, so it was not used.
- Blocked: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, `Build(s)/`, `*.csproj`, `*.sln(x)`, `Assets/TextMesh Pro/`, `*.meta`, `*.unity`, `*.prefab`, `*.mat`, `*.shadergraph`, `*.fbx`, `*.blend`, `*.psd`. Kept readable: `.cs`, `.asmdef`, ScriptableObject `.asset`, `ProjectSettings/`, `Docs/`.
- Updated Context.md Section 3 (Work Environments). No game code or Unity assets changed.
- Verification: JSON syntax parse only. Claude Code behavior must be confirmed manually.
- Manual verification: Restart Claude Code at the repository root, run `/permissions` and confirm the deny rules are listed; ask it to read a `.unity` or `.meta` file and confirm it is refused; confirm `.cs` files still read normally.

### 2026-11-12: TOOL-claude-read-deny (revision) - Deny List Review for Claude-Led Development

- Fixed: `Logs/**` block broke the documented compile check (`Logs/Editor.log` search for `error CS`). Now only `*-prev.log`, `AssetImportWorker*`, `*.jsonl` in `Logs/` are blocked.
- `Library/` narrowed to generated subfolders so `Library/PackageCache/` package source can be read by explicit path (gitignored, so not hit by default searches).
- Unblocked `.prefab` and `.mat` (project files are small; needed for reference debugging). Kept `.unity` blocked (`Game.unity` ≈ 596 KB).
- Added pre-emptive blocks for art phase: `Assets/Synty/`, `Assets/Polygon*/`, `Assets/Plugins/`, `Assets/Samples/`, textures under `Assets/`, models, audio, fonts, video, `*.anim`, `*SDF.asset`, `LightingData.asset`, `NavMesh*.asset`, binaries.
- Added `Edit(...)` deny for `*.unity`, `*.prefab`, `*.asset`, `*.meta`, `*.mat`, `*.controller`, `*.anim` to enforce the Editor-API-only rule.
- Verification: JSON parse only. Manual: restart Claude Code, run `/permissions`; confirm `.unity`/`.meta` reads and `.asset` edits are refused, while `.cs`, `.prefab`, and a `Grep` for `error CS` in `Logs/Editor.log` work.
