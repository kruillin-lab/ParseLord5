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
# ParseLord5 Docs Consistency Audit 20260517

## Files Reviewed

- `docs/HealerSettings.md`
- `docs/BurstHoldingMacros.md`
- `docs/AutoRotation.md`
- `WrathCombo/Commands.cs`

## Files Changed

- `docs/HealerSettings.md`
- `docs/BurstHoldingMacros.md`
- `docs/AutoRotation.md`
- `docs/ParseLord5_Docs_Consistency_Audit_20260517.md`

## Command Source Check

- `/wrath` exists in `WrathCombo/Commands.cs`.
- `/scombo` exists as an old alias.
- `/pl5` was not found in current command source.

## Change Summary

- Command text was changed only in explanatory documentation.
- `/wrath` macro examples were preserved.
- No `/pl5` examples were added because `/pl5` is not registered in source.
- Macro preset IDs were not changed.
- Auto Rotation wording was changed for grammar, capitalization, and ParseLord5/WrathCombo clarity.
- Healing Settings wording was changed for grammar, capitalization, and clarity.
- Heal priorities, thresholds, retargeting, Heal Stack, and Raise Stack meanings were preserved.

## Commands Run

```powershell
git grep -n -F "/pl5" -- WrathCombo docs
Select-String -Path "C:\Users\kruil\Documents\Projects\ParseLord5\WrathCombo\Commands.cs" -Pattern "/wrath|/pl5|scombo|CommandInfo|RegisterCommand" -Context 1,2
git diff -- docs/HealerSettings.md docs/BurstHoldingMacros.md docs/AutoRotation.md
dotnet build .\WrathCombo\WrathCombo.csproj -c Release
```

## Build Result

`dotnet build .\WrathCombo\WrathCombo.csproj -c Release` succeeded.

- Warnings: 0
- Errors: 0

## Remaining Risks

- Macro IDs were not individually revalidated against every preset enum entry because this was a docs-only consistency cleanup, not a preset audit.
- `/pl5` may be added later, but it does not exist in current command source.
- Existing non-target dirty files were not reviewed or changed as part of this cleanup.
