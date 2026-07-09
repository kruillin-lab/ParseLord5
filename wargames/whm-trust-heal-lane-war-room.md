---
tags:
  - type/report
  - project/parselord5
  - status/active
type: report
project: parselord5
status: active
aliases: []
---
# War Room - WHM trust heal lane

**Status:** EXECUTED
**Date opened:** 2026-07-07
**Advisor:** Codex session, AER role-hat fallback
**Battle plan:** [whm-trust-heal-lane-battle-plan.md](whm-trust-heal-lane-battle-plan.md)

Convened because the user reported AST trust healing improved after the upstream-baseline healer patch, but WHM trust healing still appears to come only from incidental Afflatus Rapture lily-overcap prevention and Assize. The user explicitly required `/war-room` before another implementation/debugging attempt.

## 1. Frame

- **Current state:** deployed ParseLord5 build contains the upstream-baseline healer patch; live report says AST trust now seems to heal, while WHM trust does not reliably use the dedicated heal lane.
- **Desired end state:** prove where WHM trust healing fails before tuning or behavior changes, then patch only the demonstrated failure.
- **Five Ws:** user is live-testing WHM in trust content; behavior is in `/home/kruillin/Projects/Projects/ParseLord5`; the report is from 2026-07-07 after the 12:12 deployed DLL; urgency is that WHM can DPS/weave incidentally but not sustain the party.
- **In scope:** WHM auto-rotation healer target selection, heal-delay gating, ST/AoE heal preset invocation, trust NPC inclusion/exclusion, and WHM combo returned action.
- **Out of scope:** restoring DPS-heal priority helpers, healer tuning thresholds, broad SGE/SCH/AST rewrites, changing user config as the primary fix, and overwriting unrelated dirty worktree changes.
- **Constraints:** use a WHM-specific proof loop first; keep logs throttled; do not reintroduce the fork-only DPS-heal leak fixed by the upstream-baseline pass; run quality gate before declaring implementation complete.
- **Commander's intent:** create a WHM-only live trace that can say, from one trust pull, whether the controller did not select healing, delayed healing, excluded trust NPCs, invoked the wrong lane, or the WHM combo returned no usable healing action.

## 2. Recon

### Facts

| # | Fact | Evidence |
|---|------|----------|
| F1 | The last deployed DLL includes the upstream-baseline healer patch and no WHM-trust-specific fix. | `/tmp/parselord5-whm-trust-healing-handoff-20260707.md` |
| F2 | User reported AST trust now appears to work, but WHM trust still only heals incidentally via Afflatus Rapture lily-overcap prevention and Assize. | `/tmp/parselord5-whm-trust-healing-handoff-20260707.md` |
| F3 | The previous war-room already identified instrumentation as the fallback if AST trust or WHM Medica/heal behavior still failed after upstream-baseline restoration. | `wargames/healer-upstream-baseline-battle-plan.md` trigger forks |
| F4 | Auto-rotation selects a single heal target through `AutoRotationHelper.GetSingleTarget(cfg.HealerRotationMode)`, computes `aoeheal`, `needsHeal`, `actCheck`, `effectiveHealDelay`, and `canHeal`, then processes heal or DPS actions based on `canHeal`. | `WrathCombo/AutoRotation/AutoRotationController.cs:459-539` |
| F5 | `ProcessAutoActions` only runs actions whose `AutoAction.IsHeal` equals `canHeal`, and heal entries call `AutomateHealing`. | `WrathCombo/AutoRotation/AutoRotationController.cs:680-723` |
| F6 | WHM ST/AoE heal presets are marked as heal auto-actions, while WHM DPS presets remain DPS auto-actions. | `WrathCombo/Combos/CustomComboPreset.cs:7667-7695`, `WrathCombo/Combos/CustomComboPreset.cs:7793-7799`, `WrathCombo/Combos/CustomComboPreset.cs:7862-7867` |
| F7 | `SimpleTarget.Stack.OneButtonHealLogic` uses `AutoRotationController.AutorotHealTarget` when set, otherwise falls back to `AllyToHeal`. | `WrathCombo/CustomCombo/SimpleTarget.cs:83-89` |
| F8 | WHM ST and AoE heal combos both use `SimpleTarget.Stack.OneButtonHealLogic` as the local heal target. | `WrathCombo/Combos/PvE/WHM/WHM.cs:411`, `WrathCombo/Combos/PvE/WHM/WHM.cs:539`, `WrathCombo/Combos/PvE/WHM/WHM.cs:624` |
| F9 | `HealerTargeting.HealTargets()` can include any party-list member passing targetable, range, HP, status, and line-of-sight checks. | `WrathCombo/AutoRotation/AutoRotationController.cs:1526-1540` |
| F10 | `HealerTargeting.CanAoEHeal()` explicitly excludes `IsOutOfPartyNPC` members before counting AoE heal targets. | `WrathCombo/AutoRotation/AutoRotationController.cs:1543-1563` |
| F11 | The party list appends out-of-party NPCs when retargeting is enabled or healer auto-rotation has `IncludeNPCs=true`, and marks them `IsOutOfPartyNPC=true`. | `WrathCombo/CustomCombo/Functions/Party.cs:88-104` |
| F12 | The saved ParseLord5 config has `IncludeNPCs=true`, advanced WHM heal auto-actions enabled, and simple WHM heal auto-actions disabled; the saved `RotationConfig.Enabled=false` may reflect post-test plugin state and must be live-confirmed. | `/home/kruillin/.xlcore/pluginConfigs/ParseLord5.json` inspected 2026-07-07 |
| F13 | `AutomateHealing` returns false without lane details if the player is casting or `HealThrottle` is active, then dispatches to ExecuteAoE or ExecuteST. | `WrathCombo/AutoRotation/AutoRotationController.cs:1017-1031` |
| F14 | ST execution invokes the combo with the selected target, checks action readiness/range/target usability, and can fail silently at several points before `UseAutorotAction`. | `WrathCombo/AutoRotation/AutoRotationController.cs:1223-1297` |
| F15 | AoE heal execution invokes the combo on the player, then separately requires `HealerTargeting.CanAoEHeal(outAct)` before using the action. | `WrathCombo/AutoRotation/AutoRotationController.cs:1099-1129` |

### Assumptions

| # | Assumption | Settling check (read-only or instrumentation) | Routing |
|---|------------|-----------------------------------------------|---------|
| A1 | WHM ST healing may not be entering the heal lane because `needsHeal`, `actCheck`, `TimeToHeal`, or `canHeal` is false. | Log those values only while `Player.Job` is WHM. | If false before invocation, patch controller gating or config state, not WHM combo priorities. |
| A2 | WHM AoE healing may be blocked because trust NPCs are counted as `IsOutOfPartyNPC` and excluded from `CanAoEHeal()`. | Log party count, out-of-party NPC count, low-HP count, and AoE decision. | If confirmed, decide separately whether trust NPCs should count for AoE under `IncludeNPCs`. |
| A3 | WHM combo may be invoked but return a non-healing fallback, blocked setup oGCD, or an action that cannot be queued on the selected target. | Log preset, selected target, `outAct`, readiness, and execute result at the ST/AoE healing boundaries. | If confirmed, patch WHM combo branch or retargeting for that action. |
| A4 | Saved `RotationConfig.Enabled=false` may be a post-test artifact, not the live failure, because user observed WHM offensive oGCD behavior. | User retest with instrumentation can capture whether `Run()` is active and whether WHM auto-actions are considered. | If auto-rotation is actually off during retest, stop code work and resolve config/test procedure first. |

### RECON NEEDED

- [R1] Live WHM trust log with `[PL5-WHM-HEAL]` entries while party members take damage - route to controller, AoE NPC, or combo-return fix.
- [R2] Confirm whether the user is testing advanced WHM heals (`WHM_STHeals` / `WHM_AoEHeals`) or simple heals - route log fields by preset.

## 3. Criteria

**Screening:** feasible; suitable; acceptable; distinguishable; complete

| Criterion | Weight | Why it matters here |
|-----------|--------|---------------------|
| Diagnostic separation | 5 | The next step must distinguish controller gating, target selection, trust NPC counting, and WHM combo output. |
| Behavior safety | 5 | WHM healing changes can wipe trust pulls; proof must precede behavior edits. |
| Scope control | 4 | The worktree is already dirty and prior fixes should not be overwritten. |
| Live usefulness | 4 | The user can only prove this in-game; logs must answer the next routing question in one run. |
| Upstream alignment | 3 | Avoid restoring fork-only DPS-heal helpers or drifting away from the upstream-baseline contract without evidence. |
| Cleanup cost | 2 | Temporary diagnostics should be easy to remove after the bug is isolated. |

## 4. Courses Of Action

### COA-0 - No code change; ask for another live retest

- **Purpose:** avoid adding diagnostics and rely on user observation.
- **Contract:** no files touched.
- **Work guidance:** ask the user to retest WHM with current settings and report actions seen.
- **Verification:** subjective live report.
- **Screen:** killed. It is not distinguishable or complete; current observations already lack the needed lane evidence.

### COA-1 - Restore WHM DPS-heal helper calls

- **Purpose:** make DPS buttons inject WHM heals again, hoping trust survival improves.
- **Contract:** edit WHM DPS combo paths.
- **Work guidance:** re-add `TryDpsSingleTargetHealPriority` / `TryDpsAoEHealPriority`.
- **Verification:** live WHM appears to cast heals.
- **Screen:** killed. It violates upstream alignment and risks reintroducing the exact DPS-heal leakage removed in the prior pass.

### COA-2 - Patch trust NPC AoE counting now

- **Purpose:** allow trust NPCs to count for AoE healing when `IncludeNPCs=true`.
- **Contract:** edit `HealerTargeting.CanAoEHeal()`.
- **Work guidance:** remove or conditionally relax `!x.IsOutOfPartyNPC`.
- **Verification:** WHM AoE healing begins in trusts.
- **Screen:** killed for this pass. It may be the fix, but it only explains AoE and would not prove ST lane behavior.

### COA-3 - Controller-only WHM instrumentation

- **Purpose:** prove controller gating, target selection, and AoE trust counts without entering WHM combo internals.
- **Contract:** edit only `AutoRotationController.cs`.
- **Work guidance:** add throttled WHM logs around `needsHeal`, `canHeal`, `actCheck`, ST/AoE execute attempts, and trust counts.
- **Verification:** `[PL5-WHM-HEAL]` log can classify the failure before combo internals.
- **Screen:** survives, but may miss a WHM combo returning the wrong action after invocation.

### COA-4 - End-to-end WHM-only instrumentation

- **Purpose:** build a live feedback loop across controller selection and WHM combo return boundaries, with no behavior changes.
- **Contract:** edit `AutoRotationController.cs` plus WHM heal combo logging only; no logic changes, no config changes, no AST/SGE/SCH changes.
- **Work guidance:** add throttled logs tagged `[PL5-WHM-HEAL]` around controller heal decision, ST/AoE healing execution, and WHM ST/AoE combo returned actions.
- **Verification:** one WHM trust pull produces enough evidence to route to controller gating, AoE NPC counting, target/retarget failure, or WHM priority return.
- **Screen:** survives and is complete.

## 5. Wargame

### COA-3 fought

| Move | Expected observation | Most-likely failure and cause | Counter-move |
|------|----------------------|-------------------------------|--------------|
| Log WHM controller decision once per second | `needsHeal`, `canHeal`, `actCheck`, target HP, and AoE decision are visible | Values show heal lane active but no action used; combo internals remain opaque | Add combo-return instrumentation |
| Log trust NPC count and AoE eligible count | Confirms whether trust NPCs are present but excluded from AoE count | Count requires more than current controller cheaply exposes | Compute only simple counts under throttle |
| Log ST/AoE execute result | Shows whether `ExecuteST` / `ExecuteAoE` returns true | False result does not identify which branch failed | Add focused logs near readiness/range or combo return |

**Red-team verdict:** Controller-only logs are lower touch but may force a second instrumentation build.
**Second-order effects:** safer first edit, but slower live iteration.

### COA-4 fought

| Move | Expected observation | Most-likely failure and cause | Counter-move |
|------|----------------------|-------------------------------|--------------|
| Add WHM-only throttled controller summary | Live log shows whether WHM healing was needed and whether delay/action readiness allowed heal lane | Log spam in combat if not throttled tightly | Gate by `Player.Job is WHM` and `EzThrottler` key |
| Add WHM-only ST/AoE execute logs | Shows preset, selected target, `outAct`, and execute return | Logging every failed poll makes noisy logs | Throttle per lane/preset and include only summary fields |
| Add WHM heal combo returned-action logs | Shows whether WHM ST/AoE combo returns Cure/Cure2/Tetra/Rapture/Assize/etc. | Touching combo code risks accidental behavior change | Introduce local `LogWhmHealChoice` helper and return the same action |
| Deploy instrumentation DLL | User can run one trust pull and collect log lines | Saved config has auto-rotation disabled during retest | Logs prove no controller loop; user re-enables auto-rotation before code fixes |
| Remove instrumentation after root cause is isolated | Repo returns to clean behavior patch | Root cause still ambiguous | Re-convene Phase 2 with captured log lines |

**Red-team verdict:** COA-4 touches more code than controller-only logs, but it is still no-behavior-change and gives the best chance of one-run diagnosis.
**Second-order effects:** temporary logs must be removed or intentionally converted into debug-gated diagnostics after the fix.

### Decision matrix

| Criterion (weight) | COA-0 | COA-1 | COA-2 | COA-3 | COA-4 |
|--------------------|-------|-------|-------|-------|-------|
| Diagnostic separation (5) | 1 | 1 | 2 | 4 | 5 |
| Behavior safety (5) | 5 | 1 | 2 | 5 | 5 |
| Scope control (4) | 5 | 2 | 4 | 5 | 4 |
| Live usefulness (4) | 1 | 2 | 3 | 4 | 5 |
| Upstream alignment (3) | 5 | 0 | 3 | 5 | 5 |
| Cleanup cost (2) | 5 | 3 | 4 | 4 | 3 |
| **Total** | **59** | **28** | **61** | **103** | **106** |

## 6. Decision Record

- **Selected:** COA-4 - end-to-end WHM-only instrumentation.
- **Why losers lost:** COA-0 repeats subjective testing; COA-1 violates the upstream-baseline contract; COA-2 may be a later fix but prematurely assumes AoE NPC counting is the only failure; COA-3 is slightly safer but likely under-instruments the decisive combo-return boundary.
- **Residual risk accepted:** temporary logs touch hot paths and must be throttled and removed after diagnosis; saved config currently says auto-rotation disabled, so the first live retest may prove a test-state issue instead of a code bug.
- **Orders:** battle plan at `wargames/whm-trust-heal-lane-battle-plan.md`.

## 7. Supervision And After-Action

- **Execution log:** implemented and deployed WHM-only instrumentation in `AutoRotationController.cs` and `WHM.cs`; no thresholds, priorities, target selection, or DPS-heal helper behavior changed.
- **Re-convene events:** live WHM trust retest still pending; re-open Phase 2 when `[PL5-WHM-HEAL]` lines identify a trigger fork.
- **Reviewer verdict:** role-hat review passed: instrumentation is WHM-gated, throttled, and preserves the SGE defensive-action source-shape regression test.
- **Quality gate:** `python /home/kruillin/Projects/Projects/quality-gate/gate.py normal --repo /home/kruillin/Projects/Projects/ParseLord5 --task whm-trust-heal-lane` failed with run `20260707T165135Z-34d27c57` because configured Windows `powershell` commands are unavailable on Linux and older unrelated wargame docs are missing metadata; direct Linux test/build passed.
- **Written back:** no AGENTS workflow change.
