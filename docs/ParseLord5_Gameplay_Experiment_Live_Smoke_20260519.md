---
tags:
  - type/audit
  - project/parselord5
  - status/active
type: audit
project: parselord5
status: archived
aliases: []
---
# ParseLord5 Gameplay Experiment — Live Smoke Test — 2026-05-19

## Purpose

Verify all **21 jobs / 43 gates** behind `ParseLord5ExperimentalMode` in a live Dalamud session. Confirms:

1. **Flag off** — behavior matches pre-experiment WrathCombo simple presets.
2. **Flag on** — paired oGCD priority swap fires when both actions are ready in the same weave window.
3. No crashes, config corruption, or cross-job bleed.

**Prerequisite identity/load checks:** see [ParseLord5_Runtime_Smoke_And_Collision_Audit_20260517.md](ParseLord5_Runtime_Smoke_And_Collision_Audit_20260517.md) section 1 (plugin load, `/pl5`, config path).

**Source commits under test:** `78a2aa5a9` (BLM) + `0296fff85` (remaining jobs).

---

## Preconditions

| Step | Action |
|------|--------|
| 1 | `dotnet build .\WrathCombo\WrathCombo.csproj -c Release` → 0 errors |
| 2 | Confirm Dalamud loads `ParseLord5.dll` from devPlugins |
| 3 | Config path: `%AppData%\XIVLauncher\pluginConfigs\ParseLord5.json` |
| 4 | Training dummy or low-risk target (e.g. **Limsa** striking dummy, **Materia** EX trial if confident) |
| 5 | Enable **Action Event** or combat log if you want hard proof of cast order |
| 6 | Optional: `/pl5 debug` before session; note `dalamud.log` path |

---

## Enable the flag

1. `/pl5` → **Settings** → **Main UI Options**
2. Toggle **ParseLord5 Experimental Mode** (`ParseLord5ExperimentalMode`)
3. Change applies on next combo resolution (no reload required)
4. **Default must stay OFF** for baseline pass; turn ON only for experiment pass

---

## Test method (per job)

For each job you can play on the test character:

### Pass A — Baseline (flag **OFF**)

1. Enable the job’s **Simple ST** preset (name in table below).
2. Enable the job’s **Simple AoE** preset if listed.
3. Fight until the **first two** experiment actions in the table are both off cooldown and a weave window opens.
4. Record which action the combo pressed **first**.
5. **Expected:** “Baseline first” column.

### Pass B — Experiment (flag **ON**)

1. Keep same presets enabled.
2. Repeat until the same pair is both ready.
3. **Expected:** “Experiment first” column (swapped order).

### Pass criteria

| Result | Meaning |
|--------|---------|
| **PASS** | Baseline order with flag off; swapped order with flag on; no errors |
| **INCONCLUSIVE** | Could not get both actions ready in one window (note why) |
| **FAIL** | Wrong order, crash, wrong preset, or flag off still swaps |

### WAR extra gate (3rd)

With flag **ON**, enable debug trace (see `ParseLord5_Warrior_Debug_Tracing.md`): throttled `gcd-exp` / `ogcd-exp` lines in log when `WAR_ST_Simple` / `WAR_AoE_Simple` run.

---

## Master job table

**Legend:** *Baseline first* = flag OFF. *Experiment first* = flag ON.

| Job | Simple ST preset | Simple AoE preset | Pair tested | Baseline first | Experiment first |
|-----|------------------|-------------------|-------------|----------------|------------------|
| WAR | `WAR_ST_Simple` | `WAR_AoE_Simple` | GCD vs oGCD pass order | oGCD → GCD | GCD → oGCD |
| DRG | `DRG_ST_SimpleMode` | `DRG_AoE_SimpleMode` | Lance Charge / Battle Litany | Lance Charge | Battle Litany |
| SAM | `SAM_ST_SimpleMode` | `SAM_AoE_SimpleMode` | Meikyo / Ikishoten | Meikyo | Ikishoten |
| WHM | `WHM_ST_Simple_DPS` | `WHM_AoE_Simple_DPS` | Assize / Presence of Mind | Assize | Presence of Mind |
| DRK | `DRK_ST_Simple` | `DRK_AoE_Simple` | Spender / Cooldown_2 | Spender | Cooldown_2 |
| AST | `AST_ST_Simple_DPS` | `AST_AOE_Simple_DPS` | Divination / Earthly Star | Divination | Earthly Star |
| BRD | `BRD_ST_SimpleMode` | `BRD_AoE_SimpleMode` | Wanderer’s Minuet / Mage’s Ballad | Minuet | Ballad |
| RDM | `RDM_ST_SimpleMode` | `RDM_AoE_SimpleMode` | Fleche / Contre Sixte | Contre Sixte | Fleche |
| NIN | `NIN_ST_SimpleMode` | `NIN_AoE_SimpleMode` | Trick Attack / Mug | Trick Attack | Mug |
| MCH | `MCH_ST_SimpleMode` | `MCH_AoE_SimpleMode` | Reassemble / Queen | Reassemble | Queen |
| DNC | `DNC_ST_SimpleMode` | `DNC_AoE_SimpleMode` | Flourish / Devilment | Flourish | Devilment |
| SMN | `SMN_ST_Simple_Combo` | `SMN_AoE_Simple_Combo` | Garuda / Ifrit (after Titan) | Garuda | Ifrit |
| BLM | `BLM_ST_SimpleMode` | `BLM_AoE_SimpleMode` | Amplifier / Ley Lines | Amplifier | Ley Lines |
| GNB | `GNB_ST_Simple` | `GNB_AoE_Simple` | No Mercy / Bloodfest | No Mercy | Bloodfest |
| PLD | `PLD_ST_SimpleMode` | `PLD_AoE_SimpleMode` | Circle of Scorn / Spirits Within | Scorn | Spirits Within |
| SCH | `SCH_ST_Simple_DPS` | `SCH_AoE_Simple_DPS` | Chain Stratagem / Energy Drain | Chain | Energy Drain |
| SGE | `SGE_ST_Simple_DPS` | `SGE_AoE_Simple_DPS` | Psyche / Soteria | Psyche | Soteria |
| RPR | `RPR_ST_SimpleMode` | `RPR_AoE_SimpleMode` | Gluttony / Bloodstalk (ST), Gluttony / Grim Swathe (AoE) | Gluttony | Spender |
| MNK | `MNK_ST_SimpleMode` | `MNK_AoE_SimpleMode` | Brotherhood / Riddle of Fire | Brotherhood | Riddle of Fire |
| VPR | `VPR_ST_SimpleMode` | `VPR_AoE_SimpleMode` | Hunter / Swiftskin venom weaves | Hunter | Swiftskin |
| PCT | `PCT_ST_SimpleMode` | `PCT_AoE_SimpleMode` | Living Muse / Steel Muse | Living | Steel |

**Not in scope:** BLU, DOL (no simple ST+AoE experiment rollout).

---

## Suggested session order

Minimize job switches:

1. **Tanks (4):** WAR → DRK → PLD → GNB  
2. **Melee (6):** DRG → SAM → NIN → VPR → MNK → RPR  
3. **Ranged (4):** BRD → MCH → DNC  
4. **Casters (5):** BLM → SMN → RDM → PCT  
5. **Healers (4):** WHM → AST → SCH → SGE  

~5–10 minutes per job if both ST and AoE are tested; full sweep ≈ 2–3 hours.

**Quick spot-check (30 min):** WAR, BLM, PCT, AST, GNB — one per role + PCT helper path.

---

## Recording template

Copy per job:

```text
Job: ___
Commit: 0296fff85 (or current HEAD)
Flag OFF ST: PASS / FAIL / INC — observed: ___
Flag OFF AoE: PASS / FAIL / INC — observed: ___
Flag ON ST: PASS / FAIL / INC — observed: ___
Flag ON AoE: PASS / FAIL / INC — observed: ___
Notes: ___
```

---

## Failure triage

| Symptom | Check |
|---------|--------|
| No difference flag on/off | Wrong preset (Advanced vs Simple); flag not saved to `ParseLord5.json` |
| Swap when flag off | File not from current build; wrong plugin loaded |
| Crash on toggle | `dalamud.log` stack trace; note job + preset |
| Only ST works | AoE preset not enabled or different preset name |
| PCT no change | Must use **Simple** mode presets (gate is in `TryOGCDSpells` for `Combo.Simple` only) |
| SMN no Ifrit/Garuda swap | Need both egis ready; Titan still first |

---

## Session exit checklist

- [ ] Set `ParseLord5ExperimentalMode` back to **OFF** if you use ParseLord5 daily
- [ ] Save config (closing UI or reload plugin)
- [ ] Attach results file or paste summary into next handoff
- [ ] If all PASS → milestone **Advanced preset experiments** or **branding** is unblocked

---

## Related docs

| Doc | Content |
|-----|---------|
| `ParseLord5_Gameplay_Experiment_Batch2_20260519.md` | Batch 2 job list |
| `ParseLord5_Gameplay_Experiment_BLM_20260519.md` | BLM detail |
| `ParseLord5_Gameplay_Experiment_*_20260517.md` | Per-job notes (early jobs) |
| `ParseLord5_Runtime_Smoke_And_Collision_Audit_20260517.md` | Plugin identity / config isolation |
