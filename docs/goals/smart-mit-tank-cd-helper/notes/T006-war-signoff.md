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
# T006 — WAR phase sign-off

**Date:** 2026-05-31  
**Operator:** User accepted WAR slice as complete for this goal phase.

> "i think you can consider the war good and passed"

## Delivered (WAR smart mit + TCH)

| Behavior | Where documented |
| --- | --- |
| TCH IPC danger snapshot (`GetLocalDangerSnapshot`) | `notes/T005-tch-ipc.md`, `TankCooldownHelperIpcClient.cs` |
| Telemetry fallback when IPC missing | T005 note; `WrathCombo.cs` always updates `CombatTelemetryService` in experimental mode |
| Selective CDs via bridge + coverage calculator | T003 receipt; `WAR_MitigationBridge.cs`, `WAR_SmartMitigation.cs` |
| Damnation only on strict TB telegraph or TCH Emergency + HP &lt; 50% | T005 note; `ShouldOfferDamnation*` |
| 60s long-CD overlap rules (short may stack) | T005 note; `WarLongMitigationRecastSeconds` |
| Thrill / Equilibrium excluded from smart mit (heals only) | T005 note; still in `WAR_Helper` rotation/heal paths |
| All smart mit blocked during Bloodwhetting defense buff | T005 note; `IsWarBloodwhettingDefenseActive()` |
| Trash Reprisal first (≥3 enemies); overlaps long CDs | `TrashMitigationOrdering`, `TrySelectWarTrashReprisalFirst` |
| Long-long exclusion (Holmgang / Shake / Vengeance-Damnation) | `IsWarLongMitigationActive` + filter/fallbacks; Reprisal/Rampart short |

## Verification

- `dotnet build .\WrathCombo\WrathCombo.csproj -c Release` — pass (2026-05-31)
- In-game oracle (full scenario matrix): optional; operator sign-off substitutes for WAR tranche

## Remaining (goal not closed)

- **PLD / GNB** — bridge pattern port (`state.yaml` T005); deferred until requested
- **T999** — final audit vs full goal oracle when multi-tank scope decided or goal narrowed to WAR-only
- **Legacy helper path** — non-experimental `WAR_Helper` sequential mit unchanged
- **Other tanks** (DRK, etc.) — out of current tranche

## Related notes

- Scout: `T001-scout.md`
- Bridge slice: T003 receipt in `state.yaml`
- IPC + mit rules: `T005-tch-ipc.md`
- Scenarios (reference): `scenario-matrix.md`
