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
# War Room - Healer autorotation probe execution split

**Status:** DECIDED
**Date opened:** 2026-07-07
**Advisor:** Codex session, role-hat AER fallback
**Battle plan:** [healer-autorot-probe-execution-split-battle-plan.md](healer-autorot-probe-execution-split-battle-plan.md)

User added two key observations after the third SGE-only deployment: WHM casts a lone Medica III and WHM uses no oGCD skills, while AST and SCH DPS appear to work. This shifts the decision away from "all healer autorotation is broken" and toward a probe/execution context bug affecting jobs that gate DPS oGCDs behind `!AutoRotationController.IsSelectingAutorotAction`.

## 1. Frame

- **Current state:** WHM and SGE are not behaving like normal DPS autorotation on a dummy; WHM can heal once and does not use expected oGCDs, while AST/SCH DPS are clean.
- **Desired end state:** dry-run/probe combo calls suppress unsafe heal/raidwide side effects, but actual autorotation execution can still use legitimate DPS oGCDs such as WHM Assize and Presence of Mind.
- **Five Ws:** user testing healer jobs on dummies; PL5 autorotation; `/home/kruillin/Projects/Projects/ParseLord5`; after multiple SGE fixes on 2026-07-07; new cross-job evidence isolates context handling.
- **In scope:** `AutoRotationController.InvokeCombo` context flag semantics, structural tests, build/deploy.
- **Out of scope:** changing user config as a workaround, broad WHM/SGE priority rewrites, touching unrelated dirty files.
- **Constraints:** preserve prior SGE guard behavior during dry-run probes; keep actual healing when `canHeal` is true; keep the SGE controller DPS deny list; run quality gate.
- **Commander's intent:** only readiness probes should set `IsSelectingAutorotAction`; actual autorotation execution must not set it, so DPS combos can return legitimate oGCDs.

## 2. Recon

### Facts

| # | Fact | Evidence |
|---|------|----------|
| F1 | WHM DPS combo weaves, including Assize, Presence of Mind, and Lucid Dreaming, are guarded by `!AutoRotationController.IsSelectingAutorotAction`. | `WrathCombo/Combos/PvE/WHM/WHM.cs` simple and advanced DPS weave blocks |
| F2 | `AutoRotationHelper.InvokeCombo` currently sets `IsSelectingAutorotAction=true` for every invocation, including actual `ExecuteST` and `ExecuteAoE` execution calls. | `WrathCombo/AutoRotation/AutoRotationController.cs`, `InvokeCombo()` |
| F3 | The only explicit readiness probe found in the controller is `actCheck`, which invokes heal combos only to see whether any healing action is ready. | `WrathCombo/AutoRotation/AutoRotationController.cs`, `actCheck` block |
| F4 | AST/SCH DPS working while WHM does not is consistent with a job-specific guard pattern rather than a completely broken action loop. | User report, 2026-07-07 |
| F5 | Prior SGE fixes rely on `IsSelectingAutorotAction` to suppress unsafe raidwide/heal branches during probes, and the new SGE DPS deny list protects actual DPS execution. | `sge-dummy-heal-spam-third-war-room.md`; `AutoRotationController.cs` |

### Assumptions

| # | Assumption | Settling check (read-only) | Routing |
|---|------------|----------------------------|---------|
| A1 | WHM no-oGCD behavior is caused by actual execution being mislabeled as selection. | Split the flag and test WHM dummy DPS. | If WHM oGCDs return, keep split; if not, instrument WHM combo output. |
| A2 | SGE heal spam will not regress when execution calls stop setting the flag because the controller-level SGE DPS deny list remains active. | Structural tests assert the deny list is still on DPS execution paths. | If SGE heals still fire, deploy tagged action-source instrumentation. |
| A3 | The lone WHM Medica III may be a separate heal-lane false positive, but fixing probe/execution split is prerequisite because it explains the oGCD starvation symptom. | Retest WHM after split. | If Medica III persists, trace `canHeal`, `aoeheal`, and `HandleRaidwide`. |

## 3. Criteria

**Screening:** feasible; suitable; acceptable; distinguishable; complete

| Criterion | Weight | Why it matters here |
|-----------|--------|---------------------|
| Restores WHM/SGE DPS oGCDs | 4 | User specifically reports missing WHM oGCDs and SGE offensive oGCD starvation. |
| Preserves probe safety | 4 | The previous false-heal fixes depend on dry-run calls not returning heal/raidwide branches. |
| Minimal blast radius | 3 | One controller flag semantic is safer than per-job priority rewrites. |
| Explains AST/SCH contrast | 2 | The selected fix should fit the cross-job observation. |
| Testability | 2 | Must be capturable with structural tests and live DLL verification. |

## 4. Courses Of Action

### COA-0 - No code change

- **Purpose:** accept current third SGE deployment and ask for more manual testing.
- **Contract:** no file changes.
- **Verification:** none.
- **Screen:** killed. It does not address the new WHM oGCD evidence.

### COA-1 - Per-job WHM/SGE exceptions

- **Purpose:** remove or weaken `IsSelectingAutorotAction` guards inside WHM and SGE combo files.
- **Contract:** touch job combo code.
- **Verification:** WHM oGCDs return.
- **Screen:** killed. It repeats the failed pattern of downstream branch edits and risks reintroducing probe side effects.

### COA-2 - Split readiness probe context from actual execution

- **Purpose:** make `InvokeCombo` set `IsSelectingAutorotAction` only when the caller explicitly asks for a dry-run probe.
- **Contract:** touch controller call semantics and tests; do not touch job priority logic.
- **Verification:** structural tests assert `actCheck` passes `selectingAutorotAction: true`, while normal execution calls do not.
- **Screen:** survives. It directly explains WHM no-oGCD behavior and preserves prior probe safety.

### COA-3 - Instrument first

- **Purpose:** add `[DEBUG-HEALER-AUTOROT]` logs around combo output and `UseAutorotAction`.
- **Contract:** temporary instrumentation, cleanup later.
- **Verification:** live user test yields exact path.
- **Screen:** survives as fallback if COA-2 does not address observed behavior.

## 5. Wargame

### COA-2 fought

| Move | Expected observation | Most-likely failure and cause | Counter-move |
|------|----------------------|-------------------------------|--------------|
| Add optional `selectingAutorotAction` parameter to `InvokeCombo` | Existing execution callers default to false | A real probe caller is missed | Search all `InvokeCombo` call sites and explicitly tag probe calls |
| Pass `selectingAutorotAction: true` from `actCheck` | Heal readiness dry-run still suppresses side-effect branches | Some other dry-run exists outside `actCheck` | Add explicit flag at that call site when found |
| Preserve SGE DPS deny list | SGE DPS execution can use Psyche/Phlegma but not Kerachole/Rhizomata/etc. | SGE still emits a heal via an unblocked action ID | Trigger COA-3 instrumentation |
| Build/deploy | WHM oGCDs should return in live dummy test | Lone Medica III remains | Trace heal-lane gate separately |

**Red-team verdict:** COA-2 changes semantics of a shared helper, but the current helper name and observed behavior show it is overbroad. Structural tests are required to keep probe safety explicit.

### Decision matrix

| Criterion (weight) | COA-0 | COA-1 | COA-2 | COA-3 |
|--------------------|-------|-------|-------|-------|
| Restores DPS oGCDs (4) | 0 | 3 | 4 | 1 |
| Preserves probe safety (4) | 4 | 1 | 4 | 4 |
| Minimal blast radius (3) | 4 | 1 | 4 | 2 |
| Explains AST/SCH contrast (2) | 0 | 2 | 4 | 2 |
| Testability (2) | 0 | 2 | 4 | 2 |
| **Total** | **28** | **27** | **60** | **32** |

## 6. Decision Record

- **Selected:** COA-2 - split readiness probe context from actual execution.
- **Why losers lost:** COA-0 ignores new evidence; COA-1 is a per-job patch trap; COA-3 is useful only after the semantic bug is fixed.
- **Residual risk accepted:** WHM Medica III may remain as a separate heal-lane false positive and would need instrumentation if live retest confirms it.
- **Orders:** battle plan at `wargames/healer-autorot-probe-execution-split-battle-plan.md`.

## 7. Supervision And After-Action

- **Execution log:** pending.
- **Reviewer verdict:** pending.
- **Quality gate:** pending.
- **Written back:** pending.
