---
tags:
  - type/plan
  - project/parselord5
  - status/executed
type: plan
project: parselord5
status: executed
aliases: []
---
# Battle Plan - Encounter Awareness: predictive healer raidwide count + read-only IPC exposure

**Origin:** direct suggestion (2026-08-15) — generalization of the landed mechanic-cast-tracker work
(`pl5-mechanic-cast-tracker-battle-plan.md`, EXECUTED 2026-08-13) into a second consumer plus a new IPC surface.
Design exploration: `2026-08-15_parselord5-encounter-awareness-service.html` (local artifact, not in-repo).
No war room convened — same precedent as the mechanic-cast-tracker plan (first artifact for this slice).
**Status:** EXECUTED 2026-08-15 on `main`. All 5 moves applied; build 0/0, 67 tests pass, evals 14/14, both red
checks confirmed, quality gate PASS_WITH_WARNINGS (baseline test-hack notices only). One human step outstanding:
the §3 manual in-game tests (M1 flag-on pre-shield / M2 flag-off rollback / M3 IPC readout).
**Tracking:** [#8](https://github.com/kruillin-lab/ParseLord5/issues/8) — approved 2026-08-15, executed.
**Never clobber:** sibling to `pl5-mechanic-cast-tracker-battle-plan.md`, `pl5-native-action-stacks-battle-plan.md`,
`parselord5-reload-crash-battle-plan.md`, `parselord5-stability-battle-plan.md`, and the healer war rooms.
Different mission; do not merge.

---

## 0 · Theatre map

- **Repo:** `C:\Users\kruil\orca\ParseLord5` (Windows). Exactly one branch, `main` (`AGENTS.md` forbids more).
- **Tree state at plan time (2026-08-15, verified):** `git status --porcelain` is **dirty** — the mechanic-cast-tracker
  feature (`WrathCombo/Services/MechanicPrediction/*`, edits to `WrathCombo.cs`, `TankSmartMitigationThreat.cs`,
  `ParseLord5Experiments.cs`, `Configuration.ExperimentalFlags.cs`, plus test files) is sitting uncommitted on
  `main` at HEAD `e367c8220`. This is **expected, pre-existing state — do not revert it.** It is exactly the code
  this plan builds on.
- **Baselines (2026-08-15, verified on the dirty tree above):**
  - `dotnet build WrathCombo/WrathCombo.csproj -c Release` → 0 errors, 0 warnings.
  - `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` → Passed 64, Failed 0.
  - `pwsh -NoProfile -File scripts/rotation-evals.ps1` → passed=14 failed=0 total=14.
  - Re-verify at HEAD before Move 1 (`git log --oneline -1`, `git status --porcelain`) — abort on drift per MOVE 0.
- **Reference behavior already in tree (reuse, do not reinvent):**
  - `WrathCombo/Services/MechanicPrediction/MechanicCastClassifier.cs` — pure classifier, `MechanicCastKind`
    {`None`, `Raidwide`, `Tankbuster`, `Cleave`}, `MechanicCastPrediction { Kind, TimeToImpactSeconds,
    PredictedSpikeFraction, CastActionId }`. Zero plugin-framework imports (test-linked).
  - `WrathCombo/Services/MechanicPrediction/MechanicCastTracker.cs` — Dalamud-facing scanner. `Update()` runs every
    frame from `WrathCombo/WrathCombo.cs:379-380` (after `CombatTelemetryService.Update()`), no-ops entirely unless
    `ParseLord5Experiments.PredictiveMechanics` is on (`_last = default`, line 26-28). Exposes `Last` (the full
    `MechanicCastPrediction`), `PredictedSpikeFraction()`, `HasImminentImpact(float withinSeconds)`.
  - `WrathCombo/Combos/PvE/ALL/ParseLord5Experiments.cs:33-34` — `PredictiveMechanics` flag accessor already
    exists, master-gated. **Reuse this flag verbatim — do not add a new one.** This feature is the second consumer
    of the same prediction, not a new experiment.
  - `WrathCombo/Services/SmartMitigation/TankSmartMitigationThreat.cs:53-54` — the existing consumer, for pattern
    reference: `if (ParseLord5Experiments.PredictiveMechanics) mechanicSpikeFraction = Math.Max(mechanicSpikeFraction,
    MechanicCastTracker.PredictedSpikeFraction());` — additive, never downgrades the reactive fallback.
  - `WrathCombo/AutoRotation/AutoRotationController.cs:751-782` `HandleRaidwide(bool multihit)` — the healer
    raidwide-heal dispatcher. `numberOfCasts` (lines 758-763) is chosen **purely from current party-average HP**
    (`GetPartyAvgHPPercent() switch { <=30 => 3, <=60 => 2, _ => 1 }`) — no time-to-impact or magnitude input.
  - `WrathCombo/AutoRotation/AutoRotationController.cs:526` — the gate that calls `HandleRaidwide`:
    `if (ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming(out var multi))`. **Do not touch this line or
    `ShouldHandleHealerRaidwides` (line 646-655)** — `RotationStructureTests.cs` (~line 76) asserts the exact
    substring `"ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming"` and this plan must not break it.
  - `WrathCombo/CustomCombo/Functions/Action.cs:367-404` — `GroupDamageIncoming`/`RaidwideCasting` are **already**
    cast-bar/ETA-aware (throttled 100ms, `maxTimeRemaining` param) and are used at 50+ call sites across nearly
    every job (Feint, Addle, Reprisal, Third Eye, Arcane Crest, Troubadour, Shield Samba, healer raidwide heals).
    **Correction vs the 2026-08-15 design exploration:** that artifact characterized the healer raidwide path as
    "purely reactive." It is not — the *gate to enter* `HandleRaidwide` is already ETA-aware via `RaidwideCasting`.
    What is actually missing, and what this plan targets, is narrower and more precise: the **cast-count decision
    inside** `HandleRaidwide` (how many shields to spend) still only looks at current HP, with no forward-looking
    magnitude. `RaidwideCasting`/`GroupDamageIncoming` return a bool ("is something incoming"); they cannot tell
    the count logic *how big* the hit is predicted to be — only `MechanicCastTracker`'s spike-fraction classifier
    does that. This is the real, narrow gap.
  - **Architectural note, out of scope for this slice:** `RaidwideCasting` and `MechanicCastTracker.Update()`
    independently re-scan `Svc.Objects` for hostile casters with near-identical cast-type/effect-range filters
    every frame. Unifying them would touch the ~50 call sites of `GroupDamageIncoming` across every job — too broad
    for a first slice per `docs/ParseLord5_Roadmap.md` ("job tuning should happen one job at a time"; "do not
    rewrite architecture up front"). Logged under Deferred (§5).
  - `WrathCombo/Services/IPC/Provider.cs:335-339` `GetAutoRotationState()` — the pattern to copy for the new
    getters: `[EzIPC]` + `[SuppressMessage("Performance", "CA1822:Mark members as static")]` on a plain public
    method, no lease required for a read-only getter.
  - `docs/IPC.md` — current IPC surface is **control-only** (auto-rotation state, combo state, variant options).
    Zero telemetry/prediction getters exist today.
- **Test linking (load-bearing):** `WrathCombo.Tests/WrathCombo.Tests.csproj` — `MechanicCastClassifier.cs` is
  already linked (line 28). `MechanicCastTracker.cs` and `Provider.cs` are **not** test-linkable (Dalamud imports);
  cover their contracts with structural (source-text) tests in `RotationStructureTests.cs`, matching the existing
  `PredictiveMechanics_SpikeFeedIsDefaultOffAndMasterGated` / `PredictiveMechanics_LandedHitPathRemainsAsFallback`
  pattern (lines 205-227).

### Mission (one sentence)

Give the healer raidwide-heal count decision the same forward-looking spike magnitude tank smart mitigation
already has, and expose that same prediction read-only over IPC — both as pure additive consumers of the
already-shipped `MechanicCastTracker`, with zero new config surface.

### Explicit non-goals (first slice)

- No new experimental flag — reuse `ParseLord5Experiments.PredictiveMechanics` verbatim.
- No change to `ShouldHandleHealerRaidwides` or the `HandleRaidwide` call-site gate (line 526) — only the
  `numberOfCasts` computation inside `HandleRaidwide` changes.
- No DPS burst-window automation (`/wrath burst` is a manual command today; auto-gating it on encounter state
  changes command semantics and is a separate, larger-blast-radius plan).
- No unification of `RaidwideCasting`/`GroupDamageIncoming`'s scanning with `MechanicCastTracker`'s scanning — real
  duplication, but ~50 call sites across every job is out of scope for one slice (§5, deferred).
- No BossMod/Reborn timeline IPC consumption (Slice 3 from the original mechanic-cast-tracker plan, still
  deferred — unverified external provider shape).
- No Settings UI toggle — matches the existing `PredictiveMechanics` promotion pattern (flag stays master-gated,
  no UI until play-proven).
- No lease/write surface on the new IPC methods — read-only getters only, cannot be used by an external plugin to
  change ParseLord5 behavior.

---

## 1 · Design contract

### Healer feed (the behavior change)

In `HandleRaidwide` (`AutoRotationController.cs:751-782`), after the existing HP-based `numberOfCasts` switch,
add one additive line, same `Math.Max` shape as the tank consumer — never *downgrades* the reactive fallback:

```csharp
int numberOfCasts = GetPartyAvgHPPercent() switch
{
    <= 30 => 3,
    <= 60 => 2,
    _ => 1
};

if (ParseLord5Experiments.PredictiveMechanics &&
    MechanicCastTracker.HasImminentImpact(MechanicCastKind.Raidwide, PredictiveHealerLeadSeconds))
    numberOfCasts = Math.Max(numberOfCasts, 2);
```

- `PredictiveHealerLeadSeconds` is a new `private const float` on `AutoRotationController`, value `3f` — inside
  the classifier's outer `MaxLeadSeconds` (6f) bound, chosen so shields start landing shortly before impact rather
  than so early they're wasted on a mechanic that gets reset/moved. Tunable via TRIGGER-1 (§4) without touching
  the classifier itself.
- Gated on `MechanicCastKind.Raidwide` specifically — `Tankbuster`/`Cleave` predictions must not inflate the
  party-wide raidwide-heal count; those are single-target mechanics.
- Only ever raises the count (`Math.Max`), so a party already below 60% HP still gets 2-3 casts exactly as today;
  the only new behavior is a *healthy* party (current logic would say 1 cast) getting a second shield down ahead
  of a predicted big raidwide.

### `MechanicCastTracker` — one convenience overload (mirrors existing `HasImminentImpact(float)`)

```csharp
internal static bool HasImminentImpact(MechanicCastKind kind, float withinSeconds) =>
    _last.Kind == kind && _last.TimeToImpactSeconds <= withinSeconds;
```

No other change to `MechanicCastTracker`/`MechanicCastClassifier` — `Last`, `Update()`, and the existing
`HasImminentImpact(float)` are untouched.

### IPC exposure (new, read-only)

Add four getters to `WrathCombo/Services/IPC/Provider.cs`, same shape as `GetAutoRotationState()`. No lease
parameter — these are pure reads of already-computed state, safe to call unconditionally:

```csharp
[EzIPC]
[SuppressMessage("Performance", "CA1822:Mark members as static")]
public bool GetNextMechanicImminent(float withinSeconds) =>
    MechanicCastTracker.HasImminentImpact(withinSeconds);

[EzIPC]
[SuppressMessage("Performance", "CA1822:Mark members as static")]
public string GetNextMechanicKind() =>
    MechanicCastTracker.Last.Kind.ToString();

[EzIPC]
[SuppressMessage("Performance", "CA1822:Mark members as static")]
public float GetNextMechanicTimeToImpact() =>
    MechanicCastTracker.Last.TimeToImpactSeconds;

[EzIPC]
[SuppressMessage("Performance", "CA1822:Mark members as static")]
public float GetPredictedMechanicSpikeFraction() =>
    MechanicCastTracker.PredictedSpikeFraction();
```

- No internal re-check of `ParseLord5Experiments.PredictiveMechanics` needed here: `MechanicCastTracker.Update()`
  already no-ops to `_last = default` when the flag is off, so these naturally return `"None"` / `0f` / `false`
  when the feature is disabled — same "inert unless master flag on" contract as everything else in this family.
- `MechanicCastKind` and `MechanicCastTracker` are `internal` — fine, `Provider` lives in the same assembly.
  External plugins only ever see the `string`/`float`/`bool` return values, never the internal enum type.
- Requires `using WrathCombo.Services.MechanicPrediction;` added to `Provider.cs`.
- Update `docs/IPC.md` "Capabilities of the Wrath Combo IPC" list to add a new bullet: "Predictive mechanic
  timing (read-only, ParseLord5-only extension)" under the existing capability list, and a short new subsection
  documenting the four methods (mirrors the existing method-by-method doc style at IPC.md:182-272). Note this is a
  **ParseLord5 extension** beyond the upstream WrathCombo IPC surface — do not claim it exists under the
  `"WrathCombo"` IPC prefix.

---

## 2 · Move sequence

### MOVE 0 — Preflight baseline (read-only)

```powershell
git branch --show-current                     # expect main
git status --porcelain                         # expect the pre-existing dirty set from §0, nothing else
git log --oneline -1                           # expect e367c8220 (or later, if more landed since)
dotnet build WrathCombo/WrathCombo.csproj -c Release 2>&1 | tail -4   # expect 0 err / 0 warn
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release 2>&1 | tail -3  # expect Passed 64
pwsh -NoProfile -File scripts/rotation-evals.ps1 2>&1 | tail -3       # expect passed=14 failed=0
```

- **TRIGGER — tree dirty with *unexpected* files (beyond the mechanic-cast-tracker set in §0), or build/test off
  the recorded baseline:** stop, reconcile, do not edit (ABORT-ENV).

### MOVE 1 — Tracker convenience overload

- Add `HasImminentImpact(MechanicCastKind kind, float withinSeconds)` to `MechanicCastTracker.cs` exactly as
  contracted above (place directly after the existing `HasImminentImpact(float)` at line 68-69).
- **PASS:** build green. No behavior change — pure addition, unused until MOVE 2.

### MOVE 2 — Healer raidwide predictive feed (the actual behavior change)

- Add `private const float PredictiveHealerLeadSeconds = 3f;` near `HandleRaidwide` in `AutoRotationController.cs`.
- Insert the `Math.Max` block from §1 immediately after the existing `numberOfCasts` switch (after line 763),
  before the `if (AutorotRaidwides >= numberOfCasts) return;` check (line 765).
- Add `using WrathCombo.Services.MechanicPrediction;` to `AutoRotationController.cs`'s using block.
- **Do not touch** line 526 or `ShouldHandleHealerRaidwides` (646-655).
- **PASS:** build green. Default-off (`PredictiveMechanics = false`) → `numberOfCasts` computation byte-identical
  to today; existing `RotationStructureTests.cs` assertion on line 76 (`"ShouldHandleHealerRaidwides(isHealer) &&
  GroupDamageIncoming"`) still passes untouched.

### MOVE 3 — IPC getters

- Add the four `[EzIPC]` methods from §1 to `Provider.cs`, plus the `using WrathCombo.Services.MechanicPrediction;`
  import.
- **PASS:** build green. Grep `GetNextMechanic|GetPredictedMechanicSpikeFraction` over `WrathCombo/Services/IPC/`
  shows exactly the four new method signatures, all `public`, none taking a `Guid` lease parameter.

### MOVE 4 — Docs

- Update `docs/IPC.md`: add the ParseLord5-extension bullet to the capability list, and a short method-by-method
  subsection for the four new getters (mirror the existing style for `GetAutoRotationState()` etc.).
- **PASS:** doc references the correct method names/signatures from MOVE 3 verbatim.

### MOVE 5 — Structural regression tests

Add to `WrathCombo.Tests/RotationStructureTests.cs`, mirroring the existing `PredictiveMechanics_*` tests
(lines 205-227):

- `PredictiveMechanics_HealerRaidwideCountFeedIsAdditiveAndKindScoped` — asserts `AutoRotationController.cs`
  still contains the unchanged base switch (`"<= 30 => 3,"` / `"<= 60 => 2,"` HP thresholds) **and** contains
  `Math.Max(numberOfCasts,` guarded by `ParseLord5Experiments.PredictiveMechanics` **and**
  `MechanicCastKind.Raidwide` (not `Tankbuster`/`Cleave`) in the same statement.
- `PredictiveMechanics_HealerRaidwideGateUnchanged` — re-affirms (belt-and-suspenders alongside the pre-existing
  test) that `"ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming"` is still present verbatim in
  `AutoRotationController.cs` — this plan must never touch that line.
- `IPC_NextMechanicGettersAreReadOnly` — asserts `Provider.cs` contains all four new method signatures
  (`public bool GetNextMechanicImminent(float withinSeconds)`, `public string GetNextMechanicKind()`,
  `public float GetNextMechanicTimeToImpact()`, `public float GetPredictedMechanicSpikeFraction()`) and that none
  of the four signatures contain the substring `Guid` (no lease parameter — read-only contract).

- **PASS:** build + full test suite green; 64 baseline + 3 new tests, 0 failed.

---

## 3 · Verification

All from `C:\Users\kruil\orca\ParseLord5`.

1. **Build:** `dotnet build WrathCombo/WrathCombo.csproj -c Release` → 0 errors, 0 warnings.
2. **Tests:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` → 64 baseline + 3 new structural
   tests, 0 failed.
3. **Evals:** `pwsh -NoProfile -File scripts/rotation-evals.ps1` → `passed=14 failed=0` (no preset touched; must
   stay green).
4. **Red check (healer feed):** temporarily change `MechanicCastKind.Raidwide` to `MechanicCastKind.Tankbuster` in
   the MOVE 2 guard and confirm `PredictiveMechanics_HealerRaidwideCountFeedIsAdditiveAndKindScoped` fails; restore
   immediately.
5. **Red check (IPC contract):** temporarily add a `Guid lease` parameter to one of the four getters and confirm
   `IPC_NextMechanicGettersAreReadOnly` fails; restore immediately.

### Deploy + manual test (operator only)

Release build writes straight to `C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll`.

- **M1 (flag on):** enable `ParseLord5ExperimentalMode` + `PredictiveMechanics`, play a healer (WHM/SCH/AST/SGE)
  against a boss with a telegraphed raidwide while party HP is healthy (>60%). Watch for a second raidwide-heal
  cast landing *before* the hit resolves, where today only one would fire.
- **M2 (flag off):** disable `PredictiveMechanics`; confirm healer raidwide-heal count behaves exactly as the
  pre-change build (HP-threshold only). Rollback check.
- **M3 (IPC):** with a lightweight test harness or the existing `/wrath dbg` IPC debug tab, call
  `GetNextMechanicKind()`/`GetNextMechanicTimeToImpact()` during a telegraphed raidwide and confirm they report
  `"Raidwide"` and a decreasing time-to-impact before the hit, then `"None"` after.

---

## 4 · Trigger forks

- **ABORT-ENV:** build not at baseline before edits, or the tree has unexpected dirt beyond the known
  mechanic-cast-tracker set (§0) — stop, do not edit.
- **ABORT-SCOPE:** if `HandleRaidwide`'s `numberOfCasts` logic has changed shape since this plan was written
  (line numbers drift), re-read the current file before editing; do not guess the insertion point.
- **TRIGGER-1 (predictive heal fires too early / wastes a shield on a mechanic that gets reinterrupted or moved):**
  lower `PredictiveHealerLeadSeconds` from `3f` toward `1.5f`–`2f`. Do not disable the whole feature — this is a
  tuning constant, same pattern as the tank plan's `TRIGGER-1`.
- **TRIGGER-2 (double-casting: predictive path and HP-reactive path both push to 3 casts, over-healing):** confirm
  `Math.Max` is used (never additive `+=`) — if it still over-triggers, cap `numberOfCasts` boost to raise by
  exactly one tier (`Math.Max(numberOfCasts, currentTierFloor + 1)`-style), not a flat `2`.
- **TRIGGER-3 (IPC consumer confusion — external plugin expects the upstream `"WrathCombo"` IPC method set):**
  confirm `docs/IPC.md`'s ParseLord5-extension callout (MOVE 4) is clear that these four getters exist only under
  the `"ParseLord5"` IPC prefix, not upstream WrathCombo.

## 5 · Deferred (future slices, not this plan)

- **Unify `RaidwideCasting`/`GroupDamageIncoming`'s Svc.Objects scan with `MechanicCastTracker.Update()`'s scan.**
  Real, confirmed duplication (near-identical cast-type/effect-range filtering, run independently every frame).
  Deferred because it touches the ~50 call sites of `GroupDamageIncoming` across nearly every job — needs its own
  plan and its own job-by-job verification pass, not a first-slice change.
- **DPS burst-window alignment.** Gate `/wrath burst`'s automatic behavior (not the manual command) on
  `MechanicCastTracker.HasImminentImpact(Raidwide, N)` so auto-burst biases toward landing as damage windows open.
  Deferred: `/wrath burst` is currently a pure manual toggle; making it context-aware changes command semantics and
  needs its own design pass and user sign-off before automating.
- **BossMod/Reborn timeline IPC consumption** (carried over from `pl5-mechanic-cast-tracker-battle-plan.md` §5,
  Slice 3) — multi-hit and instant-cast mechanics the cast-bar scanner can't see. Still blocked on verifying the
  timeline IPC provider shape.
- **Tank-side consumption of the new `HasImminentImpact(kind, seconds)` overload** — `TankSmartMitigationThreat`
  currently only reads `PredictedSpikeFraction()`; it could also gate `ShouldOfferHeavyMitigation`'s heavy-CD
  offer on `HasImminentImpact(Tankbuster, N)` for earlier pre-arming. Not in this slice — healer + IPC is the
  chosen vertical; re-open as its own small follow-on once this lands and is play-proven.

---

Write-back: after execution, flip this plan's status to `executed`, log outcome to `AgentBrain/state/log.md`, and
close the tracking issue with the audit summary (build/test/eval numbers + red-check results).

---

## 6 · Execution record (2026-08-15)

Implementation plan: `icm/stages/02-plan/output/encounter-awareness-02-plan.md`.
Implement note: `icm/stages/03-implement/output/encounter-awareness-03-implement.md`.

| Move | Result |
| --- | --- |
| 0 · Preflight | Baseline confirmed: HEAD `e367c8220`, build 0/0, 64 tests, evals 14/14 |
| 1 · Tracker overload | `HasImminentImpact(MechanicCastKind, float)` added |
| 2 · Healer feed | `PredictiveHealerLeadSeconds = 3f` + kind-scoped `Math.Max` boost in `HandleRaidwide` |
| 3 · IPC getters | 4 read-only `[EzIPC]` methods in new `#region Encounter Awareness (ParseLord5)` |
| 4 · Docs | `docs/IPC.md` capability bullet + `Encounter Awareness Methods (ParseLord5-only)` section |
| 5 · Tests | 3 structural tests added; 64 → 67 passed |

Verification:

| Command | Result |
| --- | --- |
| `dotnet build WrathCombo/WrathCombo.csproj -c Release` | 0 errors, 0 warnings |
| `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` | Passed 67, Failed 0 |
| `pwsh -NoProfile -File scripts/rotation-evals.ps1` | passed=14 failed=0 |
| quality gate `normal` | PASS_WITH_WARNINGS — run `20260815T165640Z-300be783`, warnings are the test-hack detector's expected test-critical-file-changed notices (matches the `AGENTS_FULL.md` documented baseline) |

Red checks (both confirmed, both restored):

| Mutation | Test that failed |
| --- | --- |
| `MechanicCastKind.Raidwide` → `.Tankbuster` | `PredictiveMechanics_HealerRaidwideCountFeedIsAdditiveAndKindScoped` |
| added `Guid lease` to `GetNextMechanicImminent` | `IPC_NextMechanicGettersAreReadOnly` |

Scope: 215 insertions, 0 deletions across 4 files owned by this plan (`AutoRotationController.cs` +17,
`Provider.cs` +71, `RotationStructureTests.cs` +83, `docs/IPC.md` +30) plus the new
`MechanicCastTracker.cs` overload. No hook, detour, `UseAction` call site, or existing IPC method modified.
`ShouldHandleHealerRaidwides` and the L526 gate untouched, as required.
