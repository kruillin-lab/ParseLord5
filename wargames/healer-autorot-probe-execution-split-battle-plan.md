---
tags:
  - type/plan
  - project/parselord5
  - status/active
type: plan
project: parselord5
status: historical
aliases: []
---
# Battle Plan - Healer autorotation probe execution split

**Origin:** [healer-autorot-probe-execution-split-war-room.md](healer-autorot-probe-execution-split-war-room.md)
**Status:** EXECUTED — 2026-08-13 on `main` @ working tree; tracked as [#5](https://github.com/kruillin-lab/ParseLord5/issues/5)
**Plan authored:** 2026-08-11 against `main` @ `aafddadd5`
**Selected COA:** COA-2 - make `AutoRotationHelper.InvokeCombo` set `IsSelectingAutorotAction` only when the caller explicitly opts into a readiness probe.

## 0. Theatre map

- **Source root:** `C:\Users\kruil\orca\ParseLord5` (Windows). The Linux path `/home/kruillin/Projects/Projects/ParseLord5` used by the 2026-07-07 sibling plans is dead.
- **Branch:** `main` only. `parselord5-wc-base` does not exist; `AGENTS.md` forbids creating branches, so this plan executes in the working tree on `main`.
- **Primary file:** `WrathCombo/AutoRotation/AutoRotationController.cs`
- **Regression file:** `WrathCombo.Tests/RotationStructureTests.cs`
- **Blast-radius files (read-only in this plan):** `WrathCombo/Combos/PvE/SGE/SGE.cs`, `WrathCombo/Combos/PvE/WHM/WHM.cs`, `WrathCombo/Combos/PvE/WHM/WHM_Helper.cs`
- **Live deploy target:** `C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll` (the Release build writes here directly).
- **Baselines at `aafddadd5` + the landed teardown fix:** build 0 errors / 0 warnings; `dotnet test` 55 passed / 0 failed; `scripts/rotation-evals.ps1` passed=14 failed=0 (the teardown-event-subscription-symmetry fixture was added 2026-08-11, taking the suite from 13 to 14).
- **Line-number anchor:** every `path:line` in this plan was read from committed `main` @ `aafddadd5` and matches `git show aafddadd5:<path>` exactly. An unrelated teardown-leak fix was in flight in the working tree while this plan was written; it inserts 6 lines at `AutoRotationController.cs:186`, which shifts every controller citation below that point by +6. Every step below leads with a `grep` that re-derives its own location, so re-run the greps rather than trusting a raw line number if the tree has moved.

## 1. Proof before edit

Run these first. If any expected observation does not hold, stop and re-open the war room — the plan is written against this exact shape.

1. Confirm the split has not already landed under another name.
   - `git log --oneline -S"selectingAutorotAction"`
   - Expected: exactly one commit, `76912de5a`, which is the commit that *introduced* the flag and added the war room doc itself. No later split commit exists.

2. Confirm `InvokeCombo` is still context-blind.
   - Read `WrathCombo/AutoRotation/AutoRotationController.cs:1458-1491`.
   - Expected: signature is `InvokeCombo(Preset preset, PresetStorage.PresetData attributes, ref uint originalAct, IGameObject? optionalTarget = null)` at `:1458`; `IsSelectingAutorotAction = true;` at `:1466` is unconditional; `finally` resets it at `:1487`.

3. Enumerate every call site.
   - `grep -n "InvokeCombo(" WrathCombo/AutoRotation/AutoRotationController.cs`
   - Expected: one definition at `:1458` and exactly four call sites — `:549` (the `actCheck` readiness probe), `:1204` (`ExecuteAoE` heal lane), `:1254` (`ExecuteAoE` DPS lane), `:1333` (`ExecuteST`). No `InvokeCombo` call sites exist outside this file.

4. Confirm the DPS deny list survives independently of the flag.
   - Read `WrathCombo/AutoRotation/AutoRotationController.cs:1431-1456` and `:1255`, `:1334`.
   - Expected: `CanUseAutorotDpsAction` is declared at `:1431`, is called on the AoE DPS result at `:1255`, and on the ST result at `:1334` gated by `!attributes.AutoAction!.IsHeal`. It never reads `IsSelectingAutorotAction`, so Move 1 cannot weaken it.

5. Confirm which combo branches change meaning once execution stops setting the flag.
   - `grep -n "IsSelectingAutorotAction" WrathCombo/Combos/PvE/SGE/SGE.cs WrathCombo/Combos/PvE/WHM/WHM.cs WrathCombo/Combos/PvE/WHM/WHM_Helper.cs`
   - Expected, SGE: DPS-mode guards at `:50`, `:57`, `:129`, `:140`, `:223`, `:247`, `:254`, `:345`, `:369`, `:376`; **heal-mode** raidwide guards at `:585` (`SGE_ST_Heal_AdvancedMode`, class opens `:573`) and `:661` (`SGE_AoE_Heal_AdvancedMode`, class opens `:651`).
   - Expected, WHM: `:116` (`WHM_ST_Simple_DPS`), `:168` (`WHM_AoE_Simple_DPS`), `:230` and `:299` (`WHM_ST_MainCombo`), `:342`, `:358`, `:403` (`WHM_AoE_DPS`).
   - Expected, `WHM_Helper.cs`: `:67` and `:101` inside `TryDpsSingleTargetHealPriority` / `TryDpsAoEHealPriority`, which have **no callers** — `RotationStructureTests.cs:175-176` forbids the WHM/AST DPS combos from calling them. These two gates are inert either way.

6. Confirm the pinned test regexes will survive a trailing optional parameter.
   - Read `WrathCombo.Tests/RotationStructureTests.cs:123-128`.
   - Expected: the regexes match the *call sites* `InvokeCombo(preset, attributes, ref gameAct, OverrideTarget)` and `InvokeCombo(preset, attributes, ref gameAct, target)`. Appending a new optional parameter **after** `optionalTarget` leaves those three execution call sites byte-identical, so the regexes keep matching.

7. Confirm a clean starting tree.
   - `git status --porcelain`
   - Expected: empty. At the time this plan was written the tree carried unrelated concurrent teardown-leak edits to `WrathCombo/Data/ActionWatching.cs`, `WrathCombo/Services/IPC/Leasing.cs`, and `WrathCombo/WrathCombo.cs`. Do not start Move 1 while those are in flight — the build/test baseline is not attributable otherwise.

## 1a. Stale intel correction

Recon fact **F1** in the war room ("WHM DPS combo weaves, including Assize, Presence of Mind, and Lucid Dreaming, are guarded by `!AutoRotationController.IsSelectingAutorotAction`") is **no longer true** and must not be used as a premise.

- `WrathCombo/Combos/PvE/WHM/WHM.cs:76-92` (`WHM_ST_Simple_DPS`), `:141-156` (`WHM_AoE_Simple_DPS`), `:247-265` (`WHM_ST_MainCombo`), and `:375-389` (`WHM_AoE_DPS`) compute `canAssize` / `canPresenceOfMind` and return `Assize`, `PresenceOfMind`, and `Role.LucidDreaming` behind a bare `CanWeave()` — no flag guard.
- `WrathCombo.Tests/RotationStructureTests.cs:180-190` (`WhmOffensiveWeaves_AreNotSuppressedByAutorotationSelectionFlag`) actively pins that removal.

In other words the *symptom* the war room opened on was closed by the per-job route the war room screened out as **COA-1**, while the decided **COA-2** semantic fix was never built. This plan is therefore about the commander's intent — "only readiness probes should set `IsSelectingAutorotAction`" (war room §1) — and not about restoring WHM Assize, which already works. Success criteria in §4 are set accordingly.

## 2. Moves

### Move 1 - Make the probe context opt-in

- **Proof before edit:** `grep -n "IsSelectingAutorotAction" WrathCombo/AutoRotation/AutoRotationController.cs` → expect `:54` (declaration), `:1466` (set), `:1487` (reset), and nothing else in this file.
- **Exact edit:** in `WrathCombo/AutoRotation/AutoRotationController.cs`
  - `:1458` — append a trailing optional parameter, after `optionalTarget`, so the three execution call sites stay textually unchanged:
    `public static uint InvokeCombo(Preset preset, PresetStorage.PresetData attributes, ref uint originalAct, IGameObject? optionalTarget = null, bool selectingAutorotAction = false)`
  - `:1466` — replace `IsSelectingAutorotAction = true;` with `IsSelectingAutorotAction = selectingAutorotAction;`
  - `:1487` — leave `IsSelectingAutorotAction = false;` in the `finally` unchanged; it is already an unconditional clear and stays correct.
- **Expected observation:** build stays 0 errors / 0 warnings. No call site edits are required by this move alone. `AutorotationDpsLane_BlocksSgeDefensiveActions` (`RotationStructureTests.cs:111-129`) still passes because its regexes read the unchanged call sites.
- **Most-likely failure and cause:** a genuine dry-run caller is silently downgraded to execution semantics, because `= false` makes omission invisible to the compiler.
- **Counter:** §1 step 3 already proves the call-site set is closed at four, all inside this one file. Move 3 adds a structural test that fails if the number of opt-in probe call sites ever leaves exactly one.

### Move 2 - Tag the one real probe

- **Proof before edit:** `grep -n "actCheck" WrathCombo/AutoRotation/AutoRotationController.cs` → expect `:546` (declaration of the readiness scan) and `:576` (its only consumer, the `canHeal` conjunction at `:574-576`). This confirms `actCheck` exists purely to answer "is any heal ready?" and never issues an action.
- **Exact edit:** `WrathCombo/AutoRotation/AutoRotationController.cs:549` — change
  `return attr.AutoAction?.IsHeal == true && ActionReady(AutoRotationHelper.InvokeCombo(x.Key, attr, ref _));`
  to
  `return attr.AutoAction?.IsHeal == true && ActionReady(AutoRotationHelper.InvokeCombo(x.Key, attr, ref _, selectingAutorotAction: true));`
  (a named argument after positional arguments, skipping the optional `optionalTarget`, is legal C# and keeps the existing `null` target).
- **Expected observation:** probe safety from the three sibling EXECUTED war rooms is preserved bit-for-bit. During the readiness scan, `SGE.cs:585` and `:661` still short-circuit their raidwide branches, and `WHM.cs:230` / `:358` still skip Plenary Indulgence, exactly as before this plan.
- **Most-likely failure and cause:** a dry-run path exists that is not `actCheck` — for example a future readiness check added to `ProcessAutoActions` — so it loses suppression and starts leaking heal branches into a scan.
- **Counter:** the closed four-site enumeration in §1 step 3 is the audit. If a fifth call site ever appears, the reviewer decides probe-vs-execution at that site and tags it explicitly; the Move 3 test forces that decision instead of letting it default.

### Move 3 - Fence the new semantics with a structural test

- **Proof before edit:** read `WrathCombo.Tests/RotationStructureTests.cs:180-191` → expect the last `[Fact]` to end at `:190` and the class brace to close at `:191`, so a new fact inserts cleanly between them.
- **Exact edit:** in `WrathCombo.Tests/RotationStructureTests.cs`, insert a new `[Fact]` between `:190` and `:191`:
  - `AutorotationProbeContext_IsOptIn`
    - read `WrathCombo/AutoRotation/AutoRotationController.cs` via the existing `RepoRoot()` helper;
    - `Assert.Contains("IGameObject? optionalTarget = null, bool selectingAutorotAction = false)", source);`
    - `Assert.Contains("IsSelectingAutorotAction = selectingAutorotAction;", source);`
    - `Assert.DoesNotContain("IsSelectingAutorotAction = true;", source);`
    - `Assert.Equal(1, Regex.Matches(source, @"selectingAutorotAction:\s*true").Count);`
    - `Assert.Matches(@"attr\.AutoAction\?\.IsHeal == true && ActionReady\(AutoRotationHelper\.InvokeCombo\(x\.Key, attr, ref _, selectingAutorotAction: true\)\)", source);`
- **Expected observation:** test count goes 55 → 56, all passing. The new fact fails if anyone re-broadens the flag, and fails if a second call site opts in without review.
- **Most-likely failure and cause:** `Regex` is not imported in the test file, or `Assert.Equal(1, ...)` trips because the string `selectingAutorotAction: true` also appears in a comment.
- **Counter:** add `using System.Text.RegularExpressions;` to the test file's usings; keep the phrase out of comments — express intent in the assertion message, not in a comment containing the literal.

### Move 4 - Build, deploy, live retest

- **Proof before edit:** `git status --porcelain` → expect only the four files this plan touches (`AutoRotationController.cs`, `RotationStructureTests.cs`, and the two wargame docs). Anything else means an unrelated change is riding along and the deploy is not attributable.
- **Exact edit:** none. Run §4 verification in order, then deploy and hand the user the manual retest below.
- **Expected observation:** on a solo target dummy, WHM and SGE keep their offensive oGCDs (already true before this plan, per §1a), **and** heal-mode raidwide branches now become reachable during genuine heal execution rather than being suppressed as if every execution were a probe.
- **Most-likely failure and cause:** SGE resumes spending weave slots on Kerachole / Holos / Panhaima on a dummy. Cause: `SGE.cs:585` and `:661` sit in the heal lane, and the heal-lane execution call at `:1204` is **not** filtered by `CanUseAutorotDpsAction` (only `:1255` and `:1334` are, and `:1334` is gated on `!IsHeal`). Removing the flag from execution is precisely what un-suppresses them.
- **Counter:** this is the accepted risk in war room assumption A2 and is the designed intent ("keep actual healing when `canHeal` is true"). If the user reports regression, do **not** re-broaden the flag — that reverts the whole plan. Instead fork to TRIGGER-1 and gate the heal lane on real heal need at the `:1204` call site, or fall back to COA-3 instrumentation.

## 3. Tests

- New: `AutorotationProbeContext_IsOptIn` (Move 3).
- Must keep passing unchanged:
  - `AutorotationDpsLane_BlocksSgeDefensiveActions` — `RotationStructureTests.cs:111-129`; proves the SGE DPS deny list is still wired into both `ExecuteST` and `ExecuteAoE`.
  - `SgeHealRaidwideFeatures_AreSuppressedDuringAutorotationSelection` — `:91-109`; proves the SGE heal-mode raidwide guards still exist and still precede `RaidwideKerachole()`.
  - `WhmOffensiveWeaves_AreNotSuppressedByAutorotationSelectionFlag` — `:180-190`; proves this plan did not re-add a flag guard to WHM's offensive weaves.
  - `AstAndWhmDpsCombos_DoNotUseForkOnlyDpsHealPriority` — `:155-178`.

## 4. Verification

All commands run from `C:\Users\kruil\orca\ParseLord5`.

1. Build:

```powershell
dotnet build WrathCombo/WrathCombo.csproj -c Release
```

PASS: 0 errors, 0 warnings. Any new warning is a regression against the `aafddadd5` baseline.

2. Tests:

```powershell
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release
```

PASS: 56 passed, 0 failed (55 baseline + `AutorotationProbeContext_IsOptIn`).

3. Rotation evals:

```powershell
pwsh -NoProfile -File scripts/rotation-evals.ps1
```

PASS: `passed=14 failed=0`.

4. Deploy proof:

The Release build writes to `C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll`.

```powershell
Get-FileHash C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll -Algorithm SHA256
```

PASS: timestamp and SHA256 both change relative to the pre-build values. Record both in §6.

5. Red check (do this before trusting the new test):

Temporarily revert Move 1's `:1466` line to `IsSelectingAutorotAction = true;` and re-run only the new fact. PASS means it **fails** red. Restore Move 1 immediately.

## 5. Trigger forks

- **TRIGGER-1:** SGE spends weave slots on Kerachole / Ixochole / Holos / Panhaima on a solo dummy after deploy → gate the heal-lane call at `AutoRotationController.cs:1204` on real heal need instead of on the probe flag. Do not re-broaden the flag.
- **TRIGGER-2:** WHM emits a lone Medica III again → this is the residual risk recorded in war room §6 and is a heal-lane false positive, not a probe/execution problem. Trace `canHeal` (`:574-576`), `aoeheal`, and the WHM raidwide block at `WHM.cs:230` / `:358` separately; the `TraceWhmHeal` calls at `:1207` and `:1339` already emit the needed phase data.
- **TRIGGER-3:** a fifth `InvokeCombo` call site appears → classify it probe-vs-execution before merging; the Move 3 count assertion will fail until someone does.
- **ABORT-1:** build fails for code reasons rather than environment reasons.
- **ABORT-2:** `AutorotationDpsLane_BlocksSgeDefensiveActions` fails — that means the trailing-parameter approach shifted the execution call sites and the deny-list fence is no longer provably on both lanes. Re-do Move 1 with the parameter strictly last.
- **ABORT-3:** `git status --porcelain` shows files outside this plan's four. Stop and reconcile before deploying; the DLL hash would not be attributable.

## 6. Report

- Files changed:
  - `WrathCombo/AutoRotation/AutoRotationController.cs`
  - `WrathCombo.Tests/RotationStructureTests.cs`
  - `wargames/healer-autorot-probe-execution-split-war-room.md`
  - `wargames/healer-autorot-probe-execution-split-battle-plan.md`
- Build: `dotnet build WrathCombo/WrathCombo.csproj -c Release` → 0 errors / 0 warnings.
- Tests: `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` → 56 passed / 0 failed.
- Evals: `pwsh -NoProfile -File scripts/rotation-evals.ps1` → passed=14 failed=0.
- Red check: `AutorotationProbeContext_IsOptIn` failed after temporarily restoring `IsSelectingAutorotAction = true;`, passed after restore → yes.
- Deployed DLL: `C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll`, timestamp 2026-08-13T09:06:11-04:00, SHA256 `e954c0096d3338f938da1cd5678086d4462b4edae40bfdf09efb6edc97662d85`.
- Manual test ask: disable and re-enable ParseLord5, then fight a solo target dummy as WHM and as SGE. Report (a) whether WHM still weaves Assize / Presence of Mind / Lucid Dreaming, (b) whether SGE spends weave slots on Kerachole / Holos / Panhaima, (c) whether the lone Medica III recurs.
- Write-back: war room §7 updated; both docs flipped to `EXECUTED`.
