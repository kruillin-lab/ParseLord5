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
# ParseLord5 Gameplay Experiment — SMN — 2026-05-18

## Purpose

12th ParseLord5 gameplay experiment. Summoner is a Caster with egi/summon priority, Aethercharge, and Demi-summons — different resource model from RDM.

## Target

**Job**: Summoner  
**Presets**: `SMN_ST_Simple_Combo` + `SMN_AoE_Simple_Combo`  
**File**: `WrathCombo/Combos/PvE/SMN/SMN.cs`

## Experiment

**Swap Ifrit / Garuda egi priority when `ParseLord5ExperimentalMode` is enabled.**

| Flag | Egi order |
|---|---|
| Off | Titan → Garuda → Ifrit (baseline) |
| On | Titan → Ifrit → Garuda (experiment) |

Garuda (Emerald) gives Slipstream/wind spells. Ifrit (Ruby) gives melee-range burst. Swapping prioritizes Ifrit's burst over Garuda's sustained damage.

## Build

**PASS.** 0 errors.

## 25 gates, 12 jobs, all ST+AoE

| WAR | DRG | SAM | WHM | DRK | AST | BRD | RDM | NIN | MCH | DNC | SMN |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 |
