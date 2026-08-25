---
tags:
  - type/plan
  - project/parselord5
  - status/active
type: plan
project: parselord5
status: archived
aliases: []
---
# ParseLord5 Config Migration Plan

## Status

**Not implemented.** This is a plan-only document. No config import logic has been written. No code change has been needed yet.

## Current state (2026-05-17)

ParseLord5 uses its own isolated config file:

```
%appdata%\XIVLauncher\pluginConfigs\ParseLord5.json
```

This is derived from the manifest `InternalName` field (`"ParseLord5"`), which Dalamud uses to name plugin config files. WrathCombo uses a separate file:

```
%appdata%\XIVLauncher\pluginConfigs\WrathCombo.json
```

No collision exists. No automatic import occurs.

## Design principles for future config migration

When config migration is implemented (future milestone), it must follow these rules:

1. **ParseLord5 should use its own config by default.** The current behavior is correct and should not change.

2. **WrathCombo config import should be optional.** Users who are new to ParseLord5 and used WrathCombo before may want to bring over their settings. Users who are new to both or prefer to start fresh should not be forced.

3. **Import must require explicit user action.** Never auto-detect and auto-import. The user must click a button, type a command, or take a deliberate step.

4. **Import must create a backup.** Before importing, save a copy of the current ParseLord5 config (if any) to `ParseLord5.json.backup-{timestamp}`.

5. **Import must not delete or mutate WrathCombo config.** Read from `WrathCombo.json`, never write to it. The WrathCombo config file belongs to the WrathCombo plugin.

6. **Import should restore defaults on failure.** If the import fails (corrupt WrathCombo config, version mismatch, etc.), ParseLord5 should revert to its previous state, not a half-imported state.

## Suggested implementation approach (future)

A command like `/pl5 import` or a button in the Settings tab that:

1. Checks whether `WrathCombo.json` exists in the Dalamud plugin config directory.
2. If found, reads it and attempts to deserialize as a `Configuration` object.
3. If deserialization succeeds, merges relevant preset/rotation settings into the current ParseLord5 config.
4. Saves a backup of the current ParseLord5 config first.
5. Reports success/failure in chat.

## What should NOT be imported

- Plugin-specific settings (like UI tab preference, MOTD hide status) — these are meaningless across plugins.
- IPC lease registrations — these are runtime state, not config.
- Auto-rotation enabled/disabled state — this is a runtime toggle, not a config migration concern.

## What should be imported (candidate list)

- `EnabledActions` (HashSet of Preset) — the user's combo selections
- `RotationConfig` settings — auto-rotation preferences
- `ActiveBLUSpells` — Blue Mage spell selections
- `DancerDanceCompatActionIDs` — Dancer dance step config
- `CustomFloatValues`, `CustomIntValues`, etc. — custom user config values

## When to implement

**Defer to a future milestone.** The current architecture with an isolated config file is safe and functional. Config migration is a nice-to-have, not a requirement for the initial fork.

## Risks if implemented poorly

- Overwriting user's existing ParseLord5 config without consent
- Corrupting the WrathCombo config file
- Importing settings that are incompatible with ParseLord5's version
- Importing settings for jobs/features that have been changed in ParseLord5
- Creating confusion about which plugin's settings are in effect
