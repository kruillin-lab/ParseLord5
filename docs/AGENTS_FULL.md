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

## Cursor Cloud specific instructions

ParseLord5 is a **Dalamud in-process plugin**; there is no standalone `dotnet run` or dev server. Cloud verification is **compile + domain evals**; in-game load requires Windows + FFXIV + XIVLauncher/Dalamud.

### Prerequisites (one-time on a fresh VM)

- **.NET 10 SDK** on `PATH` (install to `~/.dotnet` if missing; `export PATH="$HOME/.dotnet:$PATH"`).
- **Git submodules**: `ECommons`, `PunishLib`, `WrathCombo.API` (run `git submodule update --init --recursive`).
- **Dalamud dev hook DLLs** at `~/.xlcore/dalamud/Hooks/dev/` on Linux (Windows: `%AppData%\XIVLauncher\addon\Hooks\dev\`). Download `https://goatcorp.github.io/dalamud-distrib/latest.zip` when `Dalamud.dll` is absent.

### Build and verify (Linux)

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build WrathCombo/WrathCombo.csproj -c Release
pwsh -File scripts/rotation-evals.ps1
```

Release output: `WrathCombo/bin/Release/ParseLord5.dll` and `ParseLord5.json` (`InternalName` / `DalamudApiLevel` 15).

### Lint / tests

- No dedicated linter or `dotnet test` projects.
- Domain checks: `scripts/rotation-evals.ps1` (preset enum, job coverage, unique IDs).
- Full solution build: `dotnet build WrathCombo.slnx -c Release`.

### In-game dev loop (not available in Cloud Agent VMs)

On a Windows machine with XIVLauncher: build Debug (outputs to devPlugins), add DLL under Dalamud **Experimental → Dev Plugin Locations**, enable **Auto Reload**, use `/pl5` or `/wrath`. Disable other WrathCombo/ParseLord5 instances to avoid collisions.

### Gotchas

- Build fails with thousands of `AtkUnitBase` / `FFXIVClientStructs` errors → Dalamud hook path is missing or stale; refresh `latest.zip` into the `Hooks/dev` folder above.
- `quality-gate.json` `markdown-audit` command is Windows-local and not runnable in Cloud.
- Submodule pins must match; do not bump `ECommons` without a deliberate compatibility pass.

### Automating cloud agent → local dev DLL (Windows)

**Automatable:** `git fetch` / `pull` → `dotnet build` → `rotation-evals.ps1` → DLL in `%AppData%\XIVLauncher\devPlugins\ParseLord5\`.

**Not automatable without the game running:** enabling the dev plugin, starting FFXIV, or hot-reload (use Dalamud **Auto Reload** while logged in).

One-shot script:

```powershell
cd C:\Users\kruil\Documents\Projects\ParseLord5
.\scripts\sync-dev-build.ps1 -Notify
```

| Trigger | How |
|---------|-----|
| Manual | Run after agent finishes or on a hotkey |
| Scheduled | Task Scheduler every 5–15 min while iterating: `pwsh -NoProfile -File ...\scripts\sync-dev-build.ps1` |
| On push | Self-hosted GitHub Actions runner on this PC, or webhook → script |
| Hermes cron | `hermes cron` job calling the same PowerShell line |

Agent branch instead of base: `-Branch cursor/your-branch`.
