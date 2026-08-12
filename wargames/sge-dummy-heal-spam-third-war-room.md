---
tags:
  - type/report
  - project/parselord5
  - status/done
type: report
project: parselord5
status: done
aliases: []
---
# War Room - SGE dummy heal spam third attempt

**Status:** EXECUTED
**Date opened:** 2026-07-07
**Advisor:** Codex session, role-hat AER fallback
**Battle plan:** [sge-dummy-heal-spam-third-battle-plan.md](sge-dummy-heal-spam-third-battle-plan.md)

User confirmed the second deployed DLL still makes SGE spam healing cooldowns on a dummy after disabling and re-enabling PL5. This reconvenes the war room from the prior dossier because a load-bearing fact was invalidated: closing global raidwide plus SGE heal-combo raidwide routes did not close the symptom.

## 1. Frame

- **Current state:** SGE auto-rotation still spends dummy-combat actions on healer/utility buttons after the previous two fixes.
- **Desired end state:** while fighting a dummy with no damaged heal targets, SGE auto-rotation presses offensive actions only, except intentionally offensive healer actions such as Pneuma/Psyche/Eukrasian Dosis setup.
- **Five Ws:** user testing SGE; PL5 auto-rotation; host dev plugin under `/home/kruillin/.xlcore/devPlugins/ParseLord5`; still failing after the 2026-07-07 11:36 deployed DLL; need a stronger controller contract now.
- **In scope:** `AutoRotationController`, SGE auto-action contract, focused source-structure regressions, build/deploy.
- **Out of scope:** broad healer redesign, unrelated uncommitted rotation work, changing user config as a workaround.
- **Constraints:** preserve real healing when `canHeal` is true; preserve grouped/boss raidwide handling; do not rely on stale Dalamud logs; run project quality gate before declaring complete.
- **Commander's intent:** A solo dummy SGE auto-rotation pass with `canHeal == false` must not be able to press SGE defensive/healing actions through any DPS-lane or preemptive-shield path.

## 2. Recon

### Facts

| # | Fact | Evidence |
|---|------|----------|
| F1 | Current `dalamud.log` is stale relative to the deployed DLL and current test; it ends at 10:42:57 while the live DLL was copied at 11:36. | `stat /home/kruillin/.xlcore/logs/dalamud.log` and DLL stat output in this session |
| F2 | The live user config has one weave slot, all SGE DPS/heal auto actions enabled, `PreEmptiveHoT=true`, and `HandleRaidwides=true`. | `/home/kruillin/.xlcore/pluginConfigs/ParseLord5.json` lines around 43, 5131-5134, 5179-5200 |
| F3 | `ProcessAutoActions` filters presets by `AutoAction.IsHeal == canHeal`, but later trusts whatever action the combo returns. | `WrathCombo/AutoRotation/AutoRotationController.cs`, `ProcessAutoActions`, `ExecuteST`, `ExecuteAoE` |
| F4 | SGE DPS combos can still return non-damage SGE buttons from the DPS lane, including Kardia, Lucid Dreaming, Rhizomata, and Eukrasia setup. | `WrathCombo/Combos/PvE/SGE/SGE.cs` DPS combo branches |
| F5 | Preemptive shield runs before normal combat/heal gating and can cast SGE Eukrasia/Eukrasian Diagnosis when a focus target exists and any hostile is nearby. | `WrathCombo/AutoRotation/AutoRotationController.cs`, `PreEmptiveShield()` |
| F6 | The previous fixes already closed global raidwide, SGE heal-combo raidwide, and no-party average HP routes; the symptom persists, so at least one other route is live. | `sge-dummy-raidwide-ogcd-starvation-war-room.md` section 9 plus user retest |

### Assumptions

| # | Assumption | Settling check (read-only) | Routing |
|---|------------|----------------------------|---------|
| A1 | At least part of the observed spam is DPS-lane leakage of SGE support actions. | Add a controller guard and test live. | If fixed, keep guard; if not, proceed to instrumentation for exact action source. |
| A2 | Focus-target preemptive shielding explains repeated Eukrasia/Diagnosis but not all cooldowns. | Disable in-combat preemptive shield and test live. | If only shields stop, continue probing heal lane and external retargeting. |
| A3 | The stale log means source-level and live DLL verification are the only available loop without adding temporary instrumentation. | Check log mtime after next user test. | If logs resume, inspect tagged traces; otherwise ask for observed action names/timestamps. |

## 3. Criteria

**Screening:** feasible; suitable; acceptable; distinguishable; complete

| Criterion | Weight | Why it matters here |
|-----------|--------|---------------------|
| Stops dummy heal spam | 4 | The user-facing failure is unchanged after two patches. |
| Preserves real healing | 3 | The controller still needs to heal when `canHeal` is true. |
| Controller-level completeness | 3 | Multiple downstream patches missed paths; the next fix should guard the shared boundary. |
| Low blast radius | 2 | Existing worktree is dirty and rotation code is broad. |
| Verifiable deployment | 2 | The user is testing live; the DLL copy must be exact. |

## 4. Courses Of Action

### COA-0 - Config workaround

- **Purpose:** tell user to disable SGE heal presets, preemptive HoT, or HandleRaidwides.
- **Contract:** no code change.
- **Verification:** user stops seeing heals.
- **Screen:** killed. Not complete; leaves PL5 behavior broken under default-like settings.

### COA-1 - SGE-local suppression only

- **Purpose:** keep adding `IsSelectingAutorotAction` guards inside SGE combo branches.
- **Contract:** touch only SGE combo files.
- **Verification:** source checks plus live dummy test.
- **Screen:** killed. Not complete; it relies on finding every downstream branch and has already missed routes twice.

### COA-2 - Controller DPS-lane allow/deny contract plus in-combat preemptive shield gate

- **Purpose:** enforce that non-heal autorotation cannot press SGE defensive/healing actions even if an SGE DPS combo returns them; also stop preemptive shields during active combat.
- **Contract:** touch `AutoRotationController.cs` and structural tests only; do not change `canHeal` healing behavior.
- **Verification:** tests assert the guard exists and preemptive shield skips `InCombat`; build/deploy; live dummy test.
- **Screen:** survives. Suitable and distinguishable because it attacks the selection boundary, not another SGE branch.

### COA-3 - Add temporary instrumentation first

- **Purpose:** deploy tagged logs for every autorotation action source, then have user reproduce.
- **Contract:** temporary debug logs in controller, cleanup after diagnosis.
- **Verification:** post-test log identifies the exact path.
- **Screen:** survives but lower-ranked because Dalamud logs are currently stale and user needs a fix now.

## 5. Wargame

### COA-2 fought

| Move | Expected observation | Most-likely failure and cause | Counter-move |
|------|----------------------|-------------------------------|--------------|
| Add `CanUseAutorotDpsAction` after combo invocation in ST/AoE DPS execution | DPS presets can no longer press SGE healing/support actions returned by combo logic | A legitimate offensive action is blocked because it shares support classification | Keep allow list by explicit SGE IDs; do not block Eukrasia or Pneuma/Psyche/Toxikon/Phlegma |
| Add SGE defensive/recovery action deny list | Rhizomata/Kardia/Kerachole/Physis/etc. cannot leak from DPS lane | User still sees heals from the true heal lane | Instrument `canHeal`, `healTarget`, and action source next |
| Add `InCombat()` skip to `PreEmptiveShield` | Dummy combat no longer fires focus-target Eukrasia/Diagnosis | Out-of-combat pre-shield behavior still works, but some in-combat pre-shield use disappears | Accept; in-combat healing belongs to the heal lane, not preemptive path |
| Build and deploy | Live DLL hash/timestamp changes | User still sees spam | Trigger COA-3 instrumentation with tagged logs |

**Red-team verdict:** COA-2 may not catch an external plugin pressing beneficial actions, but it closes the highest-risk internal controller routes without needing unreliable logs.

### COA-3 fought

| Move | Expected observation | Most-likely failure and cause | Counter-move |
|------|----------------------|-------------------------------|--------------|
| Add `[DEBUG-SGEHEAL]` logs around every action source | User repro creates exact source trace | Log file remains stale | Use visible in-game notification or ask for action list; avoid leaving debug logs |
| Deploy instrumentation only | No behavior change except logs | User still blocked from testing fix | Use only if COA-2 fails |

### Decision matrix

| Criterion (weight) | COA-0 | COA-1 | COA-2 | COA-3 |
|--------------------|-------|-------|-------|-------|
| Stops dummy heal spam (4) | 1 | 2 | 4 | 2 |
| Preserves real healing (3) | 1 | 3 | 4 | 4 |
| Controller-level completeness (3) | 0 | 1 | 4 | 3 |
| Low blast radius (2) | 4 | 3 | 3 | 2 |
| Verifiable deployment (2) | 1 | 3 | 4 | 2 |
| **Total** | **15** | **29** | **58** | **40** |

## 6. Decision Record

- **Selected:** COA-2 - enforce the DPS-lane action contract in the controller and disable in-combat preemptive shield.
- **Why losers lost:** COA-0 is a workaround; COA-1 has already proven incomplete; COA-3 is the fallback if the stronger guard still misses the live route.
- **Residual risk accepted:** an external beneficial-action retargeter or a still-unidentified heal-lane trigger could remain; if so, deploy tagged instrumentation next.
- **Orders:** battle plan at `wargames/sge-dummy-heal-spam-third-battle-plan.md`.

## 7. Supervision And After-Action

- **Execution log:** implemented COA-2. Added `CanUseAutorotDpsAction(uint outAct)` to reject SGE defensive/healing actions from DPS auto-action execution; called it from AoE DPS and ST non-heal execution after combo invocation; added `InCombat()` to `PreEmptiveShield()` early return.
- **Re-convene events:** current reconvening was triggered by user retest failure after second deployed DLL.
- **Reviewer verdict:** role-hat review found one overbroad draft placement that would have applied the ST guard to healing too; fixed before test. It also found the first preemptive edit landed on `PreEmptiveHot`; restored HoT behavior and kept the combat guard only on `PreEmptiveShield`.
- **Verification:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` inside `dev-arch` passed 43/43. Release build with `-p:DalamudLibPath=/home/kruillin/.xlcore/dalamud/Hooks/15.0.2.2/` succeeded with 3 baseline warnings. Deployed live DLL to `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`, hash `33ba23b1265f3bf3bd208e63505e14063714b6f6a49895d565380594ccdb3fa1`.
- **Quality gate:** `20260707T155315Z-5ddcb7a3` failed on Linux environment/tooling: configured build and domain eval commands require `powershell`; Markdown audit also flags three unrelated pre-existing untracked wargame docs without metadata.
- **Written back:** this dossier and sibling battle plan capture the third-attempt route.
