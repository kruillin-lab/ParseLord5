# Plan 003: Clear OverrideTarget on every ExecuteST/ExecuteAoE early return

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat e9b9d1789..HEAD -- WrathCombo/AutoRotation/AutoRotationController.cs WrathCombo/CustomCombo/Functions/Target.cs`
> If either file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition. Plan 001's two-line blocklist
> edit in this same controller is expected.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: MED
- **Depends on**: none (run after 001 if sharing a branch, so diffs stay separable)
- **Category**: bug
- **Planned at**: commit `e9b9d1789`, 2026-08-25

## Why this matters

`OverrideTarget` is static. `CurrentTarget` prefers it over the player's hard target (`WrathCombo/CustomCombo/Functions/Target.cs:27-42`). `ExecuteAoE` / `ExecuteST` set it, then several early returns leave it set. Combo logic that reads `CurrentTarget` then aims at the autorot override until a later successful send (`ActionWatching.cs:491`) or autorot disable (`AutoRotationController.cs:485`) clears it. Failed moving-cast checks, out-of-combat gates, and cannot-use-on-self exits are the leak.

## Current state

Setter/getter (`Target.cs:27-38`), do not edit:

```
public static IGameObject? OverrideTarget
{
    get
    {
        var ret = OverrideTargetID.GetObject();
        if (ret == null || ret.IsDead)
            OverrideTargetID = null;
        return ret;
    }
    set => OverrideTargetID = value?.GameObjectId;
}
```

`ExecuteAoE` DPS branch (`:1299-1373`) sets `OverrideTarget = target ?? OverrideTarget;` then:

| Line | Code | Clears OverrideTarget? |
|------|------|------------------------|
| 1319 | CanUseAutorotDpsAction fail | yes |
| 1322 | `outAct is All.SavageBlade return true` | **no** |
| 1325 | !CanQueue | yes |
| 1337 | moving + cast time + not orbwalking `return false` | **no** |
| 1373 | fallthrough `return false` | **no** |
| 1369 | successful use `return true` | no (send detour clears) |

`ExecuteST` (`:1388-1481`) sets `OverrideTarget = target ?? OverrideTarget;` at 1394, after the pause-no-target return at 1391 (that return is fine). Then:

| Line | Code | Clears OverrideTarget? |
|------|------|------------------------|
| 1417 | DPS blocklist fail | yes |
| 1425 | !ActionReady | yes |
| 1440 | in-combat-only gate `return false` | **no** |
| 1443 | `target is null && !canUseSelf` | **no** |
| 1463 | moving + cast time + not orbwalking `return false` | **no** |
| 1481 | fallthrough `return false` | **no** |
| 1478 | successful use `return true` | no (send detour clears) |

Successful `return true` paths must **not** clear before `UseAutorotAction`: the send detour in `ActionWatching.cs:491` clears after the action is issued. Clearing earlier would drop the override the combo needs during invoke.

Preferred shape — wrap from the assignment through the end of each method:

```
OverrideTarget = target ?? OverrideTarget;
var issued = false;
try
{
    // existing body; on the successful UseAutorotAction path set issued = true before return true
}
finally
{
    if (!issued)
        OverrideTarget = null;
}
```

That also covers future early returns. Existing inner `OverrideTarget = null; return false;` pairs become redundant; delete them inside the try so a later reader does not think those are the only clears.

Do not wrap `ExecuteAoE`'s heal branch (`:1235-1275`). That branch never assigns `OverrideTarget`.

Match surrounding style: 4-space indent, braces on their own line.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Drift check | `git diff --stat e9b9d1789..HEAD -- WrathCombo/AutoRotation/AutoRotationController.cs WrathCombo/CustomCombo/Functions/Target.cs` | controller may include plan 001; Target.cs unchanged |
| Tests | `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --nologo` | Failed 0, Passed 56 if 002 landed; otherwise still the two lint failures only |
| Optional plugin build | `dotnet build WrathCombo/WrathCombo.csproj -c Release` | 0 errors |

Working directory: `C:\\Users\\kruil\\Documents\\Projects\\ffxiv-tools\\ParseLord5`.

## Scope

**In scope**:
- `WrathCombo/AutoRotation/AutoRotationController.cs` — `AutoRotationHelper.ExecuteAoE` DPS branch and `AutoRotationHelper.ExecuteST` only.

**Out of scope**:
- `Target.cs`, `ActionWatching.cs`.
- Heal-lane `ExecuteAoE` (`:1235-1275`).
- Plan 001 blocklist conditions, plan 002 tests.
- Extracting peek resolution into a pure function.
- Changing `OverrideTarget` getter semantics.

## Git workflow

- Branch: `advisor/improve-f1-f5`.
- Commit message: `fix(autorot): clear OverrideTarget when ExecuteST/AoE does not issue`
- Do NOT push or open a PR unless the operator instructed it.
- Do not add a co-author trailer.

## Steps

### Step 1: Locate the two assignment sites

```
rg -n "OverrideTarget = target \\?\\? OverrideTarget" WrathCombo/AutoRotation/AutoRotationController.cs
```

Expect two hits, currently 1299 (ExecuteAoE DPS) and 1394 (ExecuteST). If missing, STOP.

### Step 2: Wrap ExecuteAoE DPS branch

Inside `ExecuteAoE`, the `else` starting near `:1276` is the DPS branch. After `OverrideTarget = target ?? OverrideTarget;` introduce `var issued = false;` and a `try/finally` through the end of that else (the `return false` at `:1373` is inside it).

On the successful path that currently ends with `return true;` after `UseAutorotAction` (`:1369`), set `issued = true` immediately before `return true`.

SavageBlade (`:1322` `if (outAct is All.SavageBlade) return true;`) must **not** set `issued`. That return does not send an action, so finally must clear.

Moving-cast fail at `:1337` has no inner clear today; finally handles it.

**Verify by reading the method**: every `return false` and the SavageBlade `return true` leave `issued == false`. Only the post-`UseAutorotAction` `return true` sets `issued = true`.

### Step 3: Wrap ExecuteST

Same pattern from `OverrideTarget = target ?? OverrideTarget;` (`:1394`) to the method's final `return false` (`:1481`).

The pause-no-target return at `:1388-1392` is **before** the assignment. Leave it outside the try.

Successful path `:1478` `return true;` after `UseAutorotAction`: set `issued = true` first.

Early returns at 1440, 1443, 1463, 1481 currently leak; finally covers them.

### Step 4: Confirm Target.cs untouched

```
git diff --name-only
```

Must not list `WrathCombo/CustomCombo/Functions/Target.cs`.

### Step 5: Build/test

If 002 has landed:

```
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --nologo
```

Expect Failed 0, Passed 56. These tests do not cover OverrideTarget; they must not go red from this change. If a structural lint fails because you rewrote `InvokeCombo` / `CanUseAutorotDpsAction` lines, STOP and restore those lines — this plan must not touch them.

## Test plan

No new tests. `WrathCombo.Tests` cannot compile `AutoRotationController.cs` (Dalamud). Characterization of OverrideTarget is F8, not this plan.

Manual in-game check (not required for Done): on a caster, enable autorot, start moving during a cast-time GCD so ExecuteST returns false at the moving check, then press a combo button that uses `CurrentTarget`. Target should still be the hard target, not a stale override.

## Done criteria

- [ ] `ExecuteAoE` DPS branch and `ExecuteST` each have a `try/finally` that sets `OverrideTarget = null` when `issued` is false
- [ ] The only `issued = true` assignments are immediately before `return true` on a path that called `UseAutorotAction`
- [ ] SavageBlade still returns true without setting `issued`
- [ ] Pause-no-target in ExecuteST remains before the OverrideTarget assignment
- [ ] `Target.cs` unmodified
- [ ] Plan 001's two `CanUseAutorotDpsAction` conditions still present without `!asRedirected &&`
- [ ] `dotnet test` does not lose passing tests compared to the pre-step baseline
- [ ] `plans/README.md` status row for 003 is DONE

## STOP conditions

- Assignment sites are gone or `OverrideTarget` is no longer static on `CustomComboFunctions`.
- Implementing this appears to require editing `Target.cs` or the send detour.
- A try/finally would also wrap the heal `ExecuteAoE` branch that never assigns OverrideTarget — do not wrap that branch; restructure so only the DPS else is wrapped.
- Tests fail for reasons other than the two pre-existing lint facts (if 002 has not landed).

## Maintenance notes

- Reviewer: walk every `return` in both methods. Any new `return false` after the assignment is covered by finally; any new `return true` that does not send must not set `issued`.
- `ActionWatching` send detour still clears on a successful send. Double-clear is fine (setter stores null).
- Do not clear on successful issue before `UseAutorotAction`: combos invoked during the send still read `CurrentTarget` then OverrideTarget.
