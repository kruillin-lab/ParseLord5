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
# ParseLord5 Gameplay Experiment — RDM — 2026-05-17

## Purpose

Eighth ParseLord5 gameplay experiment. First Caster — tests the gating mechanism on Red Mage, which has dualcast mechanics, mana gauge, melee combo, and enchanted spells fundamentally different from melee, ranged, and healers.

## Target

**Job**: Red Mage  
**Preset**: `RDM_ST_SimpleMode`  
**File**: `WrathCombo/Combos/PvE/RDM/RDM.cs`

## Experiment

**Swap Fleche / Contre Sixte priority when `ParseLord5ExperimentalMode` is enabled.**

### Baseline (flag off)

- oGCD order: Manafication → Embolden → **Contre Sixte → Fleche** → rest
- Contre Sixte (AoE-capable oGCD) fires first

### Experimental (flag on)

- oGCD order: Manafication → Embolden → **Fleche → Contre Sixte** → rest
- Fleche (single-target oGCD) fires first

### Rationale

Contre Sixte and Fleche are RDM's two primary oGCD damage abilities. Contre Sixte is AoE-capable (higher potency in multi-target) while Fleche is single-target. Swapping them prioritizes Fleche's single-target damage in all scenarios — a concrete caster-specific decision point.

## Gating mechanism

```csharp
if (Service.Configuration.ParseLord5ExperimentalMode)
    // Fleche first, then Contre Sixte
else
    // Contre Sixte first, then Fleche (original)
```

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Combos/PvE/RDM/RDM.cs` | Added `using WrathCombo.Services;` + gated Fleche/Contre Sixte swap in `RDM_ST_SimpleMode.Invoke` |

## Build result

**PASS.** 0 errors.

## Eight-job comparison

| | WAR | DRG | SAM | WHM | DRK | AST | BRD | RDM |
|---|---|---|---|---|---|---|---|---|
| Role | Tank | Melee | Melee | Healer | Tank | Healer | Ranged | **Caster** |
| Change | GCD/oGCD | Buff | Cooldown | oGCD | Spender | Big CD | Song | **oGCD** |
| Gates | 3 | 2 | 2 | 2 | 2 | 2 | 2 | 1 |

## Recommended next

- ~~RDM AoE cross-preset expansion~~ **DONE (2026-05-18).** AoE gate added — same Fleche/Contre Sixte swap.
- Commit consolidation

## Cross-Preset Expansion — RDM AoE

**Preset**: `RDM_AoE_SimpleMode`. Same Fleche/Contre Sixte swap. Independent gate, same `if/else` pattern.

## 23 gates, 11 jobs, all ST+AoE

| WAR | DRG | SAM | WHM | DRK | AST | BRD | RDM | NIN | MCH | DNC |
|---|---|---|---|---|---|---|---|---|---|---|
| 3 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 |
