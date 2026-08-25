---
tags:
  - type/audit
  - project/parselord5
  - status/active
type: audit
project: parselord5
status: archived
aliases: []
---
# ParseLord5 Command Alias - 2026-05-17

## Files changed

- `WrathCombo/Commands.cs`
- `docs/BurstHoldingMacros.md`
- `docs/ParseLord5_Command_Alias_20260517.md`

## Existing commands before change

- `/wrath`
- `/scombo`

## Commands after change

- `/wrath`
- `/scombo`
- `/pl5`

## Confirmations

- `/wrath` remains registered.
- `/scombo` remains registered.
- `/pl5` was added as an alias.
- Command behavior was not changed.
- Alias routes to the existing command handler path used by `/wrath`.

## Build result

- `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`
- `PASS`
- `8 warnings`
- `0 errors`
