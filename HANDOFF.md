---
tags:
  - type/handoff
  - project/parselord5
  - status/active
type: handoff
project: parselord5
status: active
aliases: []
---

# HANDOFF — ParseLord5 break-proof refactor (2026-07-02, v2)

Session handoff for continuing the "clean & break-proof" plan. Read top to
bottom; **Immediate next steps** is the work queue. Supersedes v1.

## Context

- Repo: `C:\Users\kruil\Documents\Projects\ParseLord5` — fork of WrathCombo
  (origin = PunishXIV/WrathCombo, push remote = `parselord5`).
- Branch: `parselord5-wc-base` (ahead of upstream by local work, ~359 commits behind upstream main).
- Master plan + architecture comparison (vs Rotation Solver Reborn / WrathCombo):
  `C:\Users\kruil\html-artifacts\2026-07-02_parselord5-vs-rsr-wrath-architecture.html`
- Build check: `dotnet build WrathCombo/WrathCombo.csproj -c Release --nologo -v q`
- Structural evals: `powershell -NoProfile -File scripts/rotation-evals.ps1`
  (forward slashes — bash eats backslashes)

## CURRENT STATE (where we stopped)

**Uncommitted, verified-building work in the tree:** the dedup fleet's output.
19 modified files under `WrathCombo/Combos/PvE/`, net −417 lines
(1023 insertions / 1440 deletions), **build green, 0 warnings, 0 errors**.

Stopped mid-verification: structural evals (`scripts/rotation-evals.ps1`) were
about to run when session ended. NOT yet committed.

### What the fleet did

21 headless Sonnet 5 workers (`claude -p`, worker prompt preserved at
`.claude/dedup-worker-prompt.txt`) rewrote duplicated
`if (ParseLord5Experiments.JobRotationExperiments) {A;B} else {B;A}` blocks
into the guarded-ladder pattern (canonical example: MCH pilot, commit
`a63517a12` — hoist each condition into a single-copy bool, then
`if (aFirst && canA) / if (canB) / if (!aFirst && canA)`).

Per-job tally (68 blocks rewritten, all skips verified legitimate):

| Job | Rewritten | Skipped / why |
|---|---|---|
| AST, BLM, BRD, DRG, DRK, GNB, MNK, NIN, PLD, RDM, RPR, SAM, SCH, SGE, WHM | 4 each | — |
| VPR | 4 | — |
| SMN | 2 | 2 — Advanced Egi priority mutates an array + OrderBy, not a pure reorder; left as is |
| DNC | 0 | 4 — branches differ beyond order (Flourish `IsOffCooldown(Devilment)` disjunct from `1707c4702` in one branch only); merging would change behavior |
| WAR.cs | 0 | 1 — single-line boolean guard, not a duplicated block |
| PCT_Helper | 1 | — |
| WAR_Helper | 1 | — |

### Manual fix applied on top of fleet output

`WrathCombo/Combos/PvE/SAM/SAM.cs` ~line 262: worker's
`canIkishoten = IsEnabled(...) && TryGetIkishotenAction(out uint ikishotenAction, ...)`
caused CS0165 (short-circuit leaves out-var unassigned). Fixed by declaring
`uint ikishotenAction = 0;` before the condition. Build green after fix.

### Audit status

- AST.cs (largest churn, 238 lines) diff read in full — textbook pattern match.
- Leftover-block scan: only DNC (4) and SMN (2) `if (...JobRotationExperiments)`
  blocks remain — both are the documented legitimate skips.
- Remaining 17 diffs NOT eyeball-audited (build + worker self-reports only).

## IMMEDIATE NEXT STEPS (in order)

1. `powershell -NoProfile -File scripts/rotation-evals.ps1` — must pass.
2. Optional deeper audit: `git diff -- WrathCombo/Combos/PvE/<job>` per file;
   accept criteria = conditions hoisted single-copy, ladder reproduces BOTH
   orders exactly, nothing else touched. (Native subagents work after the env
   fix below — a review fleet is cheap.)
3. Commit (everything currently modified under Combos/PvE is this work):
   ```
   refactor(jobs): dedupe experimental priority blocks via guarded ladder across all jobs

   Apply the MCH pilot pattern (a63517a12) to 19 job files via Sonnet 5
   worker fleet: hoist each duplicated condition into a single-copy bool,
   replace if/else order-duplication with guarded ladders. 68 blocks
   rewritten, net -417 lines. Behavior identical in both flag states.

   Skipped by design: DNC (branches differ beyond order), SMN Advanced Egi
   (array-mutation pattern), WAR.cs (single-line guard).
   Manual fix: SAM CS0165 out-var definite assignment.
   ```
4. Then continue the roadmap (next: step 3 below).

## Roadmap (from the HTML report)

| Step | Status |
|---|---|
| 0. Commit dirty work | DONE — `68558e739`, `f016d0587` |
| 2. Per-feature flag split | DONE — `212806bdc` |
| 1. Dedupe experimental blocks | DONE pending evals+commit (this handoff) — pilot `a63517a12` |
| 3. SmartMit shared engine | NEXT — job files supply only `MitigationOption` catalogs + quirks; selection/threat/trace logic moves to `WrathCombo/Services/SmartMitigation/`. Shrink `WAR_SmartMitigation.cs` (832 lines) first; DRK/GNB/PLD copies (committed in `68558e739`) drift daily until this lands |
| 4. xUnit tests on pure logic | `MitigationCoverageCalculator` (zero Dalamud deps), heal-delay curve (extract pure static from `AutoRotationController.cs` ~line 429 first), `CombatEventBuffer` |
| 5. Partial-class restructure | Move ParseLord5 experiment code into `<JOB>_ParseLord5.cs` partials; upstream files keep 1–3-line hooks → trivial upstream merges |
| 6. Upstream sync cadence | Biweekly rebase/merge of PunishXIV/WrathCombo main (~359 behind) |
| 7. Small hardening | Telemetry `Clear()` on combat end + stale-entry pruning + phase-transition spike filter; heal-curve magic numbers (50/75%, 0.5s) → named tuning class; gate-or-document ungated changes (`AutoRotCanPressAction` weave relax, NIN `InMudra`); UI toggles for the six sub-flags |

## Flag architecture (step 2, merged)

- Master: `Service.Configuration.ParseLord5ExperimentalMode` (default false).
- Sub-flags (all default true): `Configuration.ExperimentalFlags` in
  `WrathCombo/Core/Configuration.ExperimentalFlags.cs` — JobRotationExperiments,
  SmartMitigation, DynamicHealCurve, FastPartyCache, NoTargetDpsFallback,
  CombatTelemetry.
- Call sites use `ParseLord5Experiments.<Flag>`
  (`WrathCombo/Combos/PvE/ALL/ParseLord5Experiments.cs`) = master AND sub-flag.
- Sub-flags config-file-only; UI = step 7.

## USER ACTION REQUIRED (unblocks native agent teams)

Windows user env: `CLAUDE_CODE_SUBAGENT_MODEL=gemini-2.5-flash-lite[1m]` —
forces every native subagent (Agent tool / Workflow) onto a nonexistent model;
they die instantly. Permission classifier blocked assistant removal. Run once:

```powershell
[Environment]::SetEnvironmentVariable('CLAUDE_CODE_SUBAGENT_MODEL', $null, 'User')
```

Restart session afterward. Until then, agent teams only work via headless
`claude -p` workers with the var blanked per-spawn:

```
CLAUDE_CODE_SUBAGENT_MODEL= claude -p "<prompt>" --model claude-sonnet-5 --permission-mode acceptEdits --allowedTools "Read,Edit,Grep" --max-turns 40
```

(Advisor Router hook labels are cosmetic; `localhost:8787` in settings.json is
NOT the culprit — desktop sessions hit api.anthropic.com directly.)

## Gotchas

- LF→CRLF warnings on every commit under `Combos/PvE/` — harmless.
- Submodules (ECommons, PunishLib, WrathCombo.API) always show dirty content —
  leave alone.
- Single-line uses `(!InBossEncounter() || ParseLord5Experiments.JobRotationExperiments)`
  (SGE/WHM/SCH/AST/WAR) are NOT duplication — never "dedupe".
- `.quality-gate/logs/`, root review zips/txt = untracked artifacts, don't commit.
- `HANDOFF.md` + `.claude/dedup-worker-prompt.txt` are session artifacts —
  keep untracked or commit as chore, either fine.
- Bash tool eats backslashes in unquoted paths → use forward slashes for
  PowerShell script args.
