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
# ParseLord5 Gameplay Experiment — SAM — 2026-05-17

## Purpose

Third ParseLord5 gameplay experiment. Prove the experimental gating mechanism works across a third job with a different rotation model (Sen system, combo chains). Follows the same pattern as WAR and DRG: one preset, one priority swap, fully gated.

## Target

**Job**: Samurai  
**Presets**: `SAM_ST_SimpleMode` (ST) + `SAM_AoE_SimpleMode` (AoE, cross-preset expansion)  
**File**: `WrathCombo/Combos/PvE/SAM/SAM.cs`

## Experiment

**Swap MeikyoShisui / Ikishoten priority when `ParseLord5ExperimentalMode` is enabled.**

### Baseline behavior (flag off — unchanged)

1. Content-specific actions
2. If can weave (oGCD section):
   - **MeikyoShisui first** (opens Sen, enabling combo freedom)
   - **Ikishoten second** (generates Kenki, enables Zanshin)
   - Remaining oGCDs (Kenki spender, Senei, Guren, Zanshin, Shoha, Shinten, etc.)
3. TsubameGaeshi → OgiNamikiri → Iaijutsu → Ranged → BasicCombo fallback

### Experimental behavior (flag on)

1. Content-specific actions — unchanged
2. If can weave (oGCD section):
   - **Ikishoten first** (generates Kenki, enables Zanshin)
   - **MeikyoShisui second** (opens Sen)
   - Remaining oGCDs — **unchanged**
3. TsubameGaeshi → OgiNamikiri → Iaijutsu → Ranged → BasicCombo fallback — **all unchanged**

### Rationale

MeikyoShisui and Ikishoten are SAM's two core cooldowns. In baseline WrathCombo, Meikyo fires first to open Sen slots. Swapping priority gives Ikishoten first, which starts Kenki generation and Zanshin readiness sooner. This changes the burst window timing in a concrete, observable way.

## Gating mechanism

```csharp
if (Service.Configuration.ParseLord5ExperimentalMode)
{
    // Ikishoten first, then Meikyo
}
else
{
    // Meikyo first, then Ikishoten (original, identical to baseline)
}
```

## How to enable/disable

1. Open ParseLord5 config (`/pl5` or `/wrath`)
2. Settings → Main UI Options → toggle `ParseLord5ExperimentalMode`

## Files changed

| File | Change |
|---|---|
| `WrathCombo/Combos/PvE/SAM/SAM.cs` | Added `using WrathCombo.Services;` + gated Meikyo/Ikishoten priority swap in `SAM_ST_SimpleMode.Invoke` + same pattern applied to `SAM_AoE_SimpleMode.Invoke` |

## Build result

**PASS.** 0 errors, 8 pre-existing warnings.

## Verification

| Check | Result |
|---|---|
| `ExperimentalMode` in `.cs` | `Configuration.cs`, `WAR.cs` (×2), `DRG.cs`, `SAM.cs` (×2) |
| Shared core files changed | None |
| Other job folders changed | None |
| Baseline preserved when flag off | Yes — exact original code in `else` branch for both presets |
| Using statement needed | Yes — `using WrathCombo.Services;` added once (both presets share it) |
| Cross-preset independence | Yes — ST and AoE gates are independent, no shared state interference |

## Three-job experiment comparison

| | WAR (1st) | DRG (2nd) | SAM (3rd) |
|---|---|---|---|
| Preset | `WAR_ST_Simple` | `DRG_ST_SimpleMode` | `SAM_ST_SimpleMode` |
| Change type | Category swap | Buff swap | Cooldown swap |
| Change | GCD/oGCD priority | LanceCharge/BattleLitany | Ikishoten/MeikyoShisui |
| Lines added | 17 | 20 | 21 |
| Using needed | No | Yes | Yes |
| Rotation model | Gauge/burst | Combo/buff | Sen/kenki |

## AoE Experiment (cross-preset expansion)

**Preset**: `SAM_AoE_SimpleMode`

Same MeikyoShisui/Ikishoten priority swap, adapted for the AoE rotation model. The AoE preset has different Meikyo/Ikishoten conditions than ST (e.g. Kenki dump logic before Ikishoten, `ComboTimer is 0` check for Meikyo). The experiment swaps only the priority order while preserving all condition checks.

The Hagakure feature (line above the swap) is left untouched — it's a separate mechanic, not a cooldown priority.

### AoE baseline (flag off)

- Hagakure → MeikyoShisui → Ikishoten (with Kenki dump) → Zanshin/Guren/Shoha/Kyuten → Ogi → Iaijutsu → combo

### AoE experimental (flag on)

- Hagakure → Ikishoten (with Kenki dump) → MeikyoShisui → Zanshin/Guren/Shoha/Kyuten → Ogi → Iaijutsu → combo

## Cross-preset independence verification

Both presets share the same file and `using WrathCombo.Services;` import but have independent `if/else` gates:

- **ST gate** at line 31 — uses `CanMeikyo()` / `CanIkishoten()` helpers
- **AoE gate** at line 143 — uses direct `ActionReady()` checks + Kenki logic

Neither preset's experiment can leak into the other through shared helpers, because each calls its own methods and returns its own actions. Both `else` branches contain exact original code.

| Flag state | ST behavior | AoE behavior |
|---|---|---|
| `false` | Original (Meikyo first) | Original (Meikyo first) |
| `true` | Ikishoten first | Ikishoten first |

## Recommended next

- Experiment on a Tank (DRK) or Healer (WHM) to test different role models
- Expand to Advanced presets (multi-preset within one job)
- Threshold-based experiment (e.g. only use cooldown above X gauge)
