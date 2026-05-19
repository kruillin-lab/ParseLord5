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
# ParseLord5 Gameplay Experiment — DRK — 2026-05-17

## Purpose

Fifth ParseLord5 gameplay experiment. Second Tank — tests the gating mechanism on a job with mana/blood gauge, Darkside, and Living Shadow mechanics.

## Target

**Job**: Dark Knight  
**Preset**: `DRK_ST_Simple`  
**File**: `WrathCombo/Combos/PvE/DRK/DRK.cs`

## Experiment

**Swap Spender / Cooldown_2 priority when `ParseLord5ExperimentalMode` is enabled.**

### Baseline behavior (flag off — unchanged)

1. Unmend (ranged) → Content → InCombat → Mitigation
2. Cooldown_1 (simple-mode oGCDs)
3. **Spender first** (mana spenders: Edge of Shadow, etc.)
4. **Cooldown_2 second** (blood spenders, Living Shadow, etc.)
5. Core combo → HardSlash

### Experimental behavior (flag on)

1-2 same as baseline
3. **Cooldown_2 first** (blood/oGCD cooldowns)
4. **Spender second** (mana spenders)
5. Core combo — unchanged

### Rationale

DRK's Spender category includes mana spenders (Edge of Shadow/Flood of Shadow for Darkside maintenance). Cooldown_2 includes blood spenders (Bloodspiller, Living Shadow, etc.) and other oGCDs. Swapping them changes the priority between burning mana vs. spending blood — a meaningful DRK-specific decision point.

## Gating mechanism

```csharp
if (Service.Configuration.ParseLord5ExperimentalMode)
{
    // Cooldown first, then Spender
}
else
{
    // Spender first, then Cooldown (original)
}
```

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Combos/PvE/DRK/DRK.cs` | Added `using WrathCombo.Services;` + gated Spender/Cooldown swap in both `DRK_ST_Simple` and `DRK_AoE_Simple` |

## Build result

**PASS.** 0 errors, 8 pre-existing warnings.

## Five-job experiment comparison

| | WAR | DRG | SAM | WHM | DRK |
|---|---|---|---|---|---|
| Role | Tank | Melee | Melee | Healer | **Tank** |
| Preset | ST_Simple | ST_SimpleMode | ST_SimpleMode | ST_Simple_DPS | ST_Simple |
| Change | GCD/oGCD | Buff swap | Cooldown swap | oGCD swap | **Spender/Cooldown** |
| Gates | 3 | 2 | 2 | 2 | 1 |
| Using | No | Yes | Yes | Yes | Yes |
| Ref line | 83 | 26 | 31 | 51 | 185 |

## Recommended next

- ~~DRK AoE cross-preset expansion~~ **DONE.**
- AST (Healer) or other job experiments
- Advanced preset experiments

## Cross-Preset Expansion — DRK AoE (2026-05-18)

**Preset**: `DRK_AoE_Simple`

The AoE Simple preset has a different action-category order than ST (Cooldown → Mitigation → Spender vs. Mitigation → Cooldown_1 → Spender → Cooldown_2). The experiment swaps Spender and Mitigation priority — analogous to ST's Spender swap but adapted for the AoE structure.

Gate is independent (line 276), same `if/else` pattern. No additional `using` needed (already present for ST). No cross-preset interference — both presets use `TryGetAction<T>` with different combo flags and action categories.
