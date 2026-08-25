# Plan 004: Stamp the source checkout into devPlugins and refuse cross-tree overwrite

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat e9b9d1789..HEAD -- WrathCombo/WrathCombo.csproj scripts/sync-dev-build.ps1 WrathCombo/ParseLord5.json`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: MED
- **Depends on**: none
- **Category**: dx
- **Planned at**: commit `e9b9d1789`, 2026-08-25

## Why this matters

`WrathCombo.csproj` sends Debug and Release to `%AppData%\\XIVLauncher\\devPlugins\\ParseLord5\\`. Two checkouts exist:

- Canonical for this plan: `C:\\Users\\kruil\\Documents\\Projects\\ffxiv-tools\\ParseLord5` at `e9b9d1789`
- Second: `C:\\Users\\kruil\\orca\\ParseLord5` at `e367c8220`, dirty, including divergent `AutoRotationController.cs`

Building either tree silently replaces the live plugin. `docs/AGENTS_FULL.md` currently names `orca` as the tree to build from (plan 005 fixes that text). This plan makes a cross-tree overwrite fail closed without changing `OutputPath` (changing OutputPath breaks `scripts/sync-dev-build.ps1:61-62`, which asserts the DLL at that exact path).

Do **not** delete `orca\\ParseLord5`. That is a human decision (D3).

## Current state

`WrathCombo/WrathCombo.csproj:43-65`:

```
<DalamudLibPath>$(appdata)\\XIVLauncher\\addon\\Hooks\\dev\\</DalamudLibPath>
<DalamudDevPlugins>$(appdata)\\XIVLauncher\\devPlugins\\ParseLord5\\</DalamudDevPlugins>
...
<PropertyGroup Condition="'$(Configuration)' == 'Debug' Or '$(Configuration)' == 'Release'">
    <OutputPath>$(DalamudDevPlugins)</OutputPath>
</PropertyGroup>
```

Leave `OutputPath` as-is.

`scripts/sync-dev-build.ps1:57-62,116-122`:

```
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
...
$DevPluginsDir = Join-Path $env:APPDATA 'XIVLauncher\\devPlugins\\ParseLord5'
$DllPath = Join-Path $DevPluginsDir 'ParseLord5.dll'
...
dotnet build $ProjectFile -c $Configuration --nologo -v minimal
if (-not (Test-Path -LiteralPath $DllPath)) {
    throw "Build succeeded but DLL missing: $DllPath"
}
```

`WrathCombo/ParseLord5.json` is the Dalamud manifest copied to output. Do not put a machine-local path in this committed file.

Required approach:

1. MSBuild writes a sidecar `devplugin-source.txt` next to the DLL containing the normalized repo root that produced the build.
2. A BeforeTargets=Build target reads that sidecar if present. If it names a different repo root than the tree being built, fail unless `ForceDevPluginOverwrite=true`.
3. `sync-dev-build.ps1` does the same check before `dotnet build`, with `-ForceDevPluginOverwrite`.
4. After a successful build, overwrite the sidecar with this tree's root.

Sidecar is build output under `%AppData%\\XIVLauncher\\devPlugins\\ParseLord5\\`, not in git.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Drift check | `git diff --stat e9b9d1789..HEAD -- WrathCombo/WrathCombo.csproj scripts/sync-dev-build.ps1 WrathCombo/ParseLord5.json` | empty until this plan |
| Plugin build | `dotnet build WrathCombo/WrathCombo.csproj -c Release` | 0 errors; sidecar written |
| Confirm sidecar | `Get-Content "$env:APPDATA\\XIVLauncher\\devPlugins\\ParseLord5\\devplugin-source.txt"` | this repo root |

Working directory: `C:\\Users\\kruil\\Documents\\Projects\\ffxiv-tools\\ParseLord5`.

## Scope

**In scope**:
- `WrathCombo/WrathCombo.csproj` — add a target that writes/checks `devplugin-source.txt`
- `scripts/sync-dev-build.ps1` — check sidecar before build; add `-ForceDevPluginOverwrite`

**Out of scope**:
- Changing `OutputPath` / `DalamudDevPlugins`.
- Deleting or merging `C:\\Users\\kruil\\orca\\ParseLord5`.
- `docs/AGENTS_FULL.md` path corrections — plan 005.
- Committed `ParseLord5.json` contents.

## Git workflow

- Branch: `advisor/improve-f1-f5`.
- Commit message: `fix(build): refuse devPlugins overwrite from a different checkout`
- Do NOT push or open a PR unless the operator instructed it.
- Do not add a co-author trailer.

## Steps

### Step 1: Add the MSBuild guard to WrathCombo.csproj

After the existing `OutputPath` property group, add:

```
<PropertyGroup>
  <ForceDevPluginOverwrite Condition="'$(ForceDevPluginOverwrite)' == ''">false</ForceDevPluginOverwrite>
  <DevPluginSourceStamp>$(DalamudDevPlugins)devplugin-source.txt</DevPluginSourceStamp>
  <DevPluginSourceRoot>$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..'))</DevPluginSourceRoot>
</PropertyGroup>

<Target Name="GuardDevPluginSource" BeforeTargets="Build">
  <PropertyGroup>
    <_StampText Condition="Exists('$(DevPluginSourceStamp)')">$([System.IO.File]::ReadAllText('$(DevPluginSourceStamp)').Trim())</_StampText>
  </PropertyGroup>
  <Error
    Condition="'$(ForceDevPluginOverwrite)' != 'true' And Exists('$(DevPluginSourceStamp)') And '$(_StampText)' != '' And '$(_StampText)' != '$(DevPluginSourceRoot)'"
    Text="devPlugins ParseLord5 was built from '$(_StampText)', not '$(DevPluginSourceRoot)'. Build the stamped checkout, or pass /p:ForceDevPluginOverwrite=true." />
</Target>

<Target Name="WriteDevPluginSourceStamp" AfterTargets="Build">
  <WriteLinesToFile File="$(DevPluginSourceStamp)" Lines="$(DevPluginSourceRoot)" Overwrite="true" Encoding="UTF-8" />
</Target>
```

Keep `OutputPath` unchanged. Path compare may be case-sensitive in MSBuild. If a same-tree build errors only because of trailing slash or `\\` vs `/`, normalize both sides with GetFullPath and TrimEnd of directory separators before compare. Do not switch to a case-insensitive compare unless you actually hit a casing mismatch on this machine.

**Verify**: `dotnet build WrathCombo/WrathCombo.csproj -c Release` exits 0. Sidecar exists and contains this checkout's root. If the first build fails because a leftover sidecar from a previous orca build names the other tree, that is the guard working. Re-run once with `dotnet build WrathCombo/WrathCombo.csproj -c Release -p:ForceDevPluginOverwrite=true`, then a normal build must succeed without the property.

Missing sidecar must not error (Exists check). First run writes the stamp.

### Step 2: Guard sync-dev-build.ps1

Add to the param block: `[switch] $ForceDevPluginOverwrite`

After `$DllPath` is set and before `dotnet build`:

- `$StampPath = Join-Path $DevPluginsDir 'devplugin-source.txt'`
- If the stamp file exists, read it, trim, and compare to `$RepoRoot.Path` case-insensitively. If different and `-ForceDevPluginOverwrite` was not passed, throw a message that includes both paths and mentions the switch.
- After the existing DLL existence check, write `$RepoRoot.Path` to the stamp file (UTF8).

Do not change `$DevPluginsDir` or `$DllPath`. The throw happens before `dotnet build`.

### Step 3: Do not touch orca

Do not `cd` into `C:\\Users\\kruil\\orca\\ParseLord5` to test the failure by building it. That is the hazard. The Force flag plus the Error text are the verification.

## Test plan

No xUnit tests. Verification is:

1. Normal `dotnet build` from this checkout succeeds and writes the sidecar.
2. `Get-Content` of the sidecar equals this repo root.
3. `git diff` shows only csproj + script (and plans/README.md).
4. `git diff -- WrathCombo/ParseLord5.json` is empty.

## Done criteria

- [ ] `OutputPath` / `DalamudDevPlugins` unchanged
- [ ] `GuardDevPluginSource` runs before Build and errors on a mismatched sidecar unless `ForceDevPluginOverwrite=true`
- [ ] `WriteDevPluginSourceStamp` runs after Build
- [ ] `sync-dev-build.ps1` has `-ForceDevPluginOverwrite` and the same check before `dotnet build`
- [ ] Committed `ParseLord5.json` unchanged
- [ ] `orca\\ParseLord5` not deleted or modified
- [ ] `plans/README.md` status row for 004 is DONE

## STOP conditions

- Operator asks to change `OutputPath` instead — report, do not do it under this plan.
- Sidecar would have to live in the git tree.
- csproj target syntax fails to evaluate GetFullPath / ReadAllText on this MSBuild — STOP and report the error rather than shelling out to a pre-build powershell that always overwrites.
- Guard fires on every build from this same tree because of path-normalization mismatch. Fix Trim + GetFullPath once. If still firing, STOP.

## Maintenance notes

- Reviewer: first build after this lands may need `-p:ForceDevPluginOverwrite=true` only if a sidecar already names another tree. Missing sidecar must not fail.
- Plan 005 will tell agents this checkout is canonical. Once 004 is in, building orca without Force fails.
- Do not add the sidecar path to the Dalamud manifest.
