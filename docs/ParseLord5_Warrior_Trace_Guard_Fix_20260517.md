---
tags:
  - type/audit
  - project/parselord5
  - status/active
type: audit
project: parselord5
status: active
aliases: []
---
# ParseLord5 Warrior Trace Guard Fix 20260517

## Files Changed

- `WrathCombo/Combos/PvE/WAR/WAR.cs`
- `docs/ParseLord5_Warrior_Debug_Tracing.md`
- `docs/ParseLord5_Warrior_Trace_Guard_Fix_20260517.md`

## Exact Guard Conditions After the Fix

The Warrior trace now returns early unless all of these are true:

- `Service.Configuration.ParseLord5ExperimentalMode` is `true`
- `Minimal` is `true`
- `InCombat()` is `true`
- `HasBattleTarget()` is `true`
- the trace throttle has elapsed

## Guard Notes

- `Minimal` is still part of the guard.
- Combat check was added explicitly.
- Target check was added explicitly.
- Existing combat helper/pattern used: `InCombat()`
- Existing target helper/pattern used: `HasBattleTarget()`

`Minimal` already resolves to `InCombat() && HasBattleTarget()` in `WAR_Helper.cs`. It remains in the trace guard to preserve the existing WAR-local pattern, while the explicit checks make the trace requirements obvious at the call site.

## Non-Changes

- Selected actions were not changed.
- Combo decision order was not changed.
- Auto-rotation was not changed.
- Hooks were not changed.
- Detours were not changed.
- `SendAction` was not changed.
- `UseAction` was not changed.

## Build Result

`dotnet build .\WrathCombo\WrathCombo.csproj -c Release` succeeded.

- Warnings: 8
- Errors: 0
