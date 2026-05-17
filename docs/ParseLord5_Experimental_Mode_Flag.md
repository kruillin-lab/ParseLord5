---
tags:
  - type/note
  - project/parselord5
  - status/active
type: note
project: parselord5
status: active
aliases: []
---
# ParseLord5 Experimental Mode Flag

Created: 2026-05-17

`ParseLord5ExperimentalMode` lives in `WrathCombo/Core/Configuration.cs`.

The flag is default-off and exposed through WrathCombo's existing reflected Settings UI. It is not consumed by rotations, auto-rotation, command handling, hooks, detours, `SendAction`, `UseAction`, or job combo logic.

Future ParseLord5 work should use this flag as a guard for isolated experiments, starting with one selected job or debug tracing path. Existing WrathCombo behavior should remain unchanged while the flag is off.

First guarded use: Warrior single-target simple trace in `WAR_ST_Simple.Invoke`. It logs a throttled state snapshot and selected replacement result only when `ParseLord5ExperimentalMode` is enabled.
