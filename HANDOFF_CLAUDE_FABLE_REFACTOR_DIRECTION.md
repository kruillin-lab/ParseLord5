---
tags:
  - type/handoff
  - project/parselord5
  - status/active
type: handoff
project: parselord5
status: active
aliases:
  - Claude Fable Refactor Direction Review
---
# HANDOFF - Claude Fable Refactor Direction Review

Date: 2026-07-02
Repo: `C:\Users\kruil\Documents\Projects\ParseLord5`
Branch observed by Codex: `merge-rehearsal` / `parselord5-wc-base`

This document is an advisory steering note for Claude Fable. Read it alongside
`HANDOFF.md`; do not treat it as a replacement for the operational work queue.

## Executive Verdict

Codex's opinion: the committed refactor direction is an improvement, but the
current working tree is not safe to build or evaluate until the merge rehearsal
conflicts are resolved.

The refactor is improving the codebase because it is moving ParseLord5 from
broad, duplicated, hard-to-bisect job edits toward:

- Per-feature experiment flags under one master kill switch.
- Guarded priority ladders instead of duplicated `if/else` blocks.
- Shared SmartMitigation services instead of four drifting tank copies.
- Pure logic extraction with xUnit tests for behavior that does not need Dalamud.

The danger is not the direction. The danger is letting merge-conflict cleanup,
generated dedupe, and architectural refactor continue in one tangled batch.

## Current State Observed

Codex observed these facts before writing this handoff:

- `HEAD` contains the recent refactor commits:
  - `212806bdc` per-feature ParseLord5 experiment flags.
  - `a63517a12` MCH guarded-ladder pilot.
  - `2117f9cb2` cross-job guarded-ladder dedupe.
  - `2fbabe3c1` WAR shared threat migration.
  - `fa04b2db8` DRK lazy guarded-ladder correction.
  - `484fea180` xUnit tests for SmartMitigation and CombatEventBuffer.
  - `e29bb7763` HealDelayCurve extraction and tests.
- The live worktree is mid-merge and has unresolved conflict markers in 29
  files, including job files, `AutoRotationController.cs`, resources,
  `WrathCombo.cs`, and `WrathCombo.csproj`.
- `dotnet test .\WrathCombo.Tests\WrathCombo.Tests.csproj -c Release --nologo -v q`
  passed: 34 passed, 0 failed.
- Full plugin build was intentionally not run by Codex because unresolved
  conflict markers would make the failure uninformative.

## What Looks Good

Continue these parts of the plan:

- Keep the per-feature flag architecture. It gives you a real bisect surface:
  `JobRotationExperiments`, `SmartMitigation`, `DynamicHealCurve`,
  `FastPartyCache`, `NoTargetDpsFallback`, and `CombatTelemetry` all remain
  behind the master `ParseLord5ExperimentalMode` switch.
- Keep the guarded-ladder dedupe where the conditions are pure. It reduces
  repeated job logic without erasing the flag's old-vs-new priority semantics.
- Keep the selective skips. DNC and SMN were correctly left with explicit
  `if (ParseLord5Experiments.JobRotationExperiments)` blocks where branches
  differ by more than order.
- Keep moving SmartMitigation toward shared services. The shape of
  `MitigationCoverageCalculator`, `TankSmartMitigationThreat`,
  `TankMitigationSelection`, and `TrashMitigationOrdering` is better than
  letting WAR, DRK, GNB, and PLD drift separately.
- Keep extracting pure functions with tests. `HealDelayCurve` is a good model:
  small, deterministic, easy to test, and insulated from Dalamud.

## Risks To Control Before More Refactor

Treat these as steering constraints:

1. Resolve the merge rehearsal before judging behavior.
   The current tree has conflict markers, so do not use it as evidence that
   the refactor builds, fails, or behaves correctly.

2. Wire the new tests into normal verification.
   `WrathCombo.Tests` exists and passes, but `WrathCombo.slnx` does not include
   it. Either add it to the solution or make `quality-gate.json` / CI run
   `dotnet test .\WrathCombo.Tests\WrathCombo.Tests.csproj` explicitly.

3. Audit guarded-ladder hoists for side effects.
   DRK is corrected properly: `TryGetAction<Cooldown>` stays lazy because it
   mutates cooldown-tracking state. Apply that standard everywhere. Hoisted
   `canX` variables are acceptable only when they are pure predicates or pure
   out-param calculators.

4. Avoid a broad partial-class restructure until upstream conflict pressure is
   understood.
   The partial-class idea may be valuable, but doing it while the tree is
   already full of upstream merge conflicts risks creating churn that hides
   behavioral mistakes.

5. Do not continue SmartMitigation by copy-paste.
   The next tank work should reduce job-local selection/threat/trace logic,
   not add another layer of copied bridge code.

## Recommended Steering Order

Recommended next direction for Claude Fable:

1. Stabilize the worktree.
   Finish the merge rehearsal, remove all conflict markers, and get
   `git diff --check` clean enough that remaining whitespace is intentional.

2. Restore verification as a first-class signal.
   Run the plugin build, run `scripts/rotation-evals.ps1`, and run the xUnit
   tests. If the test project is not in the solution, either wire it in or
   document the explicit command in the gate.

3. Do a targeted semantic audit of the guarded-ladder dedupe.
   Focus on jobs where `TryGet...`, retargeting, action selection helpers, or
   mutable static state appear in hoisted conditions. DRK is the reference
   pattern for lazy evaluation.

4. Continue the SmartMit shared engine, but shrink before expanding.
   Prefer moving threat detection, coverage selection, fallback selection, and
   trace formatting into shared services before adding more per-job special
   cases. Job files should mostly provide catalogs and genuine quirks.

5. Expand pure tests around the shared engine.
   Add tests for selection fallbacks, long-mitigation exclusion, heavy-mit
   preference, TCH danger levels, and heal-delay boundary behavior before
   broadening behavior in job files.

6. Only after the above, revisit the partial-class restructure.
   The goal should be upstream merge hygiene: upstream files get tiny hooks,
   ParseLord5 behavior moves into ParseLord5-owned partials, and behavior stays
   guarded by the per-feature flags.

## Suggested Acceptance Criteria

Before calling this refactor healthier than the starting point, require:

- No unresolved merge markers: `git diff --check` has no conflict-marker output.
- Build passes: `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`.
- Domain evals pass: `powershell -NoProfile -File scripts\rotation-evals.ps1`.
- Pure tests pass: `dotnet test .\WrathCombo.Tests\WrathCombo.Tests.csproj -c Release`.
- Only DNC and SMN retain full explicit `if (ParseLord5Experiments.JobRotationExperiments)` blocks for the documented semantic-skip reasons.
- Any hoisted condition that calls a helper is confirmed side-effect-free, or
  rewritten as a lazy ladder.
- SmartMit tank changes reduce duplicated selection/threat logic rather than
  copying more per-job behavior.

## Bottom Line

Steer toward consolidation plus tests, not more broad churn. The best next move
is to make the merge rehearsal boring and verifiable, then continue extracting
shared SmartMitigation logic behind the existing feature flags. The architecture
is improving; the process needs a tighter stabilization checkpoint before the
next large refactor step.
