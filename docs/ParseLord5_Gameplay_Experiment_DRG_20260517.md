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
# ParseLord5 Gameplay Experiment — DRG — 2026-05-17

## Purpose

Second ParseLord5 gameplay experiment. Prove the experimental gating mechanism works across a second job, following the architecture map's recommendation of DRG as a comparison target after WAR.

## Target

**Job**: Dragoon  
**Preset**: `DRG_ST_SimpleMode`  
**File**: `WrathCombo/Combos/PvE/DRG/DRG.cs`

## Experiment

**Swap LanceCharge / BattleLitany priority when `ParseLord5ExperimentalMode` is enabled.**

### Baseline behavior (flag off — unchanged)

1. Content-specific actions
2. If can weave: BattleLitany first, then LanceCharge
3. Remaining oGCDs (LifeSurge, MirageDive, Geirskogul, Wyrmwind, Starcross, etc.)
4. Jump/Dragonfire/Stardiver
5. BasicCombo fallback

### Experimental behavior (flag on)

1. Content-specific actions — unchanged
2. If can weave: **LanceCharge first**, then BattleLitany
3. Remaining oGCDs — **unchanged**
4. Jump/Dragonfire/Stardiver — unchanged
5. BasicCombo fallback — unchanged

### Rationale

LanceCharge and BattleLitany are both party-wide damage buffs. In baseline WrathCombo, BattleLitany (crit buff) fires before LanceCharge (damage buff). Swapping them changes the buff window timing — LanceCharge's shorter cooldown gets priority, potentially allowing more total uses over a fight. This is a concrete, observable rotation change.

## Gating mechanism

```csharp
if (Service.Configuration.ParseLord5ExperimentalMode)
{
    // LanceCharge first, then BattleLitany
}
else
{
    // BattleLitany first, then LanceCharge (original, identical to baseline)
}
```

When `ParseLord5ExperimentalMode` is `false` (default), the exact original code executes — no behavioral change.

## How to enable/disable

1. Open ParseLord5 config (`/pl5` or `/wrath`)
2. Settings → Main UI Options
3. Toggle `ParseLord5ExperimentalMode`

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Combos/PvE/DRG/DRG.cs` | Added `using WrathCombo.Services;` (line 2). Added gated LanceCharge/BattleLitany priority swap in `DRG_ST_SimpleMode.Invoke` (lines 24-41) |

## Build result

**PASS.** 0 errors, 8 pre-existing warnings.

## Verification

| Check | Result |
|---|---|
| `ExperimentalMode` in `.cs` | `Configuration.cs`, `WAR.cs`, `DRG.cs` only |
| Shared core files changed | None |
| Other job folders changed | None |
| Baseline preserved when flag off | Yes — exact original code in `else` branch |
| Flag default | `false` |

## Experiment comparison

| Aspect | WAR (1st) | DRG (2nd) |
|---|---|---|
| Preset | `WAR_ST_Simple` | `DRG_ST_SimpleMode` |
| Change | GCD/oGCD priority swap | Buff priority swap (LanceCharge/BattleLitany) |
| Scope | Broad — all GCDs vs all oGCDs | Targeted — two specific buffs |
| Lines added | 17 | 20 (incl. using statement) |
| Required code changes | Only Invoke block | Invoke block + using statement |

## Recommended next experiment

- ~~Apply same buff-priority concept to DRG Advanced mode~~
- Experiment on a fourth job (DRK or WHM per architecture map)
- Add a threshold-based condition (e.g., only use an oGCD above certain HP%)

## Cross-Preset Expansion — DRG AoE (2026-05-18)

**Preset**: `DRG_AoE_SimpleMode`

Same LanceCharge/BattleLitany priority swap applied to the AoE Simple preset. Identical `if/else` gate pattern, independent at line 140. No additional `using` needed (already present for ST experiment). No cross-preset interference — both presets call their own `CanDRGWeave()` but with different conditions and entry points.
