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
# ParseLord5 Gameplay Experiment — WHM — 2026-05-17

## Purpose

Fourth ParseLord5 gameplay experiment. First Healer job — tests the gating mechanism on a role with different rotation priorities (heal/DPS switching, oGCD cooldown management).

## Target

**Job**: White Mage  
**Preset**: `WHM_ST_Simple_DPS`  
**File**: `WrathCombo/Combos/PvE/WHM/WHM.cs`

## Experiment

**Swap Assize / PresenceOfMind priority when `ParseLord5ExperimentalMode` is enabled.**

### Baseline behavior (flag off — unchanged)

1. Content-specific actions
2. If can weave:
   - **PresenceOfMind first** (speed buff, 3+ GCDs used)
   - **Assize second** (damage/heal oGCD)
   - LucidDreaming (low MP)

### Experimental behavior (flag on)

1. Content-specific actions — unchanged
2. If can weave:
   - **Assize first** (immediate damage/heal)
   - **PresenceOfMind second** (speed buff)
   - LucidDreaming — unchanged

### Rationale

Assize is WHM's highest-priority oGCD — it does damage, healing, and MP restoration simultaneously. In baseline WrathCombo, PresenceOfMind (GCD speed buff) fires before Assize. Swapping them means Assize gets used immediately when available, potentially getting more total casts over a fight.

## Gating mechanism

```csharp
if (Service.Configuration.ParseLord5ExperimentalMode)
{
    // Assize first, then PresenceOfMind
}
else
{
    // PresenceOfMind first, then Assize (original)
}
```

## How to enable/disable

Settings → Main UI Options → `ParseLord5ExperimentalMode`

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Combos/PvE/WHM/WHM.cs` | Added `using WrathCombo.Services;` + gated Assize/PoM swap in both `WHM_ST_Simple_DPS` and `WHM_AoE_Simple_DPS` |

## Build result

**PASS.** 0 errors, 8 pre-existing warnings.

## Four-job experiment comparison

| | WAR | DRG | SAM | WHM |
|---|---|---|---|---|
| Role | Tank | Melee | Melee | **Healer** |
| Preset | ST_Simple | ST_SimpleMode | ST_SimpleMode | ST_Simple_DPS |
| Change | GCD/oGCD | Buff swap | Cooldown swap | **oGCD priority** |
| Lines added | 17 | 20 | 21 | 23 |
| Using needed | No | Yes | Yes | Yes |

## Recommended next

- ~~WHM AoE cross-preset expansion~~ **DONE.**
- DRK (Tank) or AST (Healer) experiment
- Advanced preset experiments

## Cross-Preset Expansion — WHM AoE (2026-05-18)

**Preset**: `WHM_AoE_Simple_DPS`

Same Assize/PresenceOfMind priority swap applied to the AoE Simple DPS preset. Interestingly, the AoE baseline has Assize BEFORE PresenceOfMind (opposite of ST), so the experiment reverses the order: experimental AoE puts PoM first. Both presets share the same principle — "reverse the priority order" — but from opposite baseline directions.

Gate is independent (line 128), same `if/else` pattern. No additional `using` needed (already present for ST). No cross-preset interference — both presets have their own `CanWeave()` calls with different conditions.
