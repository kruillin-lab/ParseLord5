---
tags:
  - type/note
  - project/parselord5
  - status/active
type: note
project: parselord5
status: active
aliases: []
---
# Plan — Encounter Awareness (predictive healer raidwide count + read-only IPC)

Execution-ready companion to `wargames/pl5-encounter-awareness-battle-plan.md` (approved 2026-08-15,
tracking [#8](https://github.com/kruillin-lab/ParseLord5/issues/8)). The battle plan holds the *why*, the design
contract, and the trigger forks. This file holds the exact per-file edits, in order, with line anchors verified
against the working tree at HEAD `e367c8220`.

## Preconditions (MOVE 0, verified 2026-08-15)

| Check | Expected | Verified |
| --- | --- | --- |
| `git branch --show-current` | `main` | yes |
| `git status --porcelain` | pre-existing mechanic-cast-tracker dirty set only | yes |
| `git log --oneline -1` | `e367c8220` | yes |
| `dotnet build WrathCombo/WrathCombo.csproj -c Release` | 0 errors, 0 warnings | yes |
| `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` | Passed 64, Failed 0 | yes |
| `pwsh -NoProfile -File scripts/rotation-evals.ps1` | passed=14 failed=0 | yes |

Do **not** revert the pre-existing dirty set — it is the mechanic-cast-tracker feature this work builds on.

## Edit map (6 files, in execution order)

| # | File | Anchor | Change |
| --- | --- | --- | --- |
| 1 | `WrathCombo/Services/MechanicPrediction/MechanicCastTracker.cs` | after `HasImminentImpact(float)` (L68-69) | add kind-scoped overload |
| 2 | `WrathCombo/AutoRotation/AutoRotationController.cs` | using block (L25-28); `HandleRaidwide` (L751-782) | add using; add `PredictiveHealerLeadSeconds` const; additive `Math.Max` on `numberOfCasts` |
| 3 | `WrathCombo/Services/IPC/Provider.cs` | using block (L17-21); between `#endregion` (L571) and `#region Fine-Grained Combo Methods` (L573) | add using; new `#region Encounter Awareness (ParseLord5)` with 4 read-only getters |
| 4 | `docs/IPC.md` | capability list (L50-56); method list tail (L272) | document the 4 getters as a ParseLord5-only extension |
| 5 | `WrathCombo.Tests/RotationStructureTests.cs` | before closing brace (L229) | 3 structural tests |
| 6 | — | — | verification + red checks |

---

## 1 · `MechanicCastTracker.cs` — kind-scoped overload

Insert immediately after the existing `HasImminentImpact(float withinSeconds)` (currently L68-69), before
`private static int Priority(...)`:

```csharp
    internal static bool HasImminentImpact(MechanicCastKind kind, float withinSeconds) =>
        _last.Kind == kind && _last.TimeToImpactSeconds <= withinSeconds;
```

Nothing else in this file changes. `Update()`, `Last`, `PredictedSpikeFraction()`, and the existing
`HasImminentImpact(float)` are untouched.

**PASS:** build green; pure addition, unused until edit 2.

## 2 · `AutoRotationController.cs` — healer raidwide predictive feed

**2a.** Add to the using block (after `using WrathCombo.Services;`, L25):

```csharp
using WrathCombo.Services.MechanicPrediction;
```

**2b.** Add the tuning constant directly above `private static void HandleRaidwide(bool multihit)` (L751):

```csharp
    /// <summary>
    ///     Lead time for the predictive raidwide-heal count boost. Inside
    ///     <see cref="MechanicCastClassifier.MaxLeadSeconds" /> (6f) so shields
    ///     start landing shortly before impact rather than early enough to be
    ///     wasted on a mechanic that gets reset or moved.
    /// </summary>
    private const float PredictiveHealerLeadSeconds = 3f;
```

**2c.** In `HandleRaidwide`, after the existing `numberOfCasts` switch (ends L763) and **before**
`if (AutorotRaidwides >= numberOfCasts) return;` (L765):

```csharp
            // Additive only: a predicted big raidwide can raise the count, never
            // lower what current party HP already demands. Kind-scoped to
            // Raidwide -- Tankbuster/Cleave are single-target and must not
            // inflate a party-wide heal count.
            if (ParseLord5Experiments.PredictiveMechanics &&
                MechanicCastTracker.HasImminentImpact(MechanicCastKind.Raidwide, PredictiveHealerLeadSeconds))
                numberOfCasts = Math.Max(numberOfCasts, 2);
```

**Do not touch:** L526 (`ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming`) or
`ShouldHandleHealerRaidwides` (L646-655). `RotationStructureTests.cs:76` pins that exact substring.

**PASS:** build green; with `PredictiveMechanics = false` the computed `numberOfCasts` is identical to today.

## 3 · `Provider.cs` — four read-only IPC getters

**3a.** Add to the using block (after `using WrathCombo.Combos;`, L19):

```csharp
using WrathCombo.Services.MechanicPrediction;
```

**3b.** Insert a new region between the `#endregion` closing *Extra Job State Checks* (L571) and
`#region Fine-Grained Combo Methods` (L573). Four getters, `GetAutoRotationState()`'s attribute pattern
(`[EzIPC]` + `[SuppressMessage("Performance", "CA1822:Mark members as static")]`), no `Guid` lease parameter —
these are pure reads and cannot change plugin behavior.

Return types are `bool`/`string`/`float` only; `MechanicCastKind` stays internal and is never exposed as a type.
No flag re-check inside: `MechanicCastTracker.Update()` already resets `_last` to `default` when
`PredictiveMechanics` is off, so these return `false`/`"None"`/`0f` while the feature is disabled.

Methods: `GetNextMechanicImminent(float withinSeconds)` → `bool`; `GetNextMechanicKind()` → `string`;
`GetNextMechanicTimeToImpact()` → `float`; `GetPredictedMechanicSpikeFraction()` → `float`.

**PASS:** build green; grep shows exactly 4 new signatures, none containing `Guid`.

## 4 · `docs/IPC.md` — document the extension

**4a.** Append to the accessible-capability list (after L56 `- Variant Dungeon skills`):

```markdown
- Predictive mechanic timing (read-only; **ParseLord5-only extension**, not in upstream Wrath Combo)
```

**4b.** Append a documented method block after the `SetAutoRotationConfigState` entry (L272), matching the
existing `- \`signature\`` + indented-bullet style, and stating explicitly: available only under the
`"ParseLord5"` IPC prefix; no lease required; returns inert values when the feature flag is off.

**PASS:** documented signatures match edit 3 verbatim.

## 5 · `RotationStructureTests.cs` — 3 structural tests

Append before the closing brace (L229), matching the existing `PredictiveMechanics_*` style (L205-227):

1. `PredictiveMechanics_HealerRaidwideCountFeedIsAdditiveAndKindScoped` — asserts the HP-threshold base switch
   still exists (`<= 30 => 3,` / `<= 60 => 2,`), and that the boost line is flag-guarded, `Math.Max`-based, and
   `MechanicCastKind.Raidwide`-scoped.
2. `PredictiveMechanics_HealerRaidwideGateUnchanged` — re-pins
   `ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming`.
3. `IPC_NextMechanicGettersAreReadOnly` — asserts all 4 signatures are present and that none contains `Guid`.

**PASS:** 64 baseline + 3 new = 67 passed, 0 failed.

## 6 · Verification

```powershell
dotnet build WrathCombo/WrathCombo.csproj -c Release          # 0 errors, 0 warnings
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release  # 67 passed, 0 failed
pwsh -NoProfile -File scripts/rotation-evals.ps1              # passed=14 failed=0
```

Red checks (must fail, then restore):

| Check | Mutation | Expected failure |
| --- | --- | --- |
| Healer feed kind-scoping | `MechanicCastKind.Raidwide` → `.Tankbuster` in edit 2c | `PredictiveMechanics_HealerRaidwideCountFeedIsAdditiveAndKindScoped` |
| IPC read-only contract | add `Guid lease` to one getter | `IPC_NextMechanicGettersAreReadOnly` |

Operator-only in-game checks (M1 flag-on pre-shield, M2 flag-off rollback, M3 IPC readout) stay as specified in
the battle plan §3 — they need FFXIV + Dalamud and cannot be run headless.

## Rollback

Every edit is additive and flag-gated behind the existing `ParseLord5Experiments.PredictiveMechanics`
(default `false`). Disabling that flag — or `ParseLord5ExperimentalMode` — restores exact pre-change behavior
with no code revert. No hook, detour, `UseAction` call site, or existing IPC method is modified.
