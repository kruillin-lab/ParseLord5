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
# ParseLord5 Gameplay Experiment — WAR — 2026-05-17

## Purpose

First ParseLord5 gameplay experiment. Prove the fork can safely modify rotation behavior without affecting the WrathCombo baseline, using the `ParseLord5ExperimentalMode` flag as the sole gate.

## Target

**Job**: Warrior  
**Preset**: `WAR_ST_Simple`  
**File**: `WrathCombo/Combos/PvE/WAR/WAR.cs`

## Experiment

**Swap GCD/oGCD priority when `ParseLord5ExperimentalMode` is enabled.**

### Baseline behavior (flag off — unchanged)

1. Mitigation check (`TryUseMits`)
2. oGCD attacks first (`TryOGCDAttacks`)
3. GCD attacks second (`TryGCDAttacks`)
4. Fallback to `STCombo`

### Experimental behavior (flag on)

1. Mitigation check (`TryUseMits`) — unchanged
2. **GCD attacks first** (`TryGCDAttacks`)
3. **oGCD attacks second** (`TryOGCDAttacks`)
4. Fallback to `STCombo` — unchanged

### Rationale

The baseline WrathCombo behavior prioritizes oGCD weaving before GCD combo progression. Under experimental mode, GCD combo progression takes priority. This is a concrete, observable rotation change that tests the gating mechanism without modifying any shared core files.

## Gating mechanism

```csharp
if (Service.Configuration.ParseLord5ExperimentalMode)
{
    // GCD-first experimental path
}
else
{
    // Original oGCD-first path (identical to baseline)
}
```

When `ParseLord5ExperimentalMode` is `false` (default), the exact original code executes — no behavioral change.

## How to enable/disable

1. Open the ParseLord5 config window (`/pl5` or `/wrath`)
2. Go to Settings → Main UI Options
3. Toggle `ParseLord5ExperimentalMode` on/off
4. The change takes effect immediately on the next `HeavySwing` press

## Build result

**PASS.** 0 errors, 8 pre-existing warnings.

## Verification

| Check | Result |
|---|---|
| `ExperimentalMode` references in `.cs` | Only `WrathCombo/Combos/PvE/WAR/WAR.cs` (lines 28, 83) and `WrathCombo/Core/Configuration.cs` (line 50) |
| Shared core files changed | None |
| Other job folders changed | None |
| `ActionReplacer` / `AutoRotationController` / `AutoRotationHelper` touched | No |
| Baseline behavior preserved when flag off | Yes — exact original code in `else` branch |
| Flag default value | `false` |

## Known expectations and caveats

- This is a **priority swap**, not a removal. oGCDs still fire in experimental mode — they just fire after GCDs instead of before.
- The trace source labels differ (`"gcd-exp"` / `"ogcd-exp"` vs `"gcd"` / `"ogcd"`) to make debug output distinguishable.
- This experiment does NOT change `WAR_AoE_Simple` or any other preset — the flag gate exists only in `WAR_ST_Simple.Invoke`.
- Live testing should compare GCD timing and oGCD weave windows between flag-on and flag-off states.

## Recommended next experiment

Any of:
- Apply the same GCD/oGCD swap to `WAR_AoE_Simple` for consistency
- A condition-based experiment (e.g., only use oGCDs above a Beast Gauge threshold)
- Experiment on a second job (DRG or DRK, following the architecture map suggestions)
