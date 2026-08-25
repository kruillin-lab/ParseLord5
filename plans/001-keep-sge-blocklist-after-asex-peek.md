# Plan 001: Stop ASEX peek from bypassing the SGE DPS-lane blocklist

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat e9b9d1789..HEAD -- WrathCombo/AutoRotation/AutoRotationController.cs`
> If that file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: MED
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `e9b9d1789`, 2026-08-25

## Why this matters

Auto-Rotation probes the DPS preset and the Heal preset separately. The DPS probe must not return a heal or shield, or Sage dumps Kerachole/Druochole/etc. onto a full-HP party. `CanUseAutorotDpsAction` is the last-line blocklist for that (SGE-only today). HEAD commit `e9b9d1789` inserted an ActionStacksEX `TryPeekAction` before the blocklist and short-circuited it with `!asRedirected &&`. When ASEX matches a stack, the blocklist never runs, so a redirected SGE defensive fires from the damage lane. Users with ActionStacksEX installed see the bug; users without it do not.

## Current state

- `WrathCombo/AutoRotation/AutoRotationController.cs` — Auto-Rotation two-pass probe. Nested class `AutoRotationHelper` owns `ExecuteAoE`, `ExecuteST`, `CanUseAutorotDpsAction`, `InvokeCombo`.
- `WrathCombo/Services/IPC_Subscriber/ActionStacksEXIPC.cs` — IPC wrapper. `TryPeekAction` returns false (and leaves the input action/target unchanged) when ASEX is missing, the action is unmatched, or the peek endpoint throws. Do not change this file.

AoE DPS branch today (`:1299-1321`):

```
OverrideTarget = target ?? OverrideTarget;
uint outAct = OriginalHook(InvokeCombo(preset, attributes, ref gameAct, OverrideTarget));
bool asRedirected = ActionStacksEXIPC.TryPeekAction(
    Service.Configuration.ActionChanging ? gameAct : outAct,
    OverrideTarget?.GameObjectId ?? player.GameObjectId,
    out var asResolvedAction,
    out var asResolvedTarget,
    out _);
if (asRedirected)
{
    outAct = asResolvedAction;
    if (asResolvedTarget != (OverrideTarget?.GameObjectId ?? player.GameObjectId))
    {
        var newTarget = asResolvedTarget.GetObject();
        if (newTarget is not null)
            OverrideTarget = newTarget;
    }
}
if (!asRedirected && !CanUseAutorotDpsAction(outAct))
{
    OverrideTarget = null;
    return false;
}
```

ST branch today (`:1394-1419`). Same peek, then:

```
if (!attributes.AutoAction!.IsHeal && !asRedirected && !CanUseAutorotDpsAction(outAct))
{
    OverrideTarget = null;
    return false;
}
```

Blocklist (`:1512-1537`) is SGE-only and lists Kardia, Rhizomata, Soteria, Druochole, Taurochole, Haima, Krasis, Zoe, Pepsis, Kerachole, Ixochole, Holos, Panhaima, Philosophia, Physis, Physis2, EukrasianDiagnosis, EukrasianPrognosis, EukrasianPrognosis2. It does **not** list Eukrasia. Leave the list alone.

Style: 4-space indent, braces on their own line, `uint outAct` in AoE and `var outAct` in ST. Match surrounding code. Do not reformat the rest of the method.

Repo constraint from `CLAUDE.md`: do **not** apply healer DPS-lane `IsSelectingAutorotAction` gates to tanks/DPS. This plan only restores the existing SGE blocklist so it also runs after an ASEX redirect.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Drift check | `git diff --stat e9b9d1789..HEAD -- WrathCombo/AutoRotation/AutoRotationController.cs` | empty, or only this plan's edits |
| Confirm the two bypass sites | `rg -n "asRedirected && !CanUseAutorotDpsAction" WrathCombo/AutoRotation/AutoRotationController.cs` | exactly two matches, lines 1317 and 1415 |
| Plugin build (optional, Windows with Dalamud hooks) | `dotnet build WrathCombo/WrathCombo.csproj -c Release` | 0 errors |
| Tests (will still be red until plan 002) | `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --nologo` | Failed 2, Passed 54 — same two facts as HEAD. Do **not** "fix" the tests in this plan. |

Working directory: `C:\Users\kruil\Documents\Projects\ffxiv-tools\ParseLord5`.

## Scope

**In scope**:
- `WrathCombo/AutoRotation/AutoRotationController.cs` — the two `if` conditions named above, nothing else.

**Out of scope**:
- `WrathCombo.Tests/RotationStructureTests.cs` — plan 002 rewrites those regexes against the post-fix shape.
- Adding `OverrideTarget = null` on other early returns — plan 003.
- `ActionStacksEXIPC.cs`, healer `actCheck` peek at `:570-585`, `CanUseAutorotDpsAction` body, job combo files, SCH/AST raidwide gates, experimental flags.

## Git workflow

- Branch: `advisor/improve-f1-f5` (shared with 002–005). Create it from `main` at `e9b9d1789` if it does not exist. AGENTS.md: at most one work branch besides `main`.
- Commit message style from recent log: `fix(autorot): keep SGE DPS-lane blocklist after ActionStacksEX peek`
- Do NOT push or open a PR unless the operator instructed it.
- Do not add a co-author trailer.

## Steps

### Step 1: Confirm the two sites

Run:

```
rg -n "asRedirected && !CanUseAutorotDpsAction" WrathCombo/AutoRotation/AutoRotationController.cs
```

Expect exactly:

```
1317:                if (!asRedirected && !CanUseAutorotDpsAction(outAct))
1415:            if (!attributes.AutoAction!.IsHeal && !asRedirected && !CanUseAutorotDpsAction(outAct))
```

If the line numbers drifted but the conditions still contain `!asRedirected && !CanUseAutorotDpsAction`, proceed on those lines. If either condition is already without `!asRedirected &&`, STOP — the bug may already be fixed.

### Step 2: Drop the short-circuit, keep the heal-lane exemption

Replace the AoE condition:

old: `if (!asRedirected && !CanUseAutorotDpsAction(outAct))`
new: `if (!CanUseAutorotDpsAction(outAct))`

Replace the ST condition:

old: `if (!attributes.AutoAction!.IsHeal && !asRedirected && !CanUseAutorotDpsAction(outAct))`
new: `if (!attributes.AutoAction!.IsHeal && !CanUseAutorotDpsAction(outAct))`

Do not change the bodies. `asRedirected` stays in scope; it is still used to rewrite `outAct` / `OverrideTarget` above each `if`. After this change the blocklist runs on the **resolved** action id, which is the point.

**Verify**:

```
rg -n "asRedirected && !CanUseAutorotDpsAction" WrathCombo/AutoRotation/AutoRotationController.cs
```

Expect no matches.

```
rg -n "CanUseAutorotDpsAction\(outAct\)" WrathCombo/AutoRotation/AutoRotationController.cs
```

Expect three matches: the method definition at ~1512 and the two call sites. Each call site must **not** be preceded by `!asRedirected &&` on the same line.

### Step 3: Confirm you did not touch tests or other files

```
git diff --name-only
```

Expect only `WrathCombo/AutoRotation/AutoRotationController.cs` (and `plans/README.md` if you update the status row).

## Test plan

No new tests in this plan. Plan 002 updates `AutorotationDpsLane_BlocksSgeDefensiveActions` so it asserts the post-fix shape: `InvokeCombo` then `CanUseAutorotDpsAction` **without** an `asRedirected` short-circuit.

Until 002 lands, `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` stays Failed 2 / Passed 54. That is expected. Do not edit the test file to make it green.

## Done criteria

- [ ] AoE call site is `if (!CanUseAutorotDpsAction(outAct))`
- [ ] ST call site is `if (!attributes.AutoAction!.IsHeal && !CanUseAutorotDpsAction(outAct))`
- [ ] `rg -n "asRedirected && !CanUseAutorotDpsAction" WrathCombo/AutoRotation/AutoRotationController.cs` returns no matches
- [ ] `CanUseAutorotDpsAction` method body is unchanged
- [ ] No files outside the in-scope list are modified except `plans/README.md`
- [ ] `plans/README.md` status row for 001 is DONE

## STOP conditions

- Drift check shows `AutoRotationController.cs` changed in ways that no longer contain the two `!asRedirected && !CanUseAutorotDpsAction` conditions.
- Either call site already dropped the short-circuit.
- Making the change appear to require editing `CanUseAutorotDpsAction`, job files, or tests.
- A verification command fails twice after a reasonable fix attempt.

## Maintenance notes

- Reviewer: confirm the blocklist now sees `asResolvedAction`, not the pre-peek id. An ASEX stack that turns Dosis into Kerachole must be rejected in the DPS lane.
- `asRedirected` remains a local bool used only to apply the peek rewrite. Do not delete it.
- Plan 002 must land next so the structural lint matches this shape. Do not leave the suite red.
- Heal-lane `ExecuteST` still skips the blocklist via `!attributes.AutoAction!.IsHeal`. That is intentional — heals belong on the heal pass.
