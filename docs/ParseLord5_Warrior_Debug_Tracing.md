---
tags:
  - type/note
  - project/parselord5
  - status/active
type: note
project: parselord5
status: active
aliases: []
---
# ParseLord5 Warrior Debug Tracing

Created: 2026-05-17

## Trace Point

The first ParseLord5 trace point is `WAR_ST_Simple.Invoke` in `WrathCombo/Combos/PvE/WAR/WAR.cs`.

It runs after the pressed action has been confirmed as `HeavySwing` and after Warrior simple single-target code has selected the action it would return. It does not change `actionID`, helper ordering, combo decisions, target selection, auto-rotation, commands, hooks, detours, `SendAction`, or `UseAction`.

## Guard

Logging requires all of these:

- `Service.Configuration.ParseLord5ExperimentalMode` is `true`.
- The WAR-local `Minimal` guard passes.
- Player is in combat.
- Player has a battle target.
- Trace throttle has elapsed.

In source, `Minimal` is still part of the guard, and the trace also explicitly checks `InCombat()` and `HasBattleTarget()`.

The throttle is 15 seconds, stored in `ParseLord5WarTraceThrottleMs`.

## Log Payload

The debug line includes:

- Input action name and ID.
- Selected replacement action name and ID.
- Selection source: `content`, `mitigation`, `ogcd`, `gcd`, or `fallback`.
- Beast gauge.
- Surging Tempest presence.
- Inner Release stack count.
- Nascent Chaos presence.
- Wrathful presence.

## Risk

Risk is low because the trace is read-only, default-off, and records the already-selected return value without changing it. Remaining risk is log noise if experimental mode is enabled during combat, capped by the 15-second throttle.

Because auto-rotation can also invoke enabled combos, this trace may appear during auto-rotation if Warrior single-target simple mode is active. It still does not touch auto-rotation control flow or execution.
