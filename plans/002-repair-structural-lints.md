# Plan 002: Repair the two structural lints HEAD already broke

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat e9b9d1789..HEAD -- WrathCombo.Tests/RotationStructureTests.cs WrathCombo/AutoRotation/AutoRotationController.cs`
> If either file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition. Plan 001's two-line blocklist
> change in the controller is expected and required.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: plans/001-keep-sge-blocklist-after-asex-peek.md
- **Category**: tests
- **Planned at**: commit `e9b9d1789`, 2026-08-25

## Why this matters

`WrathCombo.Tests/RotationStructureTests.cs` is the lock `CLAUDE.md` names for Auto-Rotation probe invariants. HEAD `e9b9d1789` inserted ActionStacksEX `TryPeekAction` into the healer `actCheck` lambda and both DPS execute paths. The two regexes still describe the pre-peek one-liners, so `dotnet test` is Failed 2 / Passed 54 at HEAD. A red suite is not a gate: the next change that actually drops `CanUseAutorotDpsAction` or reintroduces an unconditional `IsSelectingAutorotAction = true` will land unnoticed. Plan 001 also changes the DPS-lane `if` text; this plan rewrites the regexes against that post-fix shape so the suite encodes "blocklist always runs", not "blocklist skipped when ASEX matches".

## Current state

- `WrathCombo.Tests/RotationStructureTests.cs` — source-text lints. `RepoRoot()` walks one directory up from the test file. Tests read `.cs` files as strings. No Dalamud reference.
- `WrathCombo/AutoRotation/AutoRotationController.cs` — live shapes this plan must lock. Do not edit it here (plan 001 already did).

Failing fact `AutorotationDpsLane_BlocksSgeDefensiveActions` (`:111-129`) still asserts:

```
Assert.Matches(
    @"uint outAct = OriginalHook\(InvokeCombo\(preset, attributes, ref gameAct, OverrideTarget\)\);\s*if \(!CanUseAutorotDpsAction\(outAct\)\)",
    source);
Assert.Matches(
    @"var outAct = OriginalHook\(InvokeCombo\(preset, attributes, ref gameAct, target\)\);\s*if \(!attributes\.AutoAction!\.IsHeal && !CanUseAutorotDpsAction\(outAct\)\)",
    source);
```

Those fail because `TryPeekAction` now sits between `InvokeCombo` and the `if`. After plan 001 the `if` lines are:

- AoE: `if (!CanUseAutorotDpsAction(outAct))`
- ST: `if (!attributes.AutoAction!.IsHeal && !CanUseAutorotDpsAction(outAct))`

Failing fact `AutorotationProbeContext_IsOptIn` (`:192-203`) still asserts:

```
Assert.Matches(@"attr\.AutoAction\?\.IsHeal == true && ActionReady\(AutoRotationHelper\.InvokeCombo\(x.Key, attr, ref _, selectingAutorotAction: true\)\)", source);
```

Live `actCheck` (`:570-585`) is a multi-statement lambda:

```
bool actCheck = autoActions.Any(x =>
{
    var attr = x.Key.Attributes();
    uint gameAct = 0;
    var outAct = AutoRotationHelper.InvokeCombo(x.Key, attr, ref gameAct, selectingAutorotAction: true);
    uint actionToCheck = outAct;
    if (ActionStacksEXIPC.TryPeekAction(...))
        actionToCheck = asResolvedAction;
    return attr.AutoAction?.IsHeal == true && ActionReady(actionToCheck);
});
```

The rest of `AutorotationProbeContext_IsOptIn` still passes and must stay:

- `Assert.Contains("IGameObject? optionalTarget = null, bool selectingAutorotAction = false)", source);`
- `Assert.Contains("IsSelectingAutorotAction = selectingAutorotAction;", source);`
- `Assert.DoesNotContain("IsSelectingAutorotAction = true;", source);` — this is the real lock. `InvokeCombo` assigns from the parameter. Do not weaken it.
- `Assert.Single(Regex.Matches(source, @"selectingAutorotAction:\s*true"));` — exactly one probe-context opt-in, the healer `actCheck`.

Style: 4-space indent, xUnit `[Fact]`, verbatim string regexes with `@`. Match the file. These are text tripwires, not Roslyn. Keep them that way in this plan.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Drift check | `git diff --stat e9b9d1789..HEAD -- WrathCombo.Tests/RotationStructureTests.cs WrathCombo/AutoRotation/AutoRotationController.cs` | controller shows plan 001 only; tests unchanged until this plan |
| Confirm 001 landed | `rg -n "asRedirected && !CanUseAutorotDpsAction" WrathCombo/AutoRotation/AutoRotationController.cs` | no matches |
| Tests | `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --nologo --filter FullyQualifiedName~RotationStructureTests` | all facts in that class pass |
| Full suite | `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --nologo` | Failed 0, Passed 56 |

Working directory: `C:\Users\kruil\Documents\Projects\ffxiv-tools\ParseLord5`.

## Scope

**In scope**:
- `WrathCombo.Tests/RotationStructureTests.cs` — the two failing `Assert.Matches` calls, plus one new assertion that the DPS-lane blocklist is not short-circuited by `asRedirected`.

**Out of scope**:
- `AutoRotationController.cs` — plan 001.
- Rewriting lints as Roslyn syntax-tree queries.
- Adding tests for `OverrideTarget`, ASEX IPC, or job combos.
- `scripts/rotation-evals.ps1`.
- GitHub Actions.

## Git workflow

- Branch: `advisor/improve-f1-f5` (same as 001).
- Commit message: `test(autorot): lock SGE blocklist and probe opt-in after ASEX peek`
- Do NOT push or open a PR unless the operator instructed it.
- Do not add a co-author trailer.

## Steps

### Step 1: Confirm plan 001 is in the controller

```
rg -n "asRedirected && !CanUseAutorotDpsAction" WrathCombo/AutoRotation/AutoRotationController.cs
```

Expect no matches. If two matches remain, STOP and run plan 001 first. Do not update the tests to encode the bypass.

Confirm the live call sites:

```
rg -n "CanUseAutorotDpsAction\(outAct\)" WrathCombo/AutoRotation/AutoRotationController.cs
```

Expect the definition plus:

- `if (!CanUseAutorotDpsAction(outAct))`
- `if (!attributes.AutoAction!.IsHeal && !CanUseAutorotDpsAction(outAct))`

### Step 2: Replace the two adjacency regexes

In `AutorotationDpsLane_BlocksSgeDefensiveActions`, replace the two `Assert.Matches` at `:123-128` with assertions that lock the **post-001** conditions without requiring them to be the next token after `InvokeCombo` (the peek block is allowed in between):

```
Assert.Contains("if (!CanUseAutorotDpsAction(outAct))", source);
Assert.Contains("if (!attributes.AutoAction!.IsHeal && !CanUseAutorotDpsAction(outAct))", source);
Assert.DoesNotContain("!asRedirected && !CanUseAutorotDpsAction", source);
```

Keep the existing `Assert.Contains` / `Assert.DoesNotContain` lines for `CanUseAutorotDpsAction` itself, `SGE.Rhizomata or`, `SGE.Kerachole or`, `SGE.EukrasianDiagnosis or`, `SGE.EukrasianPrognosis2`, and `SGE.Eukrasia or`. Those still match.

Do **not** restore a regex that requires `InvokeCombo` immediately followed by the `if`. That is what went red.

### Step 3: Replace the actCheck one-liner regex

In `AutorotationProbeContext_IsOptIn`, delete the `Assert.Matches` at `:202` that looks for `ActionReady(AutoRotationHelper.InvokeCombo(...))`.

Replace it with assertions on the live lambda, still requiring opt-in probe context and readiness on the (possibly peeked) action:

```
Assert.Contains("selectingAutorotAction: true", source);
Assert.Contains("ActionStacksEXIPC.TryPeekAction(", source);
Assert.Contains("return attr.AutoAction?.IsHeal == true && ActionReady(actionToCheck);", source);
```

Leave these four lines unchanged:

- `Assert.Contains("IGameObject? optionalTarget = null, bool selectingAutorotAction = false)", source);`
- `Assert.Contains("IsSelectingAutorotAction = selectingAutorotAction;", source);`
- `Assert.DoesNotContain("IsSelectingAutorotAction = true;", source);`
- `Assert.Single(Regex.Matches(source, @"selectingAutorotAction:\s*true"));`

The `Assert.Single` still holds: the only `selectingAutorotAction: true` is the healer `actCheck`. Execute paths call `InvokeCombo` with the default `false`.

### Step 4: Run the tests

```
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --nologo --filter FullyQualifiedName~RotationStructureTests
```

Expect every fact in that class to pass, including the two that failed at HEAD.

Then:

```
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --nologo
```

Expect `Failed: 0, Passed: 56`.

If `AutorotationProbeContext_IsOptIn` still fails on `Assert.DoesNotContain("IsSelectingAutorotAction = true;")`, STOP — that means someone assigned the flag unconditionally. Do not delete that assertion.

If `Assert.Single` fails because a second `selectingAutorotAction: true` appeared, STOP and report. Do not raise the count.

## Test plan

This plan **is** the test change. No production tests to add.

Pattern: existing facts in the same file. Keep reading source as text via `File.ReadAllText`. Do not add a project reference on the plugin.

Cases locked after this plan:

- SGE DPS-lane blocklist method still exists and still lists Rhizomata/Kerachole/EukrasianDiagnosis/EukrasianPrognosis2.
- Eukrasia is still not on the blocklist.
- Both execute paths call `CanUseAutorotDpsAction(outAct)`.
- Neither path short-circuits that call with `!asRedirected &&`.
- Probe context is opt-in (parameter default false; assignment from parameter; never `= true`; exactly one named-argument `true`).
- Healer `actCheck` peeks then calls `ActionReady(actionToCheck)`.

## Done criteria

- [ ] `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --nologo` → Failed 0, Passed 56
- [ ] The two old adjacency / one-liner regexes are gone (`rg -n "ActionReady\\(AutoRotationHelper\\.InvokeCombo" WrathCombo.Tests/RotationStructureTests.cs` returns no matches)
- [ ] `Assert.DoesNotContain("!asRedirected && !CanUseAutorotDpsAction", source);` is present
- [ ] `Assert.DoesNotContain("IsSelectingAutorotAction = true;", source);` is still present
- [ ] `Assert.Single(Regex.Matches(source, @"selectingAutorotAction:\s*true"))` is still present
- [ ] No files outside the in-scope list are modified except `plans/README.md`
- [ ] `plans/README.md` status row for 002 is DONE

## STOP conditions

- Plan 001 has not landed (`!asRedirected && !CanUseAutorotDpsAction` still in the controller).
- `Assert.DoesNotContain("IsSelectingAutorotAction = true;")` fails against live source.
- `Assert.Single(...selectingAutorotAction:\s*true)` fails.
- The live `actCheck` no longer contains `ActionReady(actionToCheck)` or `selectingAutorotAction: true`.
- A test other than the two named facts fails and the failure is not caused by your regex edit.

## Maintenance notes

- These are still string tripwires. A whitespace-only reformat of the two `if` lines will fail them. That is acceptable; the comment in the test (if you add one) should say "update me when the execute-path shape changes, do not delete".
- Reviewer: reject any patch that removes `Assert.DoesNotContain("IsSelectingAutorotAction = true;")` or raises the `Assert.Single` count.
- Do not use this plan as an excuse to convert the file to Roslyn. That is a separate finding (TEST-04), not selected.
