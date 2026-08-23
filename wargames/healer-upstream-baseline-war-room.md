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
# War Room - Healer upstream baseline

**Status:** EXECUTED
**Date opened:** 2026-07-07
**Advisor:** Codex session, role-hat AER fallback
**Battle plan:** [healer-upstream-baseline-battle-plan.md](healer-upstream-baseline-battle-plan.md)

User challenged the prior local patch loop: upstream WrathCombo appears to work, and ParseLord5 is a fork, so the correct baseline is latest upstream behavior before more local iteration.

## 1. Frame

- **Current state:** SGE still spams heals on dummy after several local patches; WHM uses no DPS oGCDs; AST in a trust party is not healing properly.
- **Desired end state:** ParseLord5 healer autorotation behaves like working upstream WrathCombo unless a ParseLord5-specific change is intentionally retained and justified.
- **Five Ws:** user is live-testing healer autorotation; behavior is in `/home/kruillin/Projects/Projects/ParseLord5`; latest upstream WrathCombo is `FETCH_HEAD` from `PunishXIV/WrathCombo`; urgency increased after AST trust-party evidence.
- **In scope:** compare upstream healer action-selection contracts, remove fork-only DPS-heal leakage from AST/WHM DPS buttons, and restore WHM offensive oGCD selection.
- **Out of scope:** wholesale reset of unrelated ParseLord5 manual-queue, diagnostics, and configuration work; user config changes; SCH/SGE healer priority redesign.
- **Constraints:** do not clobber unrelated dirty worktree changes; deploy only after build/test; keep SGE defensive branches suppressed during auto DPS selection.
- **Commander's intent:** make the fork obey upstream's main contract that DPS autorotation does not secretly become a healer-priority selector, while preserving intentional ParseLord5 queue/diagnostic mechanics.

## 2. Recon

### Facts

| # | Fact | Evidence |
|---|------|----------|
| F1 | `origin` points to upstream `https://github.com/PunishXIV/WrathCombo`; `parselord5` points to the fork. | `git remote -v` |
| F2 | Latest fetched upstream commit is `efe5d828bdc29ee62e4ca76a056ae4935a486a77`, dated `2026-06-29`, subject `1.0.4.13`. | `git log -1 FETCH_HEAD` |
| F3 | Upstream has no `TryDpsSingleTargetHealPriority` or `TryDpsAoEHealPriority` helpers. | `git grep ... FETCH_HEAD` returned no matches |
| F4 | ParseLord5 calls those fork-only DPS-heal helpers from AST and WHM DPS combos. | `WrathCombo/Combos/PvE/AST/AST.cs`; `WrathCombo/Combos/PvE/WHM/WHM.cs` |
| F5 | ParseLord5 wraps WHM offensive DPS weaves in `!AutoRotationController.IsSelectingAutorotAction`, which suppresses Assize, Presence of Mind, and Lucid when `InvokeCombo` sets that flag. | `WrathCombo/Combos/PvE/WHM/WHM.cs` |
| F6 | SGE defensive DPS branches are fork-gated by `IsSelectingAutorotAction`; removing that gate wholesale risks returning Druochole/Kerachole instead of offensive DPS actions. | `WrathCombo/Combos/PvE/SGE/SGE.cs` |

### Assumptions

| # | Assumption | Settling check (read-only) | Routing |
|---|------------|----------------------------|---------|
| A1 | AST trust-party healing is worsened by fork-only DPS-heal priority competing with the autorotation heal lane. | Remove AST DPS helper calls and retest trust. | If AST still fails, instrument heal target selection and IncludeNPCs. |
| A2 | WHM no-oGCD is caused by using the defensive selection flag around offensive oGCDs. | Remove the flag from WHM offensive weave blocks only. | If WHM still has no Assize/Presence, instrument WHM combo output. |
| A3 | SGE still needs the defensive flag for DPS execution because otherwise Addersgall-protection branches can win before Psyche. | Keep SGE guards for this pass. | If SGE still spams, instrument SGE chosen action and source branch. |

## 3. Criteria

**Screening:** feasible; suitable; acceptable; distinguishable; complete

| Criterion | Weight | Why it matters here |
|-----------|--------|---------------------|
| Upstream alignment | 5 | The user explicitly wants latest WrathCombo as the baseline. |
| Live behavior risk | 4 | Healer autorotation can wipe content or burn cooldowns if wrong. |
| Minimal clobber risk | 3 | The worktree is dirty and contains unrelated ParseLord5 changes. |
| Explains cross-job reports | 3 | The decision must explain SGE dummy, WHM no-oGCD, and AST trust evidence. |
| Testability | 2 | Static tests can catch reintroduced fork-only DPS-heal calls. |

## 4. Courses Of Action

### COA-0 - Keep patching local symptoms

- **Purpose:** continue from the previous SGE/WHM probe split hypothesis.
- **Contract:** small controller edits only.
- **Work guidance:** split `InvokeCombo` probe semantics.
- **Verification:** WHM dummy retest.
- **Screen:** killed. It no longer addresses AST trust evidence and ignores the upstream baseline.

### COA-1 - Wholesale reset healer files to upstream

- **Purpose:** make ParseLord5 healer code match WrathCombo exactly.
- **Contract:** replace controller, party, and healer job files with `FETCH_HEAD` versions.
- **Work guidance:** checkout upstream files, then reapply ParseLord5 branding/build fixes.
- **Verification:** build and live retest.
- **Screen:** killed. It is too broad and would clobber unrelated ParseLord5 manual-queue, diagnostics, and local work.

### COA-2 - Selective upstream contract restoration

- **Purpose:** remove fork-only DPS-heal selection from AST/WHM DPS buttons and stop using the defensive selection flag to suppress WHM offensive oGCDs.
- **Contract:** touch only AST/WHM combo files and focused tests; keep SGE defensive gating and controller queue work intact.
- **Work guidance:** delete DPS-heal helper calls from AST/WHM DPS invoke paths; change WHM offensive weave conditions to use normal weave readiness.
- **Verification:** tests assert AST/WHM DPS files no longer call DPS-heal priority and WHM offensive weave blocks are not gated by `IsSelectingAutorotAction`.
- **Screen:** survives. It follows upstream behavior without a destructive reset.

### COA-3 - Instrument then decide

- **Purpose:** collect live action-source evidence before any more edits.
- **Contract:** add temporary tagged logs around healer target and chosen actions.
- **Work guidance:** deploy instrumentation DLL, retest dummy/trust, then patch.
- **Verification:** log evidence identifies branch.
- **Screen:** survives as fallback if COA-2 does not fix live behavior.

## 5. Wargame

### COA-2 fought

| Move | Expected observation | Most-likely failure and cause | Counter-move |
|------|----------------------|-------------------------------|--------------|
| Remove AST DPS-heal helper calls | AST DPS combo no longer injects heals outside the heal lane | AST still heals poorly in trusts because trust NPCs are not counted correctly | Instrument `GetPartyMembers`, `HealTargets`, and `CanAoEHeal` |
| Remove WHM DPS-heal helper calls | WHM DPS button follows upstream-style DPS flow | WHM still casts Medica III from heal lane | Trace `needsHeal`, `aoeheal`, and `actCheck` |
| Allow WHM offensive weaves while selection flag is set | WHM can use Assize/Presence/Lucid again | WHM burns Afflatus Rapture from DPS path | Keep lily/raidwide defensive guards in place |
| Keep SGE defensive selection guards | SGE offensive oGCDs are not blocked by Addersgall-protection returns | SGE still returns a blocked heal and skips Psyche | Instrument SGE branch order or convert SGE to an explicit offensive-priority pass |

**Red-team verdict:** COA-2 is not a full upstream reset, but that is the point: it restores the upstream contract at the failure boundary while avoiding unrelated data loss.
**Second-order effects:** leftover unused helper methods may remain; if tests pass and behavior improves, delete them in a cleanup pass.

### Decision matrix

| Criterion (weight) | COA-0 | COA-1 | COA-2 | COA-3 |
|--------------------|-------|-------|-------|-------|
| Upstream alignment (5) | 0 | 5 | 4 | 1 |
| Live behavior risk (4) | 1 | 2 | 4 | 4 |
| Minimal clobber risk (3) | 4 | 0 | 4 | 3 |
| Explains cross-job reports (3) | 1 | 4 | 5 | 3 |
| Testability (2) | 1 | 2 | 4 | 2 |
| **Total** | **13** | **30** | **58** | **30** |

## 6. Decision Record

- **Selected:** COA-2 - selective upstream contract restoration.
- **Why losers lost:** COA-0 keeps guessing locally; COA-1 is too destructive for this dirty fork; COA-3 is the next fallback, not the first move after a clear upstream divergence.
- **Residual risk accepted:** SGE may need a second pass because upstream itself allows some defensive SGE DPS branches; this pass keeps the fork's SGE defensive gate while restoring WHM/AST upstream-like behavior.
- **Orders:** battle plan at `wargames/healer-upstream-baseline-battle-plan.md`.

## 7. Supervision And After-Action

- **Execution log:** removed AST/WHM DPS-combo calls to fork-only DPS-heal priority helpers; allowed WHM offensive weave blocks to run without the defensive selection flag; left SGE defensive selection guards intact.
- **Re-convene events:** none during implementation; live retest may trigger AST trust, SGE spam, or WHM Medica instrumentation forks.
- **Reviewer verdict:** role-hat review passed: edits match COA-2 scope and do not wholesale overwrite upstream/fork files.
- **Quality gate:** `python /home/kruillin/Projects/Projects/quality-gate/gate.py normal --repo /home/kruillin/Projects/Projects/ParseLord5 --task healer-upstream-baseline` failed because configured Windows commands require missing `powershell` and unrelated older wargame docs are missing metadata.
- **Written back:** no AGENTS workflow change.
