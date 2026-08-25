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
# ParseLord5 Gameplay Experiment — AST — 2026-05-17

## Purpose

Sixth ParseLord5 gameplay experiment. Second Healer — tests the gating mechanism on Astrologian, which has card drawing, crown cards, sects, and buff distribution mechanics fundamentally different from WHM's direct healing/DPS model.

## Target

**Job**: Astrologian  
**Preset**: `AST_ST_Simple_DPS`  
**File**: `WrathCombo/Combos/PvE/AST/AST.cs`

## Experiment

**Swap Divination / Earthly Star priority when `ParseLord5ExperimentalMode` is enabled.**

### Baseline behavior (flag off — unchanged)

1. Lightspeed (if moving) → LucidDreaming → Play Card → Minor Arcana → Card Draw
2. **Divination first** (party buff)
3. **Earthly Star second** (ground-targeted heal/damage AoE)
4. Oracle → GCDs → Malefic

### Experimental behavior (flag on)

1. Card sequence — unchanged
2. **Earthly Star first** (ground AoE)
3. **Divination second** (party buff)
4. Oracle → GCDs — unchanged

### Rationale

Divination (party damage buff) and Earthly Star (placed ground AoE that detonates after 10s) are AST's two highest-impact cooldowns. In baseline, Divination fires first to start the buff window. Swapping them places Earthly Star first, which means the star starts ticking while Divination is used — potentially aligning the star detonation better with the buff window. This is analogous to the DRG buff-priority swap and WHM oGCD-priority swap.

## Gating mechanism

```csharp
if (Service.Configuration.ParseLord5ExperimentalMode)
{
    // Earthly Star first, then Divination
}
else
{
    // Divination first, then Earthly Star (original)
}
```

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Combos/PvE/AST/AST.cs` | Added `using WrathCombo.Services;` + gated Divination/Earthly Star swap in both `AST_ST_Simple_DPS` and `AST_AOE_Simple_DPS` |

## Build result

**PASS.** 0 errors, 8 pre-existing warnings.

## Six-job experiment comparison

| | WAR | DRG | SAM | WHM | DRK | AST |
|---|---|---|---|---|---|---|
| Role | Tank | Melee | Melee | Healer | Tank | **Healer** |
| Preset | ST_Simple | ST_SimpleMode | ST_SimpleMode | ST_Simple_DPS | ST_Simple | ST_Simple_DPS |
| Change | GCD/oGCD | Buff | Cooldown | oGCD | Spender/Cooldown | **Big CD** |
| Gates | 3 | 2 | 2 | 2 | 2 | 1 |
| Using | No | Yes | Yes | Yes | Yes | Yes |

## Recommended next

- ~~AST AoE cross-preset expansion~~ **DONE.**
- Advanced preset experiments
- Commit consolidation

## Cross-Preset Expansion — AST AoE (2026-05-18)

**Preset**: `AST_AOE_Simple_DPS`

Same Divination/Earthly Star priority swap applied to the AoE Simple DPS preset. Identical `if/else` gate pattern, independent at line 164. No additional `using` needed (already present for ST). No cross-preset interference — both presets have independent weave sections with their own conditions.
