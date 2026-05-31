---
tags:
  - type/doc
  - project/parselord5
  - status/active
type: doc
project: parselord5
status: active
aliases: []
---
# Dynamic Healing Responsiveness Design Spec

**Date**: 2026-05-20
**Status**: APPROVED

## Problem Statement
WrathCombo's healer auto-rotation is perceived as slow and unresponsive compared to competing frameworks like RotationSoloReborn (RSR). This sluggishness stems from two primary architectural issues:
1. A hardcoded caching interval of `2000ms` on party member lists in `Party.cs`, which limits the freshness of healing target data.
2. A fixed, artificial reaction delay (`cfg.HealerSettings.HealDelay`, default 1.0s) in `AutoRotationController.cs` meant to simulate human play but fatal in high-intensity combat.

## Proposed Solution
Introduce a dynamic responsiveness scaling system active strictly when `ParseLord5ExperimentalMode` is enabled.

### 1. Dynamic Party Cache Updates
In `Party.cs`, reduce the cache refresh throttle from `2000ms` to `100ms` when the player or party is in combat and `ParseLord5ExperimentalMode` is active.

### 2. Linear Dynamic Reaction Delay
In `AutoRotationController.cs`, dynamically compute the reaction delay (`effectiveHealDelay`) based on the lowest HP percentage among active heal targets:
- **HP <= 35%**: `0.0` seconds delay (instantaneous execution).
- **HP >= 75%**: Full `HealDelay` (natural human speed for minor heals).
- **35% < HP < 75%**: Scaled linearly (e.g., at `55%` HP, delay is `0.5 * HealDelay`).

## Affected Files
- `WrathCombo/CustomCombo/Functions/Party.cs`
- `WrathCombo/AutoRotation/AutoRotationController.cs`

## Verification Plan
- **Verification Command**: `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`
- **Manual Verification**: Run plugin in-game, toggle `ParseLord5ExperimentalMode`, verify healers react instantaneously to major damage while retaining a natural pacing for high-health players.
