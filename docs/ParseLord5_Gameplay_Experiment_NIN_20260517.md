---
tags:
  - type/experiment
  - project/parselord5
  - status/active
type: experiment
project: parselord5
status: active
aliases: []
---
# ParseLord5 Gameplay Experiment — NIN — 2026-05-17

## Purpose

Ninth ParseLord5 gameplay experiment. Tests the gating mechanism on Ninja, which has a unique mudra/ninjutsu system not present in any other job.

## Target

**Job**: Ninja  
**Presets**: `NIN_ST_SimpleMode` + `NIN_AoE_SimpleMode` (cross-preset)  
**File**: `WrathCombo/Combos/PvE/NIN/NIN.cs`

## Experiment

**Swap TrickAttack / Mug priority when `ParseLord5ExperimentalMode` is enabled.**

| Flag | oGCD order |
|---|---|
| Off | Mug → TrickAttack (baseline) |
| On | TrickAttack → Mug (experiment) |

Mug increases Ninki gauge; TrickAttack applies a damage vulnerability debuff. Swapping prioritizes the debuff over gauge generation.

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Combos/PvE/NIN/NIN.cs` | Added `using WrathCombo.Services;` + gated TrickAttack/Mug swap in both ST and AoE presets |

## Build result

**PASS.** 0 errors.

## 18 gates, 9 jobs

| WAR | DRG | SAM | WHM | DRK | AST | BRD | RDM | NIN |
|---|---|---|---|---|---|---|---|---|
| 3 | 2 | 2 | 2 | 2 | 2 | 2 | 1 | 2 |

Jobs fully covered (ST+AoE): 9

## Recommended next

- Commit consolidation
- 10th job experiment
