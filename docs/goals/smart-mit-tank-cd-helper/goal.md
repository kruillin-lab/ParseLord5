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
# Smart mitigation + tank cooldown helper integration

## Objective

Integrate information from the **tank cooldown helper** into ParseLord5’s **smart mitigation system** so mitigation chooses cooldowns intelligently (predictive / selective) instead of firing **all** available tank cooldowns when mitigation triggers.

## Original Request

Integrate the information from tank cooldown helper into the smart mitigation system in PL5 so that it can more intelligently predict which cooldowns to use without just using all cooldowns.

## Intake Summary

- Input shape: `specific`
- Audience: ParseLord5 users running tank jobs with smart mitigation enabled
- Authority: `requested`
- Proof type: `test` + `demo` (compile, domain evals, documented in-game or log behavior)
- Completion proof: Smart mitigation selects a **subset** of appropriate tank CDs per event using helper data (potency, timing, overlap rules); regression tests or evals pass; no “use every CD” fallback except explicit debug/off mode
- Goal oracle:
  1. `dotnet build .\WrathCombo\WrathCombo.csproj -c Release` passes
  2. `scripts/rotation-evals.ps1` passes (if applicable to touched presets)
  3. Documented scenario matrix shows selective CD use (e.g. heavy hit → strongest appropriate CD; filler window → none or one CD; not full bar dump)
  4. Tank cooldown helper data is **read/consumed** by smart mitigation (single source of truth or explicit adapter — no duplicated stale tables)
- Likely misfire: Wiring helper files without changing decision logic (still casts all CDs); or hardcoding one job and claiming done
- Blind spots considered:
  - In-game verification needs FFXIV + Dalamud (out of cloud scope; compile + evals + scenario doc required)
  - May need per-job differences (PLD/WAR/DRK/GNB)
  - Interaction with manual mitigation locks, experimental mode, IPC
- Existing plan facts: None — discovery required in Scout

## Goal Kind

`specific` (feature integration with discovery phase)

## Current Tranche

**All PvE tanks ported** (T005). WAR signed off (`notes/T006-war-signoff.md`). PLD/GNB/DRK ready for operator testing (`notes/T007-multi-tank-port.md`).

1. ~~Scout + WAR + TCH IPC~~ (T001–T004, T005-tch-ipc, T006)
2. ~~PLD / GNB / DRK smart mit~~ (T005)
3. **Pending:** T999 final audit + per-job in-game validation

## Non-Negotiable Constraints

- Do not break non-tank jobs or mitigation-off paths.
- Preserve user overrides / disable flags where they exist today.
- No duplicate cooldown tables unless PM/Judge documents why adapter pattern requires it.
- Dalamud API 15 / .NET 10 compatibility maintained.

## Stop Rule

Stop when T999 audit maps receipts to oracle: selective CD selection proven for scoped jobs and verification green.

## Slice Sizing

Largest safe useful slice per Judge — prefer one decision pipeline + one job vertical before rolling all tanks.

## Canonical Board

`docs/goals/smart-mit-tank-cd-helper/state.yaml`

## Execute Command

```text
Goal execute: Follow docs/goals/smart-mit-tank-cd-helper/goal.md
```
