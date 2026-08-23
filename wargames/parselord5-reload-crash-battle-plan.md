---
tags:
  - type/plan
  - project/parselord5
  - status/done
type: plan
project: parselord5
status: done
aliases: []
---
# Battle Plan — ParseLord5 disable→re-enable crash fix

**Origin:** [parselord5-reload-crash-war-room.md](parselord5-reload-crash-war-room.md) (COA-1: surgical symmetric teardown + hook re-init).
**Status:** EXECUTED 2026-08-11 on `main` — all five moves applied, V1–V4 pass. One human step outstanding: **M1** (in-game disable→re-enable repro, §5).
**Never clobber:** sibling to `parselord5-stability-battle-plan.md` (different mission); do not merge or overwrite it.

Executable by a mid-tier model without follow-up questions. Every move: proof-before-edit → exact edit → expected observation → most-likely failure + cause → counter.

---

## 0 · Theatre map

- **Source root:** `C:\Users\kruil\orca\ParseLord5` (Windows). `[R0]` re-locate if moved: `git -C <root> rev-parse --show-toplevel`.
- **Branch:** `main` (one-branch rule, `AGENTS.md`). Do not create branches. *(The plan was authored against the retired `parselord5-wc-base` on a Linux checkout; both are gone.)*
- **Build (verified 2026-08-11):** `dotnet build WrathCombo/WrathCombo.csproj -c Release` → baseline **0 errors, 0 warnings** (the 3 CS0219/CS0169/CS0649 warnings this plan cited were removed in `007fabed1`).
- **Test:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` → baseline **55 passed**. Evals: `pwsh -NoProfile -File scripts/rotation-evals.ps1` → **14/14**.
- **Deploy:** the Release/Debug build writes straight to `%AppData%\XIVLauncher\devPlugins\ParseLord5\`; no copy step. `.\scripts\sync-dev-build.ps1` does pull + build + evals.
- **Forbidden:** anything outside the files named below. *(The "9 uncommitted fixes" and the 3 protected user files `WrathCombo/Combos/PvE/{BRD,DRK,RDM}.cs` were committed long ago; the constraint is now just scope discipline.)*

### Targets
| # | Defect | Site | Fix |
| --- | --- | --- | --- |
| F1 | `Draw` unsubscribe mismatch | WrathCombo.cs:231 vs 503 | `-= ws.Draw` |
| F2 | Leasing `Framework.Update` never removed | Leasing.cs:743 / Provider.cs:154-158 | add `-=` |
| F3 | Static hooks disposed-not-nulled, no re-init | ActionWatching.cs:82-93,195-206 | null after dispose (or re-create on Enable) |
| F4 | Missing `-=`: ErrorToast/OpenMainUi/LanguageChanged/OnStatusChanged | WrathCombo.cs:266,232,210; AutoRotationController.cs:100 | add matching `-=` |
| F5 | Leaked CTS / RunOnTick / StallWatch static | ActionWatching.cs:76,552; AutoRotationController.cs:192 | dispose + cancel + reset |

---

## 1 · Move sequence

### MOVE 0 — Establish baseline + sweep for ALL leak sites (read-only)
**Proof/recon (no edits):**
```bash
cd /home/kruillin/Projects/Projects/ParseLord5
git branch --show-current                 # expect parselord5-wc-base
git status --short                         # expect only the known uncommitted set; note it
export PATH="$HOME/.dotnet:$PATH"
dotnet build WrathCombo/WrathCombo.csproj -c Release 2>&1 | tail -3   # expect 0 errors, 3 warnings
# Sweep: every per-frame / host-event subscription must have a matching unsubscribe
grep -rn "UiBuilder.Draw +=\|Framework.Update +=\|\.Update +=\|OpenMainUi +=\|LanguageChanged +=\|ErrorToast +=\|OnStatusChanged +=" WrathCombo/ --include=*.cs
grep -rn "UiBuilder.Draw -=\|Framework.Update -=\|\.Update -=\|OpenMainUi -=\|LanguageChanged -=\|ErrorToast -=\|OnStatusChanged -=" WrathCombo/ --include=*.cs
```
- **Expected observation:** the `+=` list contains F1–F4's sites; the `-=` list is missing the F2/F4 counterparts and shows `Draw -= DrawUI` (the F1 mismatch). Build is at baseline.
- **Most likely failure:** the `+=` sweep surfaces a **new** subscriber not in the targets table. **Cause:** the codebase has more asymmetry than the five hand-found leaks. **Counter:** add it to the targets with the same pattern (matching `-=` in the owning Dispose) — do NOT skip it; this move exists to turn "the ones we found" into "all of them."
- **TRIGGER — build is not at baseline (≠0 err or ≠3 warn) before any edit:** the theatre already drifted → **ABORT-ENV**.
- **`RECON NEEDED [R1]`** — does `AutoRotationController` ctor subscribe `OnStatusChanged`? `grep -n "OnStatusChanged" WrathCombo/AutoRotation/AutoRotationController.cs`. If a `+=` exists → F4 includes `OnStatusChanged -= StatusChanged`; if only the `-=`/declaration exist → drop that F4 line (no-op, avoid a spurious unsubscribe).

### MOVE 1 — F1: unsubscribe the delegate that was actually subscribed
**Proof first:**
```bash
sed -n '231p;503p' WrathCombo/WrathCombo.cs
```
- **Expected observation:** :231 `...UiBuilder.Draw += ws.Draw;` and :503 `...UiBuilder.Draw -= DrawUI;` — a mismatch.
- **Edit:** at line 503 change `-= DrawUI` to `-= ws.Draw`. Leave `DrawUI` (:432) alone — if it's dead after this, that's out of scope.
- **Expected after:** build still 0 err/3 warn.
- **Most likely failure:** compile error "no overload matches." **Cause:** `ws.Draw` is a method group needing the same shape as the subscribe. **Counter:** mirror line 231 exactly (same `ws.Draw` reference).

### MOVE 2 — F2: unsubscribe Leasing's per-frame callback symmetrically
**Proof first:**
```bash
sed -n '743p' WrathCombo/Services/IPC/Leasing.cs
sed -n '154,158p' WrathCombo/Services/IPC/Provider.cs
grep -n "Dispose\|CheckIfLeaseePluginsUnloaded" WrathCombo/Services/IPC/Leasing.cs
```
- **Expected observation:** Leasing ctor `Svc.Framework.Update += CheckIfLeaseePluginsUnloaded`; `Provider.Dispose()` calls `SuspendLeases` but no `-=`.
- **Edit:** unsubscribe symmetric to the subscribe — preferably a `Leasing.Dispose()`/teardown that runs `Svc.Framework.Update -= CheckIfLeaseePluginsUnloaded`, invoked from `Provider.Dispose()` before/after `SuspendLeases`. If `Leasing` has no Dispose, add the `-=` directly in `Provider.Dispose()` referencing the same method.
- **Expected after:** build green.
- **Most likely failure:** `CheckIfLeaseePluginsUnloaded` is private/instance-scoped and unreachable from Provider. **Cause:** wrong teardown site. **Counter:** put the `-=` inside `Leasing` (same class as the ctor `+=`) and call that from Provider.Dispose — this is the symmetric site anyway.

### MOVE 3 — F3: make static hooks re-initializable
**Proof first:**
```bash
sed -n '82,93p;195,206p' WrathCombo/Data/ActionWatching.cs
```
- **Expected observation:** hooks initialized with `??=` in a **static ctor**; `Dispose()` calls `?.Dispose()` on each but never sets them to `null`.
- **Edit (primary — robust to ALC behavior):** move the hook creation out of the static ctor into `Enable()`, guarded so it re-creates when absent: `ReceiveActionEffectHook ??= Svc.Hook.HookFromAddress(...)` **inside `Enable()`**, for all 7 hooks. AND in `Dispose()`, after `?.Dispose()`, null each field. Together this means: whether Dalamud reloads the assembly context (fresh statics) or reuses it (stale disposed fields), the next `Enable()` sees null and re-creates valid hooks. **Ordering:** apply MOVE 5's cancel of pending work *before* the null, so no in-flight detour references a nulled field — same `Dispose()`, cancel then null.
  - *Why not null-only:* nulling alone relies on the static ctor re-running to rebuild via `??=` — it won't in a reused load context (static ctors run once per context). The Enable-guarded create is correct in **both** the reload and reuse cases; the null is its companion so the guard actually fires. This is why re-create is primary, not a fallback.
- **Expected after:** build green; on re-enable, `/xllog` shows no `HookVerificationException`/`AccessViolationException` attributed to `ParseLord5` (the war room's original F6 citation — log lines actually belonging to PortraitFixer/ActionStacksEX — was retracted 2026-07-07; this is a fresh check, not a before/after diff against that retracted evidence).
- **Most likely failure:** a combo reads a hook field expecting the static-ctor timing and NREs because create now happens in `Enable()`. **Cause:** moved initialization point. **Counter:** `Enable()` already runs in the plugin ctor (WrathCombo.cs:203) before combats execute, so timing holds; if a static read predates Enable, keep a `??=` create at that read site too. **TRIGGER — R1/A2 evidence that the ALC fully unloads and re-creates the type on re-enable:** the null-in-Dispose becomes redundant (fresh statics) but harmless — keep the Enable-guarded create regardless.

### MOVE 4 — F4: add the missing unsubscribes
**Proof first:**
```bash
sed -n '210p;232p;266p' WrathCombo/WrathCombo.cs
sed -n '100p;128,132p' WrathCombo/AutoRotation/AutoRotationController.cs
```
- **Expected observation:** subs at :210 (LanguageChanged), :232 (OpenMainUi), :266 (ErrorToast) with no `-=` in Dispose; AutoRotationController subscribes `OnStatusChanged` at :100, Dispose (:128-132) unsubscribes only `OnPartyCombatChanged`.
- **Edit:** in `WrathCombo.Dispose()` (near the other `-=` at :499-517) add:
  `Svc.PluginInterface.LanguageChanged -= Text.OnLanguageChanged;`
  `Svc.PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;`
  `Svc.Toasts.ErrorToast -= OnErrorToast;`
  In `AutoRotationController.Dispose()` add `OnStatusChanged -= StatusChanged;` **only if R1 confirmed the subscribe.**
- **Expected after:** build green.
- **Most likely failure:** method name mismatch (e.g. the ctor used a lambda, not `OnOpenMainUi`). **Cause:** anonymous handler can't be unsubscribed by name. **Counter:** if the subscribe used a lambda, refactor to a named method first, then subscribe/unsubscribe the name (symmetry requires a stable delegate).

### MOVE 5 — F5: dispose CTS, cancel RunOnTick, reset StallWatch static
**Proof first:**
```bash
sed -n '74,77p;195,206p;550,553p' WrathCombo/Data/ActionWatching.cs
grep -n "RunOnTick" WrathCombo/WrathCombo.cs
sed -n '190,193p' WrathCombo/AutoRotation/AutoRotationController.cs
```
- **Expected observation:** static `CancellationTokenSource source` recreated at ~:552 with no `Dispose()`; RunOnTick calls not tracked; `_nextStallWarnAt` static.
- **Edit:** in `ActionWatching.Dispose()` call `source?.Cancel(); source?.Dispose();` (and null it, per MOVE 3 ordering). Track constructor RunOnTick tasks in a cancellable token and cancel on Dispose, OR pass the existing `source.Token` so they cancel with it. Reset `AutoRotationController._nextStallWarnAt = 0` in its Dispose.
- **Expected after:** build green; 37/37 tests.
- **Most likely failure:** a test that touches `ActionWatching` statics fails. **Cause:** the reset changed observable static state a test asserts. **Counter:** confirm the reset only runs in Dispose (not at read time); if a test constructs/disposes, update the test expectation to the clean state (do not weaken the fix).

---

## 2 · Fork map (quick reference)
- MOVE 0 sweep finds a 5th subscriber → add as F6, same pattern.
- MOVE 0 build off baseline → **ABORT-ENV**.
- R1 no `OnStatusChanged +=` → drop that F4 line.
- MOVE 3 re-enable still errors → move `??=` into `Enable()` (guarded re-create).
- A2 ALC doesn't unload → Enable-guarded re-create is mandatory, not optional.

## 3 · Abort conditions
- **ABORT-ENV** — build not at baseline before edits, or dotnet/SDK missing. Capture: `dotnet --version`, build tail. Hand back: environment differs from theatre map; do not edit.
- **ABORT-SCOPE** — the A1 repro AV names a subsystem outside the targets (not draw/Update/hooks). Capture: the `/xllog` AV stack. Hand back: root cause is broader than teardown; re-convene the war room with the stack.
- **ABORT-REGRESS** — any edit drops a pending-fix hunk or touches a protected user file. Capture: `git diff` of the offending file. Hand back: revert that hunk; the 9 fixes and BRD/DRK/RDM are untouchable.

## 4 · Verification runs

> Executed 2026-08-11 on Windows at `aafddadd5` + this fix. Before execution the
> lines below stated **PASS** as *expected* results while the orders were still
> unexecuted; V3 in particular asserted a symmetry that did not hold. They now
> record measured outcomes.

- **V1 build:** `dotnet build WrathCombo/WrathCombo.csproj -c Release` — **PASS: 0 errors, 0 warnings** (exit 0).
- **V2 tests:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release` — **PASS: Failed 0, Passed 55** (exit 0).
- **V3 symmetry:** re-ran MOVE 0's sweep — **PASS: every tracked `+=` now has a matching `-=`.** Pre-fix this **FAILED**: `Draw` was subscribed as `ws.Draw` but unsubscribed as `DrawUI` (a delegate never subscribed, so a silent no-op), and `Leasing`'s `Framework.Update` had no `-=` anywhere in the repo. Now enforced by the `teardown-event-subscription-symmetry` eval fixture (`scripts/rotation-evals.ps1`), which was mutation-tested: reintroducing `-= DrawUI` makes it report `ws.Draw subscribed but never unsubscribed` and exit 1.
- **V4 diff hygiene:** `git diff --stat` — **PASS: only `WrathCombo.cs`, `ActionWatching.cs`, `Leasing.cs`, `Provider.cs`, `AutoRotationController.cs` changed** (plus the eval fixture and these docs); no BRD/DRK/RDM; no pending-fix hunks disturbed.
- **V5 evals:** `pwsh -NoProfile -File scripts/rotation-evals.ps1` — **PASS: passed=14 failed=0** (exit 0).

## 5 · MANUAL VERIFY (user only)

> Tracked as [#2](https://github.com/kruillin-lab/ParseLord5/issues/2) — this is the only thing standing between the fix and a closed mission.
- **M1 repro:** deploy Debug build; in-game open `/xllog`; disable ParseLord5 in the plugin installer, wait ~3s, re-enable; repeat **×5** (idle, and once in combat). **Expected:** no game crash, and no `HookVerificationException @0x…` / signature `KeyNotFoundException` / `AccessViolationException` in the log on any cycle. **Rollback:** if it crashes, redeploy the pre-fix DLL (git stash the fix, rebuild Debug, redeploy) and capture the `/xllog` tail for ABORT-SCOPE.

## 6 · Report skeleton (executor fills on completion)
- **Moves applied:** F1 (Draw unsubscribed with the delegate actually added, `ws.Draw`; dead `DrawUI` method removed), F2 (`Leasing.Dispose()` added — the class had no `Dispose` at all — and called from `Provider.Dispose()`), F3 (7 hook fields de-`readonly`'d, creation moved from the static ctor into `Enable()` behind `??=`, fields nulled in `Dispose()` so a reused load context rebuilds them), F4 (`OpenMainUi`, `LanguageChanged`, `ErrorToast` unsubscribes added; `OnStatusChanged -= StatusChanged` added to `AutoRotationController.Dispose`), F5 (`source` CTS cancelled + disposed + replaced *before* hooks are torn down).
- **MOVE 0 sweep outcome:** no new per-frame subscriber beyond the known set. Two extras found and fixed that the plan had not named: `OnCastInterrupted` was subscribed in ActionWatching's *static ctor* but unsubscribed in `Dispose`, so after one disable→re-enable it was silently gone forever — now paired across `Enable`/`Disable`; and `Disable()` called `ReceiveActionEffectHook.Disable()` without `?.` while the other six used `?.`.
- **Second sweep (independent re-audit, 2026-08-11) — F6, deferred-work and static-state leaks the delegate sweep cannot see:** three more teardown defects found and fixed after the five moves landed. (a) `Provider.ActionToken` was a `static readonly CancellationTokenSource` that `Dispose()` cancelled; a reused load context carries the cancelled token into the next `Init()`, so `BuildCaches` early-returns forever and **IPC never becomes ready after a re-enable** — now a per-instance `_actionToken`, disposed with the provider, and passed to both `RunOnTick` cache-build retries so queued builds die with the plugin. (b) The plugin ctor queued `ActionRetargeting.ClearOldRetargets` on a **60-second** `RunOnTick` with no token — disabling within that minute fires it against a disposed `ActionRetargeting`; now gated on a new `WrathCombo._lifetime` CTS cancelled at the top of `Dispose()` (the `#if DEBUG` open-to-current-job tick too). (c) `AutoRotationController`'s remaining mutable statics were left set at teardown alongside `_nextStallWarnAt`; the dangerous one is `AutorotHealTarget`, which holds an `IGameObject` from the previous session that the next enable can read before the first target scan replaces it — all of them (`Paused`, `UnpauseSeconds`, `AutorotHealTarget`, `AutorotRaidwiding`, `AutorotRaidwides`, `TankbusterHandled`, `WouldLikeToGroundTarget`, `IsIssuingAutorotAction`, `IsSelectingAutorotAction`, `IsIssuingManualQueuedAction`, `HealThrottle`, `TimeToHeal`, `_lockedST`, `_lockedAoE`) now reset in `Dispose()`. Re-verified after: build 0/0, 55 tests, 14 evals.
- **R1 outcome:** `OnStatusChanged +=` **does** exist (`AutoRotationController.cs:100`, a *static* event holding an instance handler), so the F4 line was **kept**, not dropped per §2.
- **V1–V5 results:** see §4 — all pass, 0 warnings, 55 tests, 14 evals.
- **M1 result:** **OUTSTANDING — operator only.** Nothing in this fix is confirmed in-game until §5 runs.
- **Files changed:** `WrathCombo/WrathCombo.cs`, `WrathCombo/Data/ActionWatching.cs`, `WrathCombo/Services/IPC/Leasing.cs`, `WrathCombo/Services/IPC/Provider.cs`, `WrathCombo/AutoRotation/AutoRotationController.cs`, `scripts/rotation-evals.ps1`.
- **Aborts:** none triggered.

## 7 · Residual risk at plan time
- A fourth/fifth per-frame subscriber not caught by the MOVE 0 greps (regex-bounded) — logged, not eliminated.
- The static→instance refactor (COA-2) is deferred; this plan re-initializes hooks but leaves them static.
- A1/A2 confirmation depends on M1 — until the user runs it, the first-faulting handler is inferred, not proven. The fix stands regardless (every target is a real teardown defect).
