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
# ParseLord5 Gameplay Experiment — BRD — 2026-05-17

## Purpose

Seventh ParseLord5 gameplay experiment. First Ranged DPS — tests the gating mechanism on Bard, which has song cycles, proc-based abilities, and DoT management fundamentally different from melee combos and healer priorities.

## Target

**Job**: Bard  
**Preset**: `BRD_ST_SimpleMode`  
**File**: `WrathCombo/Combos/PvE/BRD/BRD.cs`

## Experiment

**Swap Wanderer's Minuet / Mage's Ballad priority when `ParseLord5ExperimentalMode` is enabled.**

### Baseline (flag off)

- Song cycle: Wanderer's Minuet → Mage's Ballad → Army's Paeon
- Wanderer's (Pitch Perfect procs) fires first

### Experimental (flag on)

- Song cycle: **Mage's Ballad → Wanderer's Minuet** → Army's Paeon
- Mage's (Bloodletter procs) fires first

### Rationale

Wanderer's Minuet and Mage's Ballad are BRD's two primary song stances. Wanderer's gives Pitch Perfect stacks (burst damage), Mage's gives Bloodletter resets (sustained damage). Swapping them prioritizes Bloodletter procs over Pitch Perfect buildup — a concrete song-cycle decision point.

## Gating mechanism

```csharp
if (Service.Configuration.ParseLord5ExperimentalMode)
{
    // Mage's first, then Wanderer's
}
else
{
    // Wanderer's first, then Mage's (original)
}
```

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Combos/PvE/BRD/BRD.cs` | Added `using WrathCombo.Services;` + gated Wanderer's/Mage's song swap in both `BRD_ST_SimpleMode` and `BRD_AoE_SimpleMode` |

## Build result

**PASS.** 0 errors.

## Seven-job comparison

| | WAR | DRG | SAM | WHM | DRK | AST | BRD |
|---|---|---|---|---|---|---|---|
| Role | Tank | Melee | Melee | Healer | Tank | Healer | **Ranged** |
| Change | GCD/oGCD | Buff | Cooldown | oGCD | Spender | Big CD | **Song** |
| Gates | 3 | 2 | 2 | 2 | 2 | 2 | 1 |

## Recommended next

- ~~BRD AoE cross-preset expansion~~ **DONE.**
- Commit consolidation
- Advanced preset experiments

## Cross-Preset Expansion — BRD AoE (2026-05-18)

**Preset**: `BRD_AoE_SimpleMode`

Same Wanderer's Minuet/Mage's Ballad song swap applied to the AoE Simple preset. Identical `if/else` gate pattern, independent at line 36. No additional `using` needed. No cross-preset interference — both presets have independent song sections.
