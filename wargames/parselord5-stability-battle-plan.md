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
# ParseLord5 Stability Battle Plan (Rotation Hardening)

Date: 2026-07-06 (supersedes and replaces the 2026-07-05 draft in full)
Repository: `/home/kruillin/Projects/Projects/ParseLord5`
Branch: `parselord5-wc-base` (HEAD at plan time: `9be67b9a8`)
Mode: wargame only. This document is the executor route; it does not apply fixes.

**Mission:** the user reports rotations "constantly breaking and needing repair even though
the game is not being updated." Recon confirms the breakage source is internal: a fork
churn cycle (experiment promotions deleting baseline code, large upstream merges) landing
on a codebase with latent input-handling defects and **no behavioral regression net**.
This plan (a) fixes the five evidence-backed defects that produce "rotation broke"
symptoms, and (b) installs a bounded regression net so future breakage is caught at
`dotnet test` time or surfaced in logs instead of discovered mid-combat.

**Supersession note:** this plan replaces the 2026-07-05 draft (user-authorized). The
draft's top-3 targets (double-send, config wedge, manual-GCD swallow) were independently
rediscovered by this session's four-zone audit and are MOVE 2, MOVE 1, MOVE 4 here; its
distrobox build prescription was wrong for this host (`~/.dotnet/dotnet` works directly).
Every move's proof-gate still detects whether a fix was already applied by any earlier
run (→ skip that move, record "pre-applied" in the report).

---

## 0. Theatre map

### Source root & tree state

- Root: `/home/kruillin/Projects/Projects/ParseLord5`
- `[R0]` If the repo moved: `ls /home/kruillin/Projects/Projects/ParseLord5/WrathCombo/WrathCombo.csproj || find /home/kruillin/Projects -maxdepth 3 -name "WrathCombo.slnx" 2>/dev/null` — if not found at the canonical path, use the found root and re-verify branch before anything else.
- Branch must be `parselord5-wc-base`. Verify: `git branch --show-current`.
- **Three uncommitted user files — DO NOT TOUCH, DO NOT REVERT, DO NOT REFORMAT:**
  `WrathCombo/Combos/PvE/BRD/BRD.cs`, `WrathCombo/Combos/PvE/DRK/DRK.cs`,
  `WrathCombo/Combos/PvE/RDM/RDM.cs` (user-added weave-gates on standalone oGCD features).
  If `git status --short` shows them missing or committed, that is fine; if it shows
  *other* modified files not created by this plan, stop and report (ABORT-DRIFT).

### Toolchain facts (verified 2026-07-06 on this host)

- dotnet SDK 10.0.301 at `~/.dotnet/dotnet` — **not on PATH**. Prefix every dotnet
  command with: `export PATH="$HOME/.dotnet:$PATH"`
- `pwsh` is **not installed** → `scripts/rotation-evals.ps1` and quality-gate.json's
  PowerShell commands cannot run on this host. Do not attempt them; MOVE 6 ports their
  substance to xUnit.
- Canonical build (Release): `dotnet build WrathCombo/WrathCombo.csproj -c Release`
  — **Baseline: 0 errors, exactly 3 warnings** (CS0219 `UserConfig.cs(56)`,
  CS0169 `ActionWatching.cs(80)`, CS0649 `CustomActionManager.cs(343)` — all three are
  known-benign; CS0649 is an EzHook reflection false positive, CS0169 is commented-out
  dead code. Do not "fix" them).
- Canonical tests: `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj`
  — **Baseline: Passed 34, Failed 0** (~1 min including build).
- Debug build deploys to `~/.xlcore/devPlugins/ParseLord5/`; the live dev-plugin ALSO
  reads the flat dir. Deploy step after a Debug build:
  `for f in ParseLord5.dll ParseLord5.json ParseLord5.pdb ParseLord5.deps.json; do cp ~/.xlcore/devPlugins/ParseLord5/$f ~/.xlcore/devPlugins/$f; done`

### Root-cause summary (why rotations keep breaking without game patches)

1. Git history is a regression↔repair cycle: `20f5f1ac4` (experiment promotion) deleted
   WAR/GNB mitigation ladders outright → repaired in `9be67b9a8`; earlier: "Fix RDM
   custom action brick", "Fix RPR custom action", "fix(pld): resolve OGCD deadlocks".
2. Merge `16d063275` pulled 359 upstream commits across fork-touched files.
3. The only domain checks are structural (preset enum/coverage/IDs) in a pwsh script
   that can't run here; the 34 unit tests cover only 3 pure-math services. Every
   rotation edit ships behaviorally untested.
4. Latent input-handling defects (targets table below) make ordinary play produce
   "rotation stopped / input vanished / settings reverted" symptoms indistinguishable
   from fresh breakage — inflating the perceived breakage rate.

### Targets table

| # | Defect (one sentence) | File:line | Severity | Verified by |
|---|---|---|---|---|
| T1 | Debug-config mode leaks `_isSaving=true`, permanently wedging the config save queue — all later settings changes silently lost until plugin reload | `WrathCombo/Core/ConfigurationHelper.cs:49-58` (+ same leak `:82-87`) | silent config loss | planner read |
| T2 | `SendActionDetour` catch re-sends `Original(...)` after it already ran, double-sending the action packet if post-send re-enable throws | `WrathCombo/Data/ActionWatching.cs:466-477` | silent misbehavior / possible crash | planner read |
| T3 | `IsManualActionOverrideCandidate` doesn't exclude custom-action IDs (≥1,000,000), so pressing a custom one-button mid-GCD sends an invalid ID no-op AND suppresses auto-rotation output | `WrathCombo/AutoRotation/AutoRotationController.cs:227-232` + `WrathCombo/Data/ActionWatching.cs:568-598`; constant at `WrathCombo/Combos/PvE/ALL/ALL.cs:14` | silent misbehavior — matches user's "rotation stalls" complaint | planner read + audit trace |
| T4 | Swallowed manual GCD press (`return false` at `ActionWatching.cs:588-592`) is silently discarded by bail/expiry paths while `ManualOverrideUntil` keeps suppressing autorot | `WrathCombo/AutoRotation/AutoRotationController.cs:264-321` (bails `:285-295`, expiry `:190-200`) | silent input loss + stall window | audit trace, premise proof-gated |
| T5 | Eukrasia is added to `BlacklistedRaidwides` without bumping the clear-guard counter, so the blacklist can survive the raidwide window and block SGE's shield response on every future raidwide | `WrathCombo/Data/ActionWatching.cs:402-407` + `WrathCombo/AutoRotation/AutoRotationController.cs:374-381` | silent healing degradation | planner read (both sites) |

### Hardening targets (Tier 2 — the regression net)

| # | Measure | Where |
|---|---|---|
| H1 | Port rotation-evals' structural checks to xUnit so `dotnet test` guards preset structure cross-platform | new `WrathCombo.Tests/RotationStructureTests.cs` |
| H2 | Rotation stall watchdog: throttled log when autorot is on, in combat, has target, but nothing was sent for 3 GCDs — converts future "it broke" into log evidence | `WrathCombo/AutoRotation/AutoRotationController.cs` (`Run()`) |
| H3 | Per-GCD mitigation guard on the restored GNB/WAR fallback ladders (prevent smart-path + fallback double-fire across consecutive invokes) | `GNB_Helper.cs`, `WAR_Helper.cs` — gated by `[R1]` |

### Deferred ledger (evidence-backed, out of scope this mission — do NOT fix now)

- Unprefixed IPC gate `"OnActionUsed"` collides with co-installed real WrathCombo — `WrathCombo/Services/IPC/Provider.cs:94` (only matters when both plugins installed).
- Upstream kill-switch/MOTD: `Services/IPC/Helper.cs:389` + `WrathCombo.cs:453` fetch PunishXIV URLs; local `res/ipc_status.txt`, `res/motd.txt` are dead.
- `UiBuilder.Draw += ws.Draw` vs Dispose `Draw -= DrawUI` mismatch + unsubscribed `OpenMainUi`/`ErrorToast`/`LanguageChanged` — `WrathCombo.cs:231/503` (mitigated by Dalamud scoped disposal).
- Dead `ParseLord5ExperimentalMode` toggle (UI no-op) — `Core/Configuration.cs:47-50`.
- `DebugFile.cs:1017,1057` filters Dalamud log lines on `"WrathCombo"` → empty sections.
- Party cache never evicts ex-members still rendered in zone — `CustomCombo/Functions/Party.cs:34-114` (fix shape: also `RemoveAll` entries whose GameObjectId is no longer in `Svc.Party` when in a real party).
- `WouldLikeToGroundTarget` set/reset not exception-safe — `AutoRotationController.cs:297-308,508-510,596-598,1199-1201`.
- `UpdatingActions` stuck true if the RunOnTick callback throws — `ActionWatching.cs:440-443` vs clear at `:409`.
- Retarget NRE (caught, error-spam only) — `ActionWatching.cs:634-642`.

---

## 1. Move sequence

### MOVE 0 — Preflight baseline

```bash
export PATH="$HOME/.dotnet:$PATH"
cd /home/kruillin/Projects/Projects/ParseLord5
git branch --show-current
git status --short
git log --oneline -3
dotnet build WrathCombo/WrathCombo.csproj -c Release 2>&1 | tail -4
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj 2>&1 | tail -3
```

- **Expected observation:** branch `parselord5-wc-base`; status shows at most the three
  user files (BRD/DRK/RDM) modified; build tail `0 Error(s)` with `3 Warning(s)`;
  test tail `Passed! - Failed: 0, Passed: 34`.
- **Most likely failure:** build errors about `AtkUnitBase`/`FFXIVClientStructs`.
  **Cause:** Dalamud hook DLLs missing/stale at `~/.xlcore/dalamud/Hooks/dev/`.
  **Counter:** re-download `https://goatcorp.github.io/dalamud-distrib/latest.zip` into
  that dir, rebuild once; if still failing → ABORT-ENV.
- **TRIGGER — status shows modified files beyond BRD/DRK/RDM:** stop, run
  `git diff --stat`, report the list, and route to ABORT-DRIFT unless the extra files
  are exactly the ones this plan edits (a partial prior run — then diff each against
  the fix shapes below; matching fixes = resume after them).
- **TRIGGER — tests fail at baseline:** capture output; do not fix unrelated tests;
  ABORT-ENV (the net must start green or pass criteria mean nothing).

### MOVE 1 — Fix T1: config save wedge

**Proof to capture first (read-only):**
```bash
sed -n '47,60p' WrathCombo/Core/ConfigurationHelper.cs
sed -n '80,90p' WrathCombo/Core/ConfigurationHelper.cs
```
- **Expected:** `ProcessSaveQueue` sets `_isSaving = true;` (line ~51) *before* the
  `if (Debug.DebugConfig)` block (~54-59) which `return`s without resetting it; and
  `RetrySave` has the same early-return shape at ~82-87.
- **If the proof shows `_isSaving = false;` already present before those returns:** the
  old plan already fixed it — skip, record "pre-applied" in report.
- **The fix (both sites):** add one line before each `return`:

  In `ProcessSaveQueue` (inside the `Debug.DebugConfig` block, before `return;`):
  ```csharp
            _isSaving = false;
  ```
  In `RetrySave` (inside its `Debug.DebugConfig` block, before `return;`):
  ```csharp
        _isSaving = false;
  ```
  Do not restructure the method; the flag reset is the entire fix. (Why not move the
  check above the dequeue: the warning message intentionally logs the dequeued item's
  stack trace.)
- **Expected observation after edit:** `grep -n "_isSaving = false" WrathCombo/Core/ConfigurationHelper.cs`
  shows **4** hits (lines ~58, ~66, ~112, ~117 — two pre-existing, two new).
- **Most likely failure:** wrong indentation/paste position makes the build fail with
  CS1513/CS0103. **Cause:** line inserted outside the `if` block. **Counter:** re-read
  the 47-60 window; the new line goes between the `PluginLog.Warning(...)` call and
  `return;`.

### MOVE 2 — Fix T2: SendActionDetour double-send

**Proof to capture first (read-only):**
```bash
sed -n '413,417p' WrathCombo/Data/ActionWatching.cs
sed -n '464,478p' WrathCombo/Data/ActionWatching.cs
```
- **Expected:** the detour signature at ~413; `SendActionHook!.Original(...)` inside the
  try at ~466, then `OverrideTarget = null; ... EnableActionReplacingIfRequired();`, and
  a catch (~472-477) that calls `EnableActionReplacingIfRequired()` then **re-invokes
  `SendActionHook!.Original(...)`**.
- **If the catch already checks an `originalSent` flag:** pre-applied — skip.
- **The fix:** track whether Original ran; make the catch's re-enable unable to mask the
  original exception:
  ```csharp
    private unsafe static void SendActionDetour(ulong targetObjectId, byte actionType, uint actionId, ushort sequence, long a5, long a6, long a7, long a8, long a9)
    {
        var originalSent = false;
        try
        {
  ```
  (add `var originalSent = false;` as the first line of the method body, before `try`),
  change line ~466 to:
  ```csharp
            SendActionHook!.Original(targetObjectId, actionType, actionId, sequence, a5, a6, a7, a8, a9);
            originalSent = true;
  ```
  and replace the catch body with:
  ```csharp
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "SendActionDetour");
            try { Service.ActionReplacer.EnableActionReplacingIfRequired(); }
            catch (Exception ex2) { Svc.Log.Error(ex2, "SendActionDetour re-enable"); }
            if (!originalSent)
                SendActionHook!.Original(targetObjectId, actionType, actionId, sequence, a5, a6, a7, a8, a9);
        }
  ```
  The `originalSent` guard is what prevents the double-send; the nested try is what
  prevents a throwing re-enable from swallowing the send entirely. Do not remove either.
- **Expected observation after edit:** build green;
  `grep -n "originalSent" WrathCombo/Data/ActionWatching.cs` → 3 hits in this method.
- **Most likely failure:** compile error CS0165 (unassigned local). **Cause:**
  `originalSent` declared inside the try instead of before it. **Counter:** move the
  declaration to the first line of the method body.

### MOVE 3 — Fix T3: custom-action IDs leak into manual-override machinery

**Proof to capture first (read-only):**
```bash
sed -n '227,233p' WrathCombo/AutoRotation/AutoRotationController.cs
grep -n "SingleTargetDPS = " WrathCombo/Combos/PvE/ALL/ALL.cs
grep -n "actionId < All.SingleTargetDPS" WrathCombo/Data/ActionWatching.cs
```
- **Expected:** `IsManualActionOverrideCandidate` body is exactly:
  `!IsIssuingAutorotAction && cfg?.Enabled == true && actionId != 0 && !IsManualOverrideBlacklisted(actionId) && RemainingGCD > 0;`
  (no ID-range check); `SingleTargetDPS = 1_000_000` at ALL.cs:14; ActionWatching
  already uses the pattern `actionId < All.SingleTargetDPS` (~line 571), proving the
  symbol resolves from that class — and showing the intended idiom.
- **If the candidate check already contains `SingleTargetDPS`:** pre-applied — skip.
- **The fix** in `AutoRotationController.cs` (one added clause):
  ```csharp
    internal static bool IsManualActionOverrideCandidate(uint actionId) =>
        !IsIssuingAutorotAction &&
        cfg?.Enabled == true &&
        actionId != 0 &&
        actionId < All.SingleTargetDPS && // custom-action buttons are not manual presses
        !IsManualOverrideBlacklisted(actionId) &&
        RemainingGCD > 0;
  ```
- **Expected observation after edit:** build green. With this one change, custom-action
  presses fall through to the pre-existing custom-action handling at
  `ActionWatching.cs:604` instead of the manual-queue path, and no longer extend
  `ManualOverrideUntil`.
- **Most likely failure:** CS0103 `All` not found in this file. **Cause:** namespace
  import differs from ActionWatching. **Counter:** fully qualify:
  `WrathCombo.Combos.PvE.All.SingleTargetDPS` (do not add a new using unless the
  qualified name also fails).

### MOVE 4 — Fix T4: swallowed manual GCD leaves a suppression window

**Proof to capture first (read-only):**
```bash
sed -n '188,208p' WrathCombo/AutoRotation/AutoRotationController.cs
sed -n '283,296p' WrathCombo/AutoRotation/AutoRotationController.cs
```
- **Expected:** `HasManualQueuedAction()` (~190-200) calls `ClearManualQueuedAction()`
  on expiry and returns false; `TryUseManualQueuedAction` bails (~285-295) with
  `ClearManualQueuedAction(); return false;` when the stored target is gone/unusable.
  Neither path resets `ManualOverrideUntil`, so autorot stays suppressed for the rest
  of the override window even though the press it was protecting is already lost.
- **Scope note:** full replay of a dead-target press is *correctly* impossible (the
  game itself would reject it). The harm to remove is the leftover suppression window
  plus silence. The fix is minimal: release the override and log.
- **The fix:** in `ClearManualQueuedAction()` (~202-207), add one line and one log so
  every discard path is covered at once:
  ```csharp
    private static void ClearManualQueuedAction()
    {
        if (ManualQueuedActionId != 0)
            Svc.Log.Verbose($"[ManualQueue] Discarding queued manual action {ManualQueuedActionId}");
        ManualQueuedActionId = 0;
        ManualQueuedTargetId = 0;
        ManualQueuedUntil = 0;
        ManualOverrideUntil = 0;
    }
  ```
  **Caveat to check first:** `ClearManualQueuedAction` is also called on *success*
  (~line 312, after `ret == true`), where the success path immediately re-arms
  `ManualOverrideUntil = Environment.TickCount64 + ManualOverrideDurationMs;` right
  after the call — read `sed -n '308,320p'` to confirm the re-arm happens *after* the
  clear call. If it does (expected), zeroing inside the clear is safe. If the re-arm
  precedes the clear in the current code, instead add `ManualOverrideUntil = 0;` only
  at the two failure sites (the bail returns at ~287 and ~293) and the expiry site
  (~198), not inside the shared method.
- **Expected observation after edit:** build green; `grep -n "ManualOverrideUntil = 0" WrathCombo/AutoRotation/AutoRotationController.cs` shows ≥1 hit.
- **Most likely failure:** `Svc` not imported in that file. **Cause:** logging via a
  different facade there. **Counter:** `grep -n "Svc.Log" WrathCombo/AutoRotation/AutoRotationController.cs | head -3` and copy whatever logger idiom the file already uses.

### MOVE 5 — Fix T5: Eukrasia raidwide-blacklist leak

**Proof to capture first (read-only):**
```bash
sed -n '400,409p' WrathCombo/Data/ActionWatching.cs
sed -n '372,382p' WrathCombo/AutoRotation/AutoRotationController.cs
```
- **Expected:** ActionWatching adds to `BlacklistedRaidwides` with no `Contains` check
  and skips the `AutorotRaidwides++` for `SGE.Eukrasia`; the controller clears the
  blacklist only inside `if (AutorotRaidwides > 0)`.
- **The fix — two small edits:**
  1. `ActionWatching.cs` (~402-407): guard the add against duplicates:
     ```csharp
        if (AutoRotationController.AutorotRaidwiding && AutoRotationController.RaidwideActions.Any(x => x.Action == actionId))
        {
            if (!AutoRotationController.BlacklistedRaidwides.Contains(actionId))
                AutoRotationController.BlacklistedRaidwides.Add(actionId);
            if (actionId != SGE.Eukrasia)
                AutoRotationController.AutorotRaidwides++;
        }
     ```
  2. `AutoRotationController.cs` (~374-381): always clear the blacklist when leaving
     the raidwide state, keeping the log inside the counter guard:
     ```csharp
                if (AutorotRaidwides > 0)
                {
                    Svc.Log.Debug($"Used {AutorotRaidwides} raidwides {string.Join(", ", BlacklistedRaidwides.Select(x => x.ActionName()))}");
                }
                BlacklistedRaidwides.Clear();
                AutorotRaidwides = 0;
                AutorotRaidwiding = false;
     ```
- **Expected observation after edit:** build green; the `Clear()` call now sits outside
  the `if` in the else-branch of the raidwide handling.
- **Most likely failure:** the surrounding else-block braces get mismatched (CS1513).
  **Cause:** the edit moved `Clear()` without keeping the block structure. **Counter:**
  re-read `sed -n '365,385p'` and restore the exact shape above.

### MOVE 6 — H1: port structural rotation evals to xUnit

**Proof to capture first (read-only):**
```bash
ls WrathCombo.Tests/
grep -n "TargetFramework\|ProjectReference" WrathCombo.Tests/WrathCombo.Tests.csproj
```
- **Expected:** three test files + csproj; csproj targets net10.0. **Note whether it
  has a `ProjectReference` to WrathCombo** — expected NOT (tests cover pure services by
  file inclusion or source-copy; read the csproj to see the idiom). The new test reads
  repo files as text, so it needs no project reference either way.
- **The fix:** create `WrathCombo.Tests/RotationStructureTests.cs`:
  ```csharp
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Runtime.CompilerServices;
  using System.Text.RegularExpressions;
  using Xunit;

  namespace WrathCombo.Tests;

  /// <summary>
  ///     Cross-platform port of scripts/rotation-evals.ps1's structural checks:
  ///     preset enum parses, every job has presets, preset IDs are unique.
  ///     Reads source files as text from the repo root located via CallerFilePath.
  /// </summary>
  public class RotationStructureTests
  {
      private static string RepoRoot([CallerFilePath] string thisFile = "")
          => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

      private static readonly Regex PresetEntry =
          new(@"^\s+(\w+) = (\d+),?", RegexOptions.Multiline);

      private static MatchCollection PresetMatches()
      {
          var presetFile = Path.Combine(RepoRoot(), "WrathCombo", "Combos", "CustomComboPreset.cs");
          Assert.True(File.Exists(presetFile), $"Preset file missing: {presetFile}");
          return PresetEntry.Matches(File.ReadAllText(presetFile));
      }

      [Fact]
      public void PresetEnum_HasEntries()
          => Assert.True(PresetMatches().Count > 100,
              "CustomComboPreset.cs parsed to almost no entries - regex or file drifted");

      [Fact]
      public void PresetIds_AreUnique()
      {
          var dupes = PresetMatches().Select(m => m.Groups[2].Value)
              .GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
          Assert.True(dupes.Count == 0, $"Duplicate preset IDs: {string.Join(", ", dupes)}");
      }

      [Fact]
      public void EveryJobDir_HasPresets()
      {
          var combosDir = Path.Combine(RepoRoot(), "WrathCombo", "Combos", "PvE");
          var names = PresetMatches().Select(m => m.Groups[1].Value).ToList();
          var missing = new List<string>();
          foreach (var dir in Directory.GetDirectories(combosDir))
          {
              var job = Path.GetFileName(dir);
              if (!Regex.IsMatch(job, "^(ALL|[A-Z]{3})$")) continue;
              if (!names.Any(n => n.StartsWith(job + "_", StringComparison.Ordinal)))
                  missing.Add(job);
          }
          Assert.True(missing.Count == 0, $"Jobs with zero presets: {string.Join(", ", missing)}");
      }
  }
  ```
- **Expected observation:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj`
  → `Passed: 37, Failed: 0` (34 baseline + 3 new).
- **Most likely failure:** `EveryJobDir_HasPresets` fails listing `ALL`, `DOL`, `BLU`,
  `Content`, or `Enums`. **Cause A:** genuinely preset-less utility dirs (the ps1 had
  the same blind spot; `Content`/`Enums` don't match the 3-letter regex, but `DOL`/`BLU`
  do and may legitimately have few presets under different prefixes). **Counter:** check
  `grep -c "DOL_\|BLU_" WrathCombo/Combos/CustomComboPreset.cs` — if a listed job truly
  has zero presets, add it to an explicit skip set in the test with a comment naming
  this plan: `var skip = new HashSet<string> { "ALL" /* shared preset prefix differs */ };`
  and exclude skip members from `missing`. Do not weaken the assertion for real jobs.

### MOVE 7 — H2: rotation stall watchdog

**Proof to capture first (read-only):**
```bash
grep -n "internal static void Run()" WrathCombo/AutoRotation/AutoRotationController.cs
grep -n "TimeLastActionUsed" WrathCombo/Data/ActionWatching.cs | head -3
grep -n "static bool ShouldSkipAutorotation" WrathCombo/AutoRotation/AutoRotationController.cs
```
- **Expected:** `Run()` at ~323; `ActionWatching.TimeLastActionUsed` is a `DateTime`
  updated on every successful action; a skip-gate method exists near ~176-187.
- **The fix:** add a private helper + one call. Place the call in `Run()` immediately
  after the `if (ShouldSkipAutorotation()) return;` line (~331-332):
  ```csharp
        MaybeWarnRotationStalled();
  ```
  and add the helper at the end of the class body (before the closing brace of
  `AutoRotationController`, alongside other private statics):
  ```csharp
    private const int StallWarnSeconds = 8;
    private static long _nextStallWarnAt;

    /// <summary>
    ///     ParseLord5 diagnostic: if auto-rotation is enabled, we are in combat with a
    ///     battle target, and nothing has been sent for StallWarnSeconds, say so once
    ///     per window - turns silent rotation stalls into log evidence.
    /// </summary>
    private static void MaybeWarnRotationStalled()
    {
        if (cfg?.Enabled != true || !CustomComboFunctions.InCombat() || !CustomComboFunctions.HasBattleTarget())
            return;

        var idle = (DateTime.Now - ActionWatching.TimeLastActionUsed).TotalSeconds;
        if (idle < StallWarnSeconds)
            return;

        var now = Environment.TickCount64;
        if (now < _nextStallWarnAt)
            return;
        _nextStallWarnAt = now + StallWarnSeconds * 1000;

        Svc.Log.Information(
            $"[ParseLord5][StallWatch] No action sent for {idle:F0}s with autorot on " +
            $"(job={CustomComboFunctions.LocalPlayer?.ClassJob.RowId}, " +
            $"weaveCount={ActionWatching.WeaveActions.Count}, " +
            $"animLock={CustomComboFunctions.AnimationLock:F2}, " +
            $"occupied={GenericHelpers.IsOccupied()})");
    }
  ```
- **Expected observation:** build green. In-game (M4) the line appears when autorot is
  deliberately blocked, and never during normal play.
- **Most likely failure:** CS0103 on `CustomComboFunctions.*`/`GenericHelpers`.
  **Cause:** the controller file accesses these via inheritance or different usings.
  **Counter:** `grep -n "InCombat()\|HasBattleTarget()" WrathCombo/AutoRotation/AutoRotationController.cs | head -3`
  and mirror the exact idiom already used in that file (it references both in existing
  code); drop the `occupied=` field if `GenericHelpers` isn't already imported.
- **TRIGGER — `TimeLastActionUsed` is private or absent:** check
  `grep -n "TimeLastActionUsed" WrathCombo/Data/ActionWatching.cs`; if private, widen to
  `internal` (one-word change, note in report); if absent, ABORT-SCOPE for this move
  only (skip MOVE 7, continue plan).

### MOVE 8 — H3: per-GCD guard on restored fallback mitigation ladders

**`RECON NEEDED [R1]` (settles this move):**
```bash
grep -n "UsedGnbMitigationThisGcd" WrathCombo/Combos/PvE/GNB/GNB_SmartMitigation.cs WrathCombo/Combos/PvE/GNB/GNB_Helper.cs
grep -n "PerGcdActionCaps\|UsedWarMitigationThisGcd\|AnyUsed" WrathCombo/Combos/PvE/WAR/WAR_SmartMitigation.cs | head -5
grep -n "TryGetNonBossMitigation\|TryGetBossMitigation" WrathCombo/Combos/PvE/GNB/GNB_Helper.cs WrathCombo/Combos/PvE/WAR/WAR_Helper.cs
```
- **Routing:** the smart paths guard per-GCD via `UsedGnbMitigationThisGcd` /
  `PerGcdActionCaps` (GNB_SmartMitigation.cs:484-485), but the fallback ladders restored
  in `9be67b9a8` rely on their own `justMitted`/`JustUsed` lists.
  - If `UsedGnbMitigationThisGcd` appears **only** in GNB_SmartMitigation.cs (not in
    GNB_Helper.cs) → apply the GNB edit below.
  - If WAR_SmartMitigation.cs has an equivalent per-GCD helper (an `AnyUsed`/
    `PerGcdActionCaps.AnyUsed(IsWarMitigationAction)`-style member) not referenced from
    WAR_Helper.cs's fallback methods → apply the WAR edit below using that exact symbol.
  - If either fallback already references the per-GCD guard → skip that job's edit.
- **The GNB edit** — first line of *both* `TryGetNonBossMitigation` and
  `TryGetBossMitigation` in `GNB_Helper.cs`:
  ```csharp
        if (UsedGnbMitigationThisGcd)
            return false; // smart path already spent this GCD's mitigation slot
  ```
- **The WAR edit** — same shape at the top of WAR_Helper.cs's `TryGetNonBossMitigation`
  and `TryGetBossMitigation`, using the WAR-side symbol found in R1. If R1 finds **no**
  WAR per-GCD helper, add none (WAR's `justMitted` list already covers all smart-path
  actions except Equilibrium — a heal, not double-mit) and note in report.
- **Expected observation:** build green; behavior unchanged except at the boundary where
  smart-mit fired and the fallback would have fired again in the same weave window.
- **Most likely failure:** CS0103 (symbol not visible from the helper file). **Cause:**
  the guard property lives in the same partial class — it should resolve; if it doesn't,
  the partials differ. **Counter:** declare nothing new; instead inline the underlying
  call exactly as GNB_SmartMitigation.cs:484 defines it
  (`PerGcdActionCaps.AnyUsed(IsGnbMitigationAction)`).

### MOVE 9 — Full verification + deploy

Run V1-V4 from §4. Then Debug-deploy for in-game testing:
```bash
export PATH="$HOME/.dotnet:$PATH"
cd /home/kruillin/Projects/Projects/ParseLord5
dotnet build WrathCombo/WrathCombo.csproj -c Debug 2>&1 | tail -4
for f in ParseLord5.dll ParseLord5.json ParseLord5.pdb ParseLord5.deps.json; do cp ~/.xlcore/devPlugins/ParseLord5/$f ~/.xlcore/devPlugins/$f; done
```
- **Expected observation:** Debug build green; 4 files copied; both
  `~/.xlcore/devPlugins/ParseLord5.dll` and `~/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`
  carry today's timestamp.
- Commit only if the user asked for commits; otherwise leave the working tree for review
  (the three user files must remain untouched either way — verify with V4).

---

## 2. Fork map

| Trigger (observable) | Route |
|---|---|
| MOVE 0: extra modified files beyond BRD/DRK/RDM | ABORT-DRIFT unless they match this plan's own edits (partial prior run → resume after matching moves) |
| MOVE 0: baseline tests fail | ABORT-ENV |
| MOVE 0: FFXIVClientStructs build errors | refresh Dalamud hook DLLs, retry once, else ABORT-ENV |
| MOVE 1/2/3: proof shows fix already present | skip move, record "pre-applied (old plan ran)" |
| MOVE 3: CS0103 on `All` | fully qualify `WrathCombo.Combos.PvE.All.SingleTargetDPS` |
| MOVE 4: success path re-arms override *before* the shared clear call | put `ManualOverrideUntil = 0;` at the three failure/expiry sites instead of inside `ClearManualQueuedAction` |
| MOVE 6: job-coverage test fails on utility dirs | verify with grep; add explicit skip-set only for dirs with genuinely zero presets |
| MOVE 7: `TimeLastActionUsed` private | widen to `internal` |
| MOVE 7: `TimeLastActionUsed` absent | skip MOVE 7, continue |
| MOVE 8 [R1]: fallback already guarded | skip that job's edit |
| MOVE 8 [R1]: no WAR per-GCD helper exists | GNB edit only; note WAR relies on justMitted |

## 3. Abort conditions

- **ABORT-ENV** — baseline build or tests fail at MOVE 0 for reasons unrelated to this
  plan. Capture: full build/test tail, `git log --oneline -3`, hook-DLL dir listing.
  Hand back: "environment broken before any edit; fix build first."
- **ABORT-DRIFT** — ≥2 proof-gates show code shapes that match neither the plan's
  premise nor its fix (theatre changed since 2026-07-06, e.g. another upstream merge).
  Capture: the failing sed/grep outputs. Hand back: "re-wargame against current tree."
- **ABORT-SCOPE** — any move turns out to require edits outside these files:
  `ConfigurationHelper.cs`, `ActionWatching.cs`, `AutoRotationController.cs`,
  `GNB_Helper.cs`, `WAR_Helper.cs`, `WrathCombo.Tests/RotationStructureTests.cs` (new).
  Capture: why. Hand back the finding unfixed with its evidence.
- **ABORT-USERFILES** — any step would modify `BRD.cs`, `DRK.cs`, or `RDM.cs`
  (uncommitted user work). Never proceed; report which move collided and why.

## 4. Verification runs

- **V1 — Release build:** `export PATH="$HOME/.dotnet:$PATH" && dotnet build WrathCombo/WrathCombo.csproj -c Release 2>&1 | tail -4`
  **PASS:** `0 Error(s)` and `3 Warning(s)` (exactly the three baseline warnings — a
  4th warning means an edit introduced one; find and fix it).
- **V2 — Tests:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj 2>&1 | tail -3`
  **PASS:** `Failed: 0`, `Passed: 37` (34 baseline + 3 from MOVE 6; if MOVE 6 added a
  skip-set fork, still 37 passed, 0 failed).
- **V3 — Fix presence:**
  ```bash
  grep -c "_isSaving = false" WrathCombo/Core/ConfigurationHelper.cs        # PASS: 4
  grep -c "originalSent" WrathCombo/Data/ActionWatching.cs                  # PASS: 3
  grep -c "SingleTargetDPS" WrathCombo/AutoRotation/AutoRotationController.cs  # PASS: >=1
  grep -c "ManualOverrideUntil = 0" WrathCombo/AutoRotation/AutoRotationController.cs  # PASS: >=1
  grep -n "BlacklistedRaidwides.Clear" WrathCombo/AutoRotation/AutoRotationController.cs  # PASS: outside the counter if-block
  grep -c "StallWatch" WrathCombo/AutoRotation/AutoRotationController.cs    # PASS: 1 (0 if MOVE 7 was skipped via trigger)
  ```
- **V4 — Scope check:** `git status --short` — **PASS:** modified set ⊆ {the six §3
  ABORT-SCOPE files} ∪ {BRD.cs, DRK.cs, RDM.cs (pre-existing, byte-identical to
  MOVE 0 state — verify with `git diff --stat` line counts unchanged: BRD +6, DRK +3,
  RDM +3)}.

## 5. MANUAL VERIFY (user, in-game)

- **M1 — Config wedge:** enable Debug tab's debug-config mode, toggle it off, change any
  setting, `/xlplugins` → reload ParseLord5, confirm the setting persisted.
  Rollback if broken: restore `ParseLord5.json` from backup (Dalamud keeps `.bak`).
- **M2 — Custom-action press:** with autorot on and a custom one-button on a hotbar,
  spam it mid-GCD in combat: rotation must not stutter/stall afterward, and no
  "custom action does nothing" toast storm.
- **M3 — SGE raidwide:** in a duty with raidwides, confirm Eukrasian Prognosis fires on
  the second and later raidwides, not just the first.
- **M4 — StallWatch:** with autorot on, stand in combat range with autorot Paused-like
  blockage (e.g. mounted near a dummy pull or use `/wrath auto` toggle off/on): a single
  `[ParseLord5][StallWatch]` Information line appears in `/xllog` after ~8s, at most
  once per 8s; none during normal play.
  Rollback for any regression: `git checkout -- <file>` per move, rebuild, redeploy.

## 6. Report skeleton (executor fills)

```
## Fixed
- T1..T5, H1..H3: applied / pre-applied / skipped(trigger) — one line each, with V3 grep proof
## Unfixed evidence-backed findings
- (anything ABORT-SCOPEd, plus the §0 deferred ledger untouched)
## Verification
- V1: <build tail>  V2: <test tail>  V3: <grep outputs>  V4: <status output>
## Manual checks pending
- M1-M4 with expected outcomes
## Residual risk
- (from §7, plus anything new observed)
```

## 7. Residual risk noted at plan time

- **The process root-cause remains:** experiment promotions that delete baseline code
  paths shipped three separate regressions before this plan. The regression net (H1/H2)
  detects structure and stalls, not rotation-priority correctness. Recommended rule (not
  a code move): every promotion commit must keep a fallback path or add a test proving
  the promoted path covers the deleted one's cases — enforce via `dotnet test` in the
  pre-commit routine now that it runs cross-platform.
- The SmartMitigation zone audit died mid-run (session limit); only its test-baseline
  duty was re-run by the planner. `CombatEventBuffer`/`CombatTelemetryService` growth
  and thread-safety remain **unaudited** — flagged for a future wargame.
- The deferred ledger (§0) is real debt: IPC collision and upstream kill-switch will
  bite if a real WrathCombo is ever co-installed or upstream flips their status file.
- `rotation-evals.ps1` stays pwsh-only; MOVE 6 duplicates (not replaces) its checks.
  If the ps1 gains fixtures later, the xUnit port must mirror them or drift silently.
- Fixes T3/T4 alter manual-input handling under autorot — the exact area the user
  touches daily. M2 is the guard; if M2 regresses, revert MOVE 3/4 first.

---

## Execution record — 2026-07-06 (filled report)

### Fixed
- **T1 config wedge — pre-applied** (external uncommitted work, identical to plan shape; `_isSaving = false` count: 5 = 3 baseline + 2 debug-branch resets). Kept as-is.
- **T2 double-send — pre-applied** (external work, `originalCalled` flag + nested-try re-enable + guarded re-send; semantically identical to plan shape). Kept as-is.
- **T3 custom-ID exclusion — applied by planner** (`actionId < All.SingleTargetDPS` clause added to `IsManualActionOverrideCandidate`).
- **T4 suppression-window release — applied by planner as complement** to external `TryResolveManualQueuedTarget` fallback-target work (which was judged better than the plan's minimal shape and kept): `ManualOverrideUntil = 0` added to `ClearManualQueuedAction()`; success path re-arm order verified (re-arms after clear).
- **T5 Eukrasia blacklist — applied by Sonnet agent A** (Contains guard + `Clear()` moved outside counter guard, now controller line ~443).
- **H1 xUnit structural evals — applied by Sonnet agent B** (`WrathCombo.Tests/RotationStructureTests.cs`, 3 tests, no skip-set needed — all 24 job dirs have presets).
- **H2 StallWatch — applied by Sonnet agent A** (no idiom adaptation needed; `TimeLastActionUsed` already public; call sits after `ShouldSkipAutorotation()` so Paused/mounted/occupied never warn).
- **H3 per-GCD fallback guards — applied by Sonnet agent C** (R1 found `UsedWarMitigationThisGcd` DOES exist at WAR_SmartMitigation.cs:641; all four guards applied: GNB_Helper.cs:123/260, WAR_Helper.cs:558/690). Approval inspection confirmed emergency invulns (WAR Holmgang, GNB Superbolide) run in the smart prepass BEFORE any per-GCD guard — no invuln delay regression.

### Unfixed evidence-backed findings
- Deferred ledger (§0) untouched by design: OnActionUsed IPC collision, upstream kill-switch/MOTD, Draw handler mismatch, dead ExperimentalMode toggle, DebugFile filter, party-cache eviction, WouldLikeToGroundTarget/UpdatingActions exception-safety, retarget NRE spam, CTS dispose.

### Verification
- V1 Release build: 0 Errors, 3 Warnings (baseline-identical) — confirmed post-edit by agents A and C on full builds, re-confirmed at inspection.
- V2 tests: **Passed 37 / Failed 0** (34 baseline + 3 new structural).
- V3 fix-presence greps: all present (counts: wedge 5, originalCalled 3, custom-ID 1, override-release 1, Contains 1, StallWatch 3, GNB guards 2, WAR guards 2; Clear() outside guard at ~443).
- V4 scope: modified set = plan files ∪ protected pre-existing work (BRD/DRK/RDM/WHM×2 untouched byte-for-byte by this execution).
- Deployed: Debug build 2026-07-06 13:27 to both `~/.xlcore/devPlugins/` locations.

### Manual checks pending (user, in-game — see §5)
- M1 debug-config settings persistence · M2 custom-action mid-GCD press · M3 SGE second-raidwide shield · M4 StallWatch fires when blocked, silent in normal play.

### Residual risk
- §7 unchanged, plus: external parallel work observed in-tree (WHM `IsSelectingAutorotAction` gating, manual-queue fallback targeting) is uncommitted and unreviewed by this plan beyond interaction safety; SmartMitigation telemetry buffers remain unaudited.

---

## Post-execution incident — 2026-07-06 evening: healer oGCD dump / offensive-oGCD starvation

**Field report:** SGE dumped all healing cooldowns at pull start and fired no offensive
oGCDs. Both symptoms are ONE mechanism: promotion commit `20f5f1ac4` removed
`!InBossEncounter()` gates from 9 healer setup-oGCD sites and reordered Soteria ahead of
Psyche (4 SGE DPS-combo sites). With `MaximumWeavesPerWindow = 1` (user config), healing
setup oGCDs won every weave slot until the entire kit was on cooldown — simultaneously
"heals dumped early" and "no offensive oGCDs." The zone audit HAD flagged the gate
removals but misclassified them as intentional widening ("opposite of silencing") —
the starvation interplay with 1-weave windows was missed. StallWatch correctly stayed
silent (actions WERE being sent — the wrong ones); it detects stalls, not priorities,
exactly as §7 warned.

**Fix applied (baseline restoration, all 10 sites):**
- SGE.cs: Psyche restored before Soteria in all 4 DPS combos (+ `InCombat()` added to
  Soteria, matching the heal-combo idiom); `!InBossEncounter()` restored on Physis,
  Kerachole, and the Holos/Panhaima block.
- SCH.cs: `!InBossEncounter()` restored on Sacred Soil, the ST fairy block, Expedient.
- WHM.cs: restored on Asylum (ST). Temperance intentionally left alone — external
  uncommitted work already rebuilt it as need-based `RaidwideTemperance()`.
- AST.cs: restored on the Celestial Opposition/Neutral Sect/Lady of Crown/Collective
  Unconscious block.

**Verification:** Release 0 errors / 3 baseline warnings; tests 37/37; deployed 22:27
to both devPlugins locations.

**Lesson recorded:** "experiment promoted = validated" is not evidence. Any promotion
that widens ability availability must be evaluated for weave-slot contention (what does
this new firing DISPLACE?), especially under 1-weave configs. Future audits: treat
availability-widening findings on shared weave windows as defects until play-proven.
