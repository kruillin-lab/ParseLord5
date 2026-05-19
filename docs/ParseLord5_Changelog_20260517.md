---
tags:
  - type/changelog
  - project/parselord5
  - status/active
type: changelog
project: parselord5
status: active
aliases: []
---
# ParseLord5 Changelog — 2026-05-17

## Version 1.0.4.5 — Initial Fork Release

ParseLord5 is a WrathCombo-based fork and iteration that preserves the original architecture while establishing independent identity, configuration isolation, brand-safe IPC, and experimental gameplay gating.

### Identity & Configuration

- **Command alias**: `/pl5` added alongside `/wrath` and `/scombo`. All three route to the same command handler.
- **Config isolation**: `InternalName = "ParseLord5"` in manifest ensures config writes to `ParseLord5.json` — no collision with WrathCombo's `WrathCombo.json`.
- **Authored config paths**: `Search.cs` uses `Svc.PluginInterface.ConfigFile.FullName` for IPC cache invalidation. `RepoCheck.cs` reads `ParseLord5.json`.
- **Zero runtime `.cs` references to `WrathCombo.json`**.
- **Experimental flag**: `ParseLord5ExperimentalMode` (default `false`) gates all gameplay changes.

### Branding Shell

- Window title, About tab, Major Changes popup → `ParseLord5`
- DTR labels → `ParseLord5` / `ParseLord5 Opener`
- Plugin Name (`P.Name`) → `ParseLord5` via `MainWindowUI.resx`
- ImGui window IDs updated (ConfigWindow, TargetHelper)
- MOTD welcome text, MOTD prefix, PunishLib init, IPC log prefix → `ParseLord5`
- `"parselord5"` manifest tag added

### IPC

- **Provider prefix**: Changed from `"WrathCombo"` to `"ParseLord5"`. Resolves InternalName/prefix mismatch for external consumers.
- **Consumer pattern**: `TryGetDalamudPlugin("ParseLord5")` + `EzIPC.Init(..., "ParseLord5")`.
- **WrathCombo IPC**: Separate prefix, no side-by-side collision.
- **Done (2026-05-18)**: `ParseLord5Callback` callback renamed from `WrathComboCallback`. IPC status endpoint (upstream dependency) remains deferred.

### Gameplay Experiments

All gated behind `ParseLord5ExperimentalMode` (default `false`). Zero shared core files touched.

| Job | Preset | Experiment |
|---|---|---|
| WAR | `ST_Simple` | GCD before oGCD (priority swap) |
| DRG | `ST_SimpleMode` | LanceCharge before BattleLitany (buff priority swap) |
| SAM | `ST_SimpleMode` | Ikishoten before MeikyoShisui (cooldown swap) |
| SAM | `AoE_SimpleMode` | Same swap, cross-preset expansion |

### Config Import

- **`ImportFromWrathCombo()`**: Reads WrathCombo config, maps 6 compatible fields (`EnabledActions`, `RotationConfig`, `IgnoredNPCs`, `ActiveBLUSpells`, `DancerDanceCompatActionIDs`, `StatusBlacklist`).
- **UI trigger**: Settings tab → "Import settings from WrathCombo" button → confirmation dialog.
- **Read-only on source**. Explicit user action only. Never runs on load.
- **`ParseLord5ExperimentalMode` stays `false`** after import.

### Known Deferred Items

| Item | Reason |
|---|---|
| IPC status endpoint | Upstream dependency on `PunishXIV/WrathCombo` |
| MOTD fetch URL | Upstream dependency |
| `WrathCombo.API` project/namespace | Public API surface for consumers |
| C# namespaces/classes `WrathCombo.*` | Mergeability-preserving defer |
| RepoUrl, IconUrl, PackageProjectUrl, logo | Requires ParseLord5-owned repo |

### Build

- **0 errors, 8 pre-existing warnings** (CS8618 ×5, CS0219 ×2, CS0649 ×1)
- Target: `net10.0-windows`, AssemblyName: `ParseLord5`
- Output: `ParseLord5.dll`

### Documentation

- `docs/ParseLord5_Runtime_Identity_Audit_20260517.md`
- `docs/ParseLord5_Runtime_Smoke_And_Collision_Audit_20260517.md`
- `docs/ParseLord5_Remaining_Identity_Policy_20260517.md`
- `docs/ParseLord5_IPC_API_Compatibility_Evaluation_20260517.md`
- `docs/ParseLord5_Public_Distribution_Identity_20260517.md`
- `docs/ParseLord5_Gameplay_Experiment_WAR_20260517.md`
- `docs/ParseLord5_Gameplay_Experiment_DRG_20260517.md`
- `docs/ParseLord5_Gameplay_Experiment_SAM_20260517.md`
- `docs/ParseLord5_Config_Migration_Implementation_20260517.md`
- `docs/ParseLord5_WrathCombo_Architecture_Map.md` (updated)
- `docs/IPC.md` (updated with ParseLord5 compatibility note)

### Recommended Next Milestones

1. Live smoke test with WrathCombo config import
2. Cross-preset expansion for WAR AoE / DRG AoE
3. IPC callback rename + consumer coordination
4. Manifest/asset ownership (requires ParseLord5 repo)
5. Additional job experiments (DRK, WHM, Advanced presets)
