---
tags:
  - type/implementation
  - project/parselord5
  - status/active
type: implementation
project: parselord5
status: archived
aliases: []
---
# ParseLord5 Config Migration Implementation — 2026-05-17

## Purpose

Allow users migrating from WrathCombo to explicitly import compatible settings into ParseLord5. Read-only on the source WrathCombo config, additive on the destination.

## Trigger

Settings tab → "Import settings from WrathCombo" button → confirmation dialog → import.

Button is hidden after a successful import (timestamp shown instead).

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Core/ConfigurationHelper.cs` | Added `ImportFromWrathCombo()` method + `HasImportedFromWrathCombo` / `LastWrathComboImportTime` fields. Added `using System.IO`. |
| `WrathCombo/Window/Tabs/Settings.cs` | Added import button, confirmation popup, and status message in Settings Draw(). Added `_showImportConfirm`, `_importMessage`, `_importMessageTime` fields. |

## Fields imported

| ParseLord5 field | Source | Notes |
|---|---|---|
| `EnabledActions` | WrathCombo `EnabledActions` | Replaces existing (Clear + Add) |
| `RotationConfig` | WrathCombo `RotationConfig` | Direct assignment |
| `IgnoredNPCs` | WrathCombo `IgnoredNPCs` | Clears then copies |
| `ActiveBLUSpells` | WrathCombo `ActiveBLUSpells` | Direct assignment |
| `DancerDanceCompatActionIDs` | WrathCombo same field | Direct assignment |
| `StatusBlacklist` | WrathCombo `StatusBlacklist` | Clears then copies |

## Fields excluded (intentionally)

| Field | Reason |
|---|---|
| `ParseLord5ExperimentalMode` | Must stay `false` — user opts into experiments separately |
| `Version` | ParseLord5 uses its own version |
| `AprilFools2026` | Plugin-specific |
| `CustomFloatValues` / `CustomIntValues` / etc. | Static fields not deserializable from external JSON without raw parsing |
| UI-only settings (HideChildren, ShowTargetHighlight, etc.) | Cosmetic preferences, not gameplay |

## Behavior

- **WrathCombo config exists**: reads, deserializes, copies compatible fields, saves ParseLord5 config
- **WrathCombo config missing**: shows info message, returns false
- **Deserialization fails**: logs warning, returns false
- **Any exception**: logs error, returns false
- **WrathCombo source file**: never modified or deleted
- **Experiment flag**: stays `false` regardless of source config
- **Re-import**: button hidden after first import; user can re-delete ParseLord5.json and reload if needed

## Build result

**PASS.** 0 errors, 8 pre-existing warnings.

## Rollback

Delete `%appdata%\XIVLauncher\pluginConfigs\ParseLord5.json` and reload ParseLord5. Dalamud regenerates a default config.

## Recommended next

- Live smoke test the import flow
- Expand to WAR AoE / DRG AoE cross-preset experiments
- IPC callback rename + consumer coordination
