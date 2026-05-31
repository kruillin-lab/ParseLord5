---
tags:
  - type/instruction
  - project/parselord5
  - status/active
type: instruction
project: parselord5
status: active
aliases:
  - ParseLord5 executor reference
---
# ParseLord5 — full agent reference

Load when editing combos, IPC, build, or writing executor prompts.

## Architecture

```
WrathCombo/          — Main plugin (UI, combos, rotation logic)
WrathCombo.API/      — Public IPC API
ECommons/            — Shared utilities
PunishLib/           — Anti-cheat integration
```

## Key context

- IPC prefix: `"ParseLord5"` · Config: `ParseLord5.json` · Commands: `/pl5`, `/wrath`, `/scombo`
- `ParseLord5ExperimentalMode` in `Configuration.cs`
- See `docs/` for architecture maps, IPC audits, gameplay experiments.

## Executor prompt format

Build-mode executor prompts — single markdown block, sections in order:

1. **Header** — role, mode, blanket constraints
2. **Project Identity** — fork, branch, build command, config path, command alias, IPC prefix
3. **What's Done** — numbered milestones
4. **Current State** — gates, flags, imports, exclusions, corrections
5. **Key Decisions** — bullet + rationale
6. **Deferred Items** — table (Item | File | Reason)
7. **Next Milestones** — priority-ordered
8. **Constraints** — hard rules
9. **Execution Protocol** — verification checklist
10. **Critical File References** — path: purpose
11. **How to Proceed** — next milestone or wait

Terse bullets only. Preserve exact paths, error strings, identifiers.
