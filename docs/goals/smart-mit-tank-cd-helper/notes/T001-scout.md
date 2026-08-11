---
tags:
  - type/doc
  - project/parselord5
  - status/active
type: doc
project: parselord5
status: active
aliases: []
---
# T001: Scout — tank CD helper vs smart mitigation

## Summary

**Tank cooldown helper** = `#region Auto Mitigation System` in `WAR_Helper.cs`, `PLD_Helper.cs`, `GNB_Helper.cs` (sequential, threshold/enemy-count/content gates, `mitigationRunning`, `justMitted`).

**Smart mitigation** = `WrathCombo/Services/SmartMitigation/*` + `WAR_SmartMitigation.cs` (WAR only, when `ParseLord5ExperimentalMode`). Uses `MitigationCoverageCalculator.SelectMinimumMitigation` for personal CDs; party Reprisal/Shake still bypass calculator.

## “Use all CDs” root causes

1. **Over time:** Each weave can pick another CD while threat stays high; active buffs reduce required reduction but list still offers stackable mediums.
2. **Party path:** `TrySelectSmartPartyMitigation` fires Reprisal/Shake on danger ratio without coverage math.
3. **Divergence:** Smart mit duplicates preset/content checks but omits helper bailouts (`mitigationRunning`, trash HP threshold, enemy count ≤2).
4. **TB guarantee:** `tb_guarantee` fallback can still fire a small CD after a large one on later windows.

## Integration points

| Layer | Path |
|-------|------|
| Calculator | `Services/SmartMitigation/MitigationCoverageCalculator.cs` |
| Models | `Services/SmartMitigation/MitigationModels.cs` |
| Telemetry | `CombatTelemetryService.cs` |
| WAR smart | `Combos/PvE/WAR/WAR_SmartMitigation.cs` |
| WAR helper | `Combos/PvE/WAR/WAR_Helper.cs` (lines ~522+) |
| Entry | `CanUseBossMits` / `CanUseNonBossMits` → `TrySmart*Mits` when experimental |

## Verify

```bash
dotnet build .\WrathCombo\WrathCombo.csproj -c Release
pwsh -File scripts/rotation-evals.ps1
```

## Recommended slices

1. **WAR bridge** — helper-aligned `mitigationRunning` / trash gates + filter options + party via calculator.
2. Extract shared `TankMitigationOptionBuilder` for PLD/GNB after WAR proven.
3. Scenario matrix in `notes/scenario-matrix.md` (operator in-game).
