# ParseLord5 Cleanup Plan

Scope: the local checkout `C:\Users\kruil\Documents\Projects\ffxiv-tools\ParseLord5` only.
Baseline: `e9b9d1789`, `dotnet test -c Release` = Failed 2, Passed 54.
GitHub repo state, the `orca` checkout, and upstream sync are explicitly out of scope.

Existing advisor plans `001`-`005` are absorbed here. Where a phase says "execute NNN",
run that plan as written; where it says "supersedes NNN", this document replaces it.

## Decision gate (blocks Phase 3)

**D1 — What happens to the experiment flag system?**

`WrathCombo/Combos/PvE/ALL/ParseLord5Experiments.cs` exposes six gated accessors.
Call sites in the plugin: zero. `ParseLord5ExperimentalMode` is read only by that dead
accessor, a migration comment (`ConfigurationHelper.cs:461`), and a Settings string
(`Settings.cs:183`). Every promoted fork feature runs unconditionally.

- **O1 Retire.** Delete `ParseLord5Experiments.cs`, `Configuration.ExperimentalFlags.cs`,
  and `Configuration.ParseLord5ExperimentalMode`. Fork behavior becomes the product.
  Cheapest, honest, loses per-feature bisect.
- **O2 Re-wire.** Restore a runtime off-switch at each promoted feature's call site
  (SmartMit, heal curve, telemetry, probe split, ActionStacks peek). Preserves the
  roadmap guardrail. Costs a pass over five subsystems.
- **O3 Narrow.** Keep only the master switch as a single kill-switch that forces every
  fork-only path to baseline, drop the six per-feature flags.

Nothing in Phase 3 starts until D1 is answered. Phases 1, 2, 4 and 5 are unblocked.

---

## Phase 1 — Get the tree green (P1, half a day)

The suite is red at HEAD and the red is hiding a live bypass. Nothing else lands first.

1. **Execute plan 001** — `plans/001-keep-sge-blocklist-after-asex-peek.md`.
   `AutoRotationController.cs:1317` and `:1415` read
   `if (!asRedirected && !CanUseAutorotDpsAction(outAct))`, so any ActionStacks redirect
   skips the SGE defensive blocklist. Fix the condition first.
2. **Execute plan 003** — `plans/003-clear-overridetarget-on-early-return.md`.
   Same methods, do it in the same branch but a separate commit.
3. **Execute plan 002** — `plans/002-repair-structural-lints.md`, after 001 so the
   repaired regexes encode the fixed shape, not the bypass.

**Exit:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` = 0 failed.

## Phase 2 — Replace text-matching lints with a testable decision (P1, 1-2 days)

Both Phase 1 failures were `Assert.Matches` against controller source text. They broke
on an edit that compiles and reads fine, and they could not see the bypass at all.

1. Extract the peek-plus-blocklist decision into a pure, Dalamud-free function:
   `(gameAct, outAct, asRedirected, asResolvedAction, asResolvedTarget, job) -> (action, target, allowed, reason)`.
   Follow the seam that already works here: `Services/HealDelayCurve.cs`,
   `Services/SmartMitigation/MitigationCoverageCalculator.cs`.
2. Call it from all four peek sites — `AutoRotationController.cs` lines 576, 1241, 1301, 1396.
3. Port `RotationStructureTests.AutorotationDpsLane_BlocksSgeDefensiveActions` and
   `AutorotationProbeContext_IsOptIn` from regex assertions to decision assertions.
4. Keep the cheap structural lints that are genuinely structural (preset enum parses,
   IDs unique, every job has presets). Delete regexes that assert on statement adjacency.

**Exit:** an ActionStacks redirect of an SGE defensive action is rejected by a unit test,
with no regex in the assertion.

## Phase 3 — Resolve the flag system (P1, size depends on D1)

Blocked on D1. Whichever option wins, the end state is that the config UI describes
control the code actually implements, and `ParseLord5Experiments.cs` either has real
call sites or does not exist.

If O2 or O3: add a test asserting the master switch reaches at least one fork-only path,
so the system cannot silently die again.

## Phase 4 — Observability that is not WHM-only (P2, 1-2 days)

`TraceWhmHeal` has 30 call sites and opens with `if (Player.Job is not Job.WHM) return;`.
That is the whole surface. CLAUDE.md documents `/pl5 trace`; `Commands.cs` defines
`ParseLord5Command = "/pl5"` and handles no `trace` subcommand anywhere.

1. Generalize the trace to any job, keeping the `EzThrottler` pattern.
2. Emit Phase 2's decision `reason` on every rejection, so "rotation stopped" produces
   a log line instead of a wargame document.
3. Implement the `/pl5 trace` toggle CLAUDE.md already promises, or delete that claim.

## Phase 5 — Make builds and docs identify themselves (P2, 1 day)

1. **Execute plan 004** — `plans/004-stamp-devplugin-checkout.md`.
   `WrathCombo.csproj:64` writes Debug and Release straight to
   `%AppData%\XIVLauncher\devPlugins\ParseLord5\` with `<Version>1.0.4.13</Version>`
   inherited from upstream. Stamp commit SHA, branch, dirty flag, and source path;
   surface them in the plugin window.
2. **Execute plan 005** — `plans/005-correct-stale-agent-docs.md`, extended:
   - `docs/AGENTS_FULL.md:44` tells agents in bold to build from `orca\ParseLord5`.
     Correct it to this checkout.
   - All 41 files in `docs/` are `status: active`, including 20 dated 20260517 experiment
     reports. Move dated reports to `status: archived`.
   - 15 files in `wargames/` cite paths that do not exist on this machine. Mark them
     historical or archive them.
   - Decide whether `plans/` is tracked or ignored; it is untracked today.
3. Prune dead git state: 5 of 6 registered worktrees are `prunable` and point at
   `Documents\Projects\ParseLord5`, a path that no longer exists. Run
   `git worktree prune` and delete the stale `claude/*` branches, per the AGENTS.md
   main-plus-one rule.

---

## Order and parallelism

```
Phase 1  ──> Phase 2  ──> Phase 4
   │            │
   │            └──> (Phase 3 after D1)
   └──────────────> Phase 5  (independent, any time)
```

Phase 5 can run in parallel with everything. Phase 4 depends on Phase 2 because the
trace should print the decision reason rather than re-deriving it.

## Verification per phase

| Phase | Command | Expected |
|---|---|---|
| 1 | `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` | 0 failed |
| 2 | same | 0 failed, SGE-redirect case covered without regex |
| 3 | same + manual toggle check in `/wrath` settings | flag state changes observable behavior, or flag is gone |
| 4 | build, `/pl5 trace` on a non-WHM job | decision lines in the Dalamud log |
| 5 | `git worktree list`, `git branch`, plugin window | no prunable worktrees, main + at most one branch, build stamp visible |

## Branch constraint

AGENTS.md allows main plus at most one work branch. Use a single branch for all phases;
commit per phase step so a bad phase can be reverted without unwinding the rest.
