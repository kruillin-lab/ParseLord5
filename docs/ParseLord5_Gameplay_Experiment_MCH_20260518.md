---
tags:
  - type/experiment
  - project/parselord5
  - status/active
type: experiment
project: parselord5
status: archived
aliases: []
---
# ParseLord5 Gameplay Experiment — MCH — 2026-05-18

## Purpose

10th ParseLord5 gameplay experiment. Machinist is a Ranged DPS with heat gauge, Hypercharge windows, Queen automaton, and tool combos — different resource model from BRD and other ranged.

## Target

**Job**: Machinist  
**Presets**: `MCH_ST_SimpleMode` + `MCH_AoE_SimpleMode`  
**File**: `WrathCombo/Combos/PvE/MCH/MCH.cs`

## Experiment

**Swap Reassemble / Queen priority when `ParseLord5ExperimentalMode` is enabled.**

| Flag | Priority |
|---|---|
| Off | Queen → Reassemble (baseline) |
| On | Reassemble → Queen (experiment) |

Reassemble guarantees a critical hit on the next weaponskill. Queen (Rook Autoturret) summons the automaton for sustained damage. Swapping prioritizes the critical-hit setup over the automaton summon. Both ST and AoE presets gated independently.

## Build

**PASS.** 0 errors.

## 20 gates, 10 jobs

| WAR | DRG | SAM | WHM | DRK | AST | BRD | RDM | NIN | MCH |
|---|---|---|---|---|---|---|---|---|---|
| 3 | 2 | 2 | 2 | 2 | 2 | 2 | 1 | 2 | 2 |
