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
# ParseLord5 Gameplay Experiment — DNC — 2026-05-18

## Purpose

11th ParseLord5 gameplay experiment. Dancer is a Ranged DPS with dance steps, Feathers, Devilment/Flourish buffs, and fan procs — unique resource model.

## Target

**Job**: Dancer  
**Presets**: `DNC_ST_SimpleMode` + `DNC_AoE_SimpleMode`  
**File**: `WrathCombo/Combos/PvE/DNC/DNC.cs`

## Experiment

**Swap Flourish / Devilment priority when `ParseLord5ExperimentalMode` is enabled.**

| Flag | Priority |
|---|---|
| Off | Devilment → Flourish (baseline) |
| On | Flourish → Devilment (experiment) |

Devilment increases critical hit and direct hit rates. Flourish grants procs (Flourishing Flow/Symmetry, Fan Dances). Swapping prioritizes procs over the crit buff — a meaningful Dancer-specific decision.

## Build

**PASS.** 0 errors.

## 22 gates, 11 jobs

| WAR | DRG | SAM | WHM | DRK | AST | BRD | RDM | NIN | MCH | DNC |
|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 2 | 2 | 2 | 2 | 2 | 2 | 1 | 2 | 2 | 2 |
