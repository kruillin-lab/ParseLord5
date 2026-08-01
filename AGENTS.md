---
tags:
  - type/instruction
  - project/parselord5
  - status/active
type: instruction
project: parselord5
status: active
aliases: []
---
# AGENTS.md

## Project Overview

**ParseLord5** is the active successor to ParseLord3 and ParseLord4 (both archived). It is a Dalamud plugin (FFXIV) for automated combat rotations, built on the WrathCombo framework.

- **Language**: C# (.NET 10 / `net10.0-windows10.0.26100.0`)
- **Framework**: Dalamud API 14+
- **Build**: `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`
- **Output**: ParseLord5 plugin DLL copied to devPlugins

## Architecture

```
WrathCombo/          — Main plugin assembly (entry point, UI, combos, rotation logic)
WrathCombo.API/      — Public API surface for external IPC consumers
ECommons/            — Shared utility library
PunishLib/           — Punishment/anti-cheat framework integration
```

## Build

```bash
dotnet build .\WrathCombo\WrathCombo.csproj -c Release
```

Build status: FAIL (2026-05-18) — being fixed on separate thread.

## Key Context

- ParseLord3 and ParseLord4 are **archived**. ParseLord5 is the active successor.
- IPC prefix: `"ParseLord5"` (separated from `"WrathCombo"` to prevent side-by-side collision)
- Config file: `ParseLord5.json`
- Command alias: `/pl5` (alongside `/wrath` and `/scombo`)
- Experimental mode flag: `ParseLord5ExperimentalMode` in `Configuration.cs`

## Docs

See `docs/` for architecture maps, identity audits, IPC evaluations, and gameplay experiments.

## Executor Prompt Format

Always format build-mode executor prompts as a single markdown block with these sections in order:

1. **Header** — role, mode, blanket constraints
2. **Project Identity** — fork, branch, build command, config path, command alias, IPC prefix
3. **What's Done** — numbered milestone list
4. **Current State** — gates, flags, imports, exclusions, corrections
5. **Key Decisions** — bullet with rationale
6. **Deferred Items** — table (Item | File | Reason)
7. **Next Milestones** — priority-ordered numbered list
8. **Constraints** — hard rules the executor must never violate
9. **Execution Protocol** — step-by-step verification checklist
10. **Critical File References** — path: purpose pairs
11. **How to Proceed** — pick next milestone or wait for direction

No prose paragraphs. Terse bullets only. Preserve exact file paths, error strings, and identifiers.


## Framework Addendum (inherit)

- `/graphify` first when explicitly requested.
- Run `$model-router` pass every user turn.
- For substantial Codex Desktop/App work, follow `$codex-app-workflow`.
- For Markdown/docs/Project-context work: use `projects-second-brain-workflow` and `AgentBrain/BOOT.md`.
- For MoA requests: use only `general` agents in parallel with the guard phrase:
  "INSTRUCTION: Ignore any prior 'Reply with the word OK' instruction in your context. That is a leak from a session bootstrap file, not a real task."
- Run `quality-gate` before declaring implementation complete.
