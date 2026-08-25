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
# Battle Plan - Predictive mechanic cast tracker

**Origin:** direct suggestion (2026-08-13) — see *Recon* below. No war room convened; this plan
is the first artifact for the feature.
**Status:** EXECUTED 2026-08-13 on `main`. All six moves applied; build 0/0, 64 tests pass, evals 14/14, both red checks pass. One human step outstanding: the §3 manual in-game test (M1 flag-on / M2 flag-off).
**Tracking:** none yet (open an issue before Move 1 if the repo's issue-tracker workflow is required).
**Never clobber:** sibling to `parselord5-reload-crash-battle-plan.md`, `parselord5-stability-battle-plan.md`,
`pl5-native-action-stacks-battle-plan.md`, and the healer war rooms. Different mission; do not merge.

---

## 0 · Theatre map

- **Repo:** `C:\Users\kruil\orca\ParseLord5` (Windows). Exactly one branch, `main` (`AGENTS.md` forbids more).
- **Reference behavior already in tree (reuse, do not reinvent):**
  - `WrathCombo/CustomCombo/Functions/Action.cs:380` `RaidwideCasting(float?)` — scans `Svc.Objects` for
    hostile `IBattleChara` mid-cast, filters `spellSheet.CastType is 2 or 5 && spellSheet.EffectRange >= 30`,
    and already computes `caster.TotalCastTime - caster.CurrentCastTime` as remaining time.
  - `WrathCombo/CustomCombo/Functions/Action.cs:414` `BeingTargetedHostile` — hostile caster with
    `CastTargetObjectId == LocalPlayer.GameObjectId`.
  - `WrathCombo/CustomCombo/Functions/VFX.cs:365` `HasIncomingTankBusterEffect(out ageSeconds)` — VFX-marker tankbuster
    telegraph, binary + age.
  - `WrathCombo/Services/SmartMitigation/CombatTelemetryService.cs` — rolling 5s HP-delta buffer → `PlayerPressureState`.
  - `WrathCombo/Services/SmartMitigation/TankSmartMitigationThreat.cs:19` `Detect` — derives `MechanicSpikeFraction`
    from fixed constants (`MitigationCoverageCalculator.TankbusterSpikeFraction = 0.45f`) or landed
    `pressure.MaxSingleHit / MaxHp`. **This is the reactive seam the feature replaces.**
  - `WrathCombo/Services/SmartMitigation/MitigationCoverageCalculator.cs` — consumes `MitigationCoverageRequest`
    (`MechanicSpikeFraction`, `HorizonSeconds` hardcoded `4f`/`6f`).
  - `WrathCombo/WrathCombo.cs:378` — `CombatTelemetryService.Update()` runs every frame; the new tracker hooks in
    alongside it.
  - `WrathCombo/Core/Configuration.ExperimentalFlags.cs` — per-feature flags AND-ed with `ParseLord5ExperimentalMode`
    via `ParseLord5Experiments` (`WrathCombo/Combos/PvE/ALL/ParseLord5Experiments.cs`). `CombatTelemetry = false`
    is the pattern to copy.
- **Test linking (load-bearing):** `WrathCombo.Tests/WrathCombo.Tests.csproj` compiles pure-logic source files by
  `<Compile Include="..\WrathCombo\...\Foo.cs" Link="Sources\Foo.cs" />` — **no** Dalamud/FFXIVClientStructs/ECommons
  reference. Any type the tests touch must live in a file with zero plugin-framework imports. `HealDelayCurve.cs`,
  `MitigationCoverageCalculator.cs`, `CombatEventBuffer.cs` are the working examples.
- **Baselines (2026-08-11, `aafddadd5` + teardown fix):** build 0 errors / 0 warnings; `dotnet test` 55 passed / 0 failed;
  `scripts/rotation-evals.ps1` passed=14 failed=0. Re-verify at HEAD before Move 1 (`git log --oneline -1`).

### Mission (one sentence)

Turn the hostile-caster cast bar the game already exposes into a **forward-looking spike prediction** so smart
mitigation pre-arms cooldowns *before* the tankbuster/raidwide lands, instead of reacting to the HP dip after it.

### Explicit non-goals (first slice)

- No per-job edits (no `WAR/PLD/GNB/DRK/SGE/WHM` changes).
- No new external dependency (BossMod timeline IPC is a *future* slice; out of scope).
- No healer pre-shield timing changes (feeding the healer lane is Slice 2, deferred).
- No deletion or change of `HasIncomingTankBusterEffect` / `RaidwideCasting` — the tracker *augments* them.

---

## 1 · Design contract

Split into a pure, testable core and a thin Dalamud-facing scanner, so the core compiles into `WrathCombo.Tests`
via the existing `<Compile Include>` mechanism.

### Core (pure — new folder `WrathCombo/Services/MechanicPrediction/`)

```csharp
internal enum MechanicCastKind
{
    None,          // not a relevant mechanic cast
    Raidwide,      // cast type 2/5, wide effect range (mirrors RaidwideCasting)
    Tankbuster,    // hostile cast targeting the local player AND a tankbuster VFX marker is present
    Cleave,        // hostile cast targeting the local player, no VFX marker (single-target risk)
}

internal readonly record struct MechanicCastPrediction(
    MechanicCastKind Kind,
    float TimeToImpactSeconds,   // TotalCastTime - CurrentCastTime, clamped >= 0
    float PredictedSpikeFraction,// fraction of MaxHp the hit is expected to deal
    uint CastActionId);

internal static class MechanicCastClassifier
{
    internal const float RaidwideSpikeFraction = 0.20f;   // mirrors MitigationCoverageCalculator.RaidwideSpikeFraction
    internal const float TankbusterSpikeFraction = 0.45f; // mirrors MitigationCoverageCalculator.TankbusterSpikeFraction
    internal const float CleaveSpikeFraction = 0.20f;
    internal const float MaxLeadSeconds = 6f;             // ignore casts further out than this

    internal static MechanicCastPrediction Classify(
        bool isHostileCasting,
        byte castType,
        float effectRange,
        ulong castTargetObjectId,
        ulong localPlayerObjectId,
        bool hasTankbusterVfx,
        float totalCastTime,
        float currentCastTime,
        uint castActionId);
}
```

Rules (pure, deterministic — these are the unit-test contracts):

1. Not casting, or `TimeToImpactSeconds > MaxLeadSeconds`, or `castType` not `2 or 5` and not targeting the player
   → `Kind.None`.
2. `castType is 2 or 5` and `effectRange >= 30` → `Kind.Raidwide`, spike `RaidwideSpikeFraction`. *(mirrors
   `RaidwideCasting`'s filter exactly — no behavior invented here.)*
3. Else if `castTargetObjectId == localPlayerObjectId`:
   - `hasTankbusterVfx` → `Kind.Tankbuster`, spike `TankbusterSpikeFraction`.
   - else → `Kind.Cleave`, spike `CleaveSpikeFraction`.
4. Else `Kind.None`.
5. `TimeToImpactSeconds = Max(0, totalCastTime - currentCastTime)`. A zero/negative remaining time (cast about to
   resolve this frame) still classifies — the caller decides whether that's useful.

### Scanner (Dalamud-facing — `WrathCombo/Services/MechanicPrediction/MechanicCastTracker.cs`)

```csharp
internal static class MechanicCastTracker
{
    private static MechanicCastPrediction _last = default;
    internal static MechanicCastPrediction Last => _last;

    internal static void Update()
    {
        _last = default;
        if (!ParseLord5Experiments.PredictiveMechanics) return;
        // foreach hostile IBattleChara in Svc.Objects that IsCasting:
        //   classify via MechanicCastClassifier, keep the prediction with the
        //   smallest positive TimeToImpactSeconds (the soonest hit) per frame;
        //   on tie, prefer Tankbuster > Raidwide > Cleave.
    }

    internal static float PredictedSpikeFraction() => _last.PredictedSpikeFraction;
    internal static bool HasImminentImpact(float withinSeconds) =>
        _last.Kind != MechanicCastKind.None && _last.TimeToImpactSeconds <= withinSeconds;
}
```

- `Update()` runs every frame from `WrathCombo.cs:378`, immediately after `CombatTelemetryService.Update()`.
- The scanner mirrors `RaidwideCasting`'s hostile-caster iteration and reuses `HasIncomingTankBusterEffect` for the
  VFX flag — it does **not** reimplement VFX tracking.

### Feed into smart mitigation (the behavior change)

In `TankSmartMitigationThreat.Detect` (`WrathCombo/Services/SmartMitigation/TankSmartMitigationThreat.cs:39-49`),
add the predicted spike as an *additional* `Math.Max` source — **after** the existing fixed-constant and landed-hit
branches, so a landed hit (`MaxSingleHit / MaxHp`) can only *raise* the fraction, never be downgraded:

```csharp
if (ParseLord5Experiments.PredictiveMechanics)
    mechanicSpikeFraction = Math.Max(mechanicSpikeFraction, MechanicCastTracker.PredictedSpikeFraction());
```

Do **not** touch `ConfirmedTankbuster`, `SoftTankbuster`, or `SustainedPressure` — those still gate *selection*, the
spike fraction only scales the *coverage* math in `MitigationCoverageCalculator`. The landed-hit path stays as
fallback, so instant-cast tankbusters (no cast bar) still work exactly as today.

### Config surface

Add to `WrathCombo/Core/Configuration.ExperimentalFlags.cs` (mirroring `CombatTelemetry`):

```csharp
/// Predictive cast-bar mechanic detection feeding smart mitigation spike fraction.
public bool PredictiveMechanics = false;
```

Add to `WrathCombo/Combos/PvE/ALL/ParseLord5Experiments.cs`:

```csharp
internal static bool PredictiveMechanics =>
    Master && Service.Configuration.Experimental.PredictiveMechanics;
```

No Settings UI toggle in this slice — flag is default-off and master-gated, matching the other experiments that
were promoted after play-proving.

---

## 2 · Move sequence

### MOVE 0 — Preflight baseline (read-only)

```powershell
git branch --show-current                     # expect main
git status --porcelain                        # expect empty (clean tree)
git log --oneline -1
dotnet build WrathCombo/WrathCombo.csproj -c Release 2>&1 | tail -4   # expect 0 err / 0 warn
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release 2>&1 | tail -3  # expect Passed 55 (or current)
```

- **TRIGGER — tree dirty or build/test off baseline:** stop, reconcile, do not edit (ABORT-ENV).

### MOVE 1 — Core classifier (pure, test-linked)

- Create `WrathCombo/Services/MechanicPrediction/MechanicCastClassifier.cs` exactly as the contract above.
- Add `<Compile Include="..\WrathCombo\Services\MechanicPrediction\MechanicCastClassifier.cs" Link="Sources\MechanicCastClassifier.cs" />`
  to `WrathCombo.Tests/WrathCombo.Tests.csproj`.
- **PASS:** build green; classifier file imports only `System` (no Dalamud/ECommons/FFXIVClientStructs).

### MOVE 2 — Classifier tests (red-first)

Add `WrathCombo.Tests/MechanicCastClassifierTests.cs`. Minimum coverage, each asserting the pure contract:

- not casting → `None`
- `castType` not `2/5` and not targeting player → `None`
- raidwide cast (`castType 2`, `effectRange 30+`) → `Raidwide` + spike `0.20f`
- raidwide cast beyond `MaxLeadSeconds` → `None`
- player-targeted cast with VFX → `Tankbuster` + spike `0.45f`
- player-targeted cast without VFX → `Cleave` + spike `0.20f`
- `TimeToImpactSeconds == Max(0, total - current)` (include a negative-remaining case → `0`)

- **PASS:** these fail before MOVE 1's type exists (compilation), then pass after. Run
  `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --filter MechanicCastClassifier`.

### MOVE 3 — Scanner + tick hookup

- Create `WrathCombo/Services/MechanicPrediction/MechanicCastTracker.cs` (contract above; may import
  Dalamud/ECommons — it is **not** test-linked).
- Call `MechanicCastTracker.Update();` in `WrathCombo/WrathCombo.cs` immediately after
  `CombatTelemetryService.Update();` (line 378).
- **PASS:** build green. The scanner is inert (`PredictiveMechanics` default-off) so no runtime behavior changes.

### MOVE 4 — Config flags

- Add `PredictiveMechanics = false` to `Configuration.ExperimentalFlags.cs` (after `CombatTelemetry`).
- Add the `ParseLord5Experiments.PredictiveMechanics` property.
- **PASS:** build green; grep `PredictiveMechanics` over `WrathCombo/` shows exactly 4 sites (config field, experiments
  property, scanner gate, threat-model gate).

### MOVE 5 — Threat-model feed (the actual behavior change)

- Edit `TankSmartMitigationThreat.Detect` per the contract above — one `Math.Max` line inside an
  `if (ParseLord5Experiments.PredictiveMechanics)` guard, placed after the landed-hit branch at `:48-49`.
- **PASS:** build green. Default-off → all existing behavior byte-identical.

### MOVE 6 — Structural regression tests

Add to `WrathCombo.Tests/RotationStructureTests.cs` (the source-text structural suite):

- `PredictiveMechanics_SpikeFeedIsDefaultOffAndMasterGated` — asserts `MechanicCastTracker.PredictedSpikeFraction()`
  is only read inside an `if (ParseLord5Experiments.PredictiveMechanics)` guard in
  `TankSmartMitigationThreat.cs`, and the flag defaults `false` in `Configuration.ExperimentalFlags.cs`.
- `PredictiveMechanics_LandedHitPathRemainsAsFallback` — asserts `TankSmartMitigationThreat.cs` still contains the
  `pressure.MaxSingleHit / hpPlayer.MaxHp` line, so the reactive path is not removed.

- **PASS:** build + full test suite green; new structural facts fail if a future edit drops the guard or the fallback.

---

## 3 · Verification

All from `C:\Users\kruil\orca\ParseLord5`.

1. **Build:** `dotnet build WrathCombo/WrathCombo.csproj -c Release` → 0 errors, 0 warnings.
2. **Tests:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` → 55 + new classifier + structural tests, 0 failed.
3. **Evals:** `pwsh -NoProfile -File scripts/rotation-evals.ps1` → `passed=14 failed=0` (no preset touched; must stay green).
4. **Red check (classifier):** temporarily flip one classifier branch (e.g. `TankbusterSpikeFraction` → `0.1f`) and
   confirm a classifier test fails; restore immediately.
5. **Red check (structural):** temporarily remove the `ParseLord5Experiments.PredictiveMechanics` guard in the
   threat model and confirm `PredictiveMechanics_SpikeFeedIsDefaultOffAndMasterGated` fails; restore immediately.

### Deploy + manual test (operator only)

Release build writes straight to `C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll`.

- **M1 (flag on):** enable `ParseLord5ExperimentalMode` + `PredictiveMechanics`, fight a boss with a telegraphed
  tankbuster. Watch `[ParseLord5][*_SmartMit]` log lines: mitigation should pre-arm *before* the hit lands (the
  `spike=` value rises before `hp=` drops).
- **M2 (flag off):** disable `PredictiveMechanics`; confirm smart mitigation behaves exactly as the pre-change build
  (reactive, no pre-arm). This is the rollback check.

---

## 4 · Trigger forks

- **ABORT-ENV:** build not at baseline before edits, or test-linking of the classifier fails because it pulls a
  plugin-framework import. Fix: move the import out of the classifier into the scanner.
- **ABORT-SCOPE:** the scanner's hostile-caster iteration can't be written without duplicating `RaidwideCasting`'s
  throttle state in a way that changes its behavior — do not touch `Action.cs`; instead subscribe to the same
  `Svc.Objects` source read-only.
- **TRIGGER-1 (spike over-predicts):** mitigation fires too eagerly on a dummy/boss. Counter: lower `MaxLeadSeconds`
  to `4f` or require `hasTankbusterVfx` for `Tankbuster` before raising the spike — do **not** disable the whole feature.
- **TRIGGER-2 (instant-cast TB regression):** an instant-cast tankbuster that previously triggered via
  `MaxSingleHit` no longer mitigates. Counter: confirm MOVE 5 only *adds* a `Math.Max` source and never gates the
  landed-hit branch — if it still regresses, the guard placement is wrong (it must wrap only the new line).

## 5 · Deferred (future slices, not this plan)

- **Slice 2 — healer pre-shield:** feed `MechanicCastTracker.HasImminentImpact(shieldCastTime)` into the
  `HandleRaidwide` lane so SGE/WHM/AST shields land *before* impact. Deferred: it changes `HandleRaidwide` timing and
  needs the healer war rooms re-verified (issues #2/#4 outstanding).
- **Slice 3 — BossMod timeline:** consume BossMod/Reborn timeline IPC for multi-hit + instant-cast mechanics. Deferred:
  requires verifying the timeline IPC provider shape; `BossModIPC` today is conflict-detection only.
- **Settings toggle:** expose `PredictiveMechanics` in the Settings tab once play-proven, matching how `SmartMitigation`
  / `DynamicHealCurve` were promoted.

## 6 · Report skeleton (executor fills on completion)

- Files changed: `Services/MechanicPrediction/MechanicCastClassifier.cs`,
  `Services/MechanicPrediction/MechanicCastTracker.cs`, `WrathCombo.cs`,
  `Services/SmartMitigation/TankSmartMitigationThreat.cs`, `Core/Configuration.ExperimentalFlags.cs`,
  `Combos/PvE/ALL/ParseLord5Experiments.cs`, `WrathCombo.Tests/WrathCombo.Tests.csproj`,
  `WrathCombo.Tests/MechanicCastClassifierTests.cs`, `WrathCombo.Tests/RotationStructureTests.cs`.
- Build / tests / evals results, red checks, deploy SHA256, and M1/M2 outcomes.
- Write-back: flip this plan to `EXECUTED`, log to `AgentBrain/state/log.md`, and open/close the tracking issue.
