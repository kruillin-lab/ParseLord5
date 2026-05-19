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
# ParseLord5 Gameplay Experiment — BLM — 2026-05-19

## Purpose

13th ParseLord5 gameplay experiment. Black Mage completes the caster DPS set after RDM and SMN. Changes are limited to oGCD utility weave priority in simple presets — no Fire/Ice phase or elemental loop changes.

## Target

**Job**: Black Mage  
**Presets**: `BLM_ST_SimpleMode` + `BLM_AoE_SimpleMode`  
**File**: `WrathCombo/Combos/PvE/BLM/BLM.cs`

## Experiment

**Swap Amplifier / Ley Lines weave priority when `ParseLord5ExperimentalMode` is enabled.**

| Flag | Weave order (ST + AoE simple) |
|---|---|
| Off | Amplifier → Ley Lines (baseline) |
| On | Ley Lines → Amplifier (experiment) |

Ley Lines grants cast-time reduction in a stationary window. Amplifier restores Polyglot stacks. Swapping prioritizes placing Ley Lines before spending Polyglot via Amplifier when both are ready in the same weave window.

## Scope

- ST: `CanWeave()` block at top of `BLM_ST_SimpleMode` only
- AoE: matching `CanWeave()` block in `BLM_AoE_SimpleMode` only
- No shared core files touched
- Default behavior unchanged when flag is `false`

## Build

**PASS.** 0 errors.

## 27 gates, 13 jobs, all ST+AoE

| WAR | DRG | SAM | WHM | DRK | AST | BRD | RDM | NIN | MCH | DNC | SMN | BLM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 |
