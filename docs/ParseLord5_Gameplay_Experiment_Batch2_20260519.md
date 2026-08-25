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
# ParseLord5 Gameplay Experiments — Batch 2 — 2026-05-19

## Purpose

Complete simple-preset ST+AoE experiment coverage for all remaining PvE jobs not yet gated behind `ParseLord5ExperimentalMode`. BLM was added in the prior commit; this batch adds eight jobs in one gameplay commit.

## Jobs Added (8)

| Job | Presets | Swap (flag off → flag on) |
|-----|---------|---------------------------|
| GNB | `GNB_ST_Simple`, `GNB_AoE_Simple` | No Mercy → Bloodfest → **Bloodfest → No Mercy** |
| PLD | `PLD_ST_SimpleMode`, `PLD_AoE_SimpleMode` | Circle of Scorn → Spirits Within → **Spirits Within → Circle of Scorn** |
| SCH | `SCH_ST_Simple_DPS`, `SCH_AoE_Simple_DPS` | Chain Stratagem → Energy Drain → **Energy Drain → Chain Stratagem** |
| SGE | `SGE_ST_Simple_DPS`, `SGE_AoE_Simple_DPS` | Psyche → Soteria → **Soteria → Psyche** |
| RPR | `RPR_ST_SimpleMode`, `RPR_AoE_SimpleMode` | Gluttony → Bloodstalk/Grim Swathe → **spender first, Gluttony second** |
| MNK | `MNK_ST_SimpleMode`, `MNK_AoE_SimpleMode` | Brotherhood → Riddle of Fire → **Riddle of Fire → Brotherhood** |
| VPR | `VPR_ST_SimpleMode`, `VPR_AoE_SimpleMode` | Hunter/Fellhunter venom → Swiftskin/Fellskin venom → **Swiftskin line first** |
| PCT | `PCT_ST_SimpleMode`, `PCT_AoE_SimpleMode` (via `TryOGCDSpells`) | Living Muse → Steel Muse → **Steel Muse → Living Muse** |

## Scope

- Job-local oGCD/utility priority swaps only
- No shared core files (`ActionReplacer`, `AutoRotationController`, IPC, manifest)
- Default behavior unchanged when `ParseLord5ExperimentalMode` is `false`
- PCT gate applies only when `Combo.Simple` is set (simple ST/AoE presets)

## Build

**PASS.** 0 errors.

## Totals (after batch + BLM)

**43 gates, 21 jobs** — all jobs with simple ST/AoE presets now have experimental coverage.

| Job | Gates |
|-----|-------|
| WAR | 3 |
| DRG, SAM, WHM, DRK, AST, BRD, RDM, NIN, MCH, DNC, SMN, BLM, GNB, PLD, SCH, SGE, RPR, MNK, VPR, PCT | 2 each |

## Skipped

- **BLU** / **DOL** — no matching simple ST+AoE preset pattern in the same rollout style
- **Advanced presets** — deferred to a later milestone
