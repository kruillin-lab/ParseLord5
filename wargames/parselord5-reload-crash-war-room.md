---
tags:
  - type/report
  - project/parselord5
  - status/active
type: report
project: parselord5
status: active
aliases: []
---
# War Room — ParseLord5 disable→re-enable crash

**Status:** DECIDED
**Date opened:** 2026-07-07 · **Advisor:** Claude Fable/Opus (war-room skill) · **Battle plan:** [parselord5-reload-crash-battle-plan.md](parselord5-reload-crash-battle-plan.md)

Convened via the `war-room` skill. Recon fanned out to a parallel explorer wave (lifecycle, crash logs, prior art) plus the skill's own eval-run recon; ~10 independent read-only agents. Findings converged, so confidence is high and cited per line.

---

## 1 · Frame

- **Current state:** Disabling ParseLord5 in the Dalamud plugin installer and re-enabling it in the same game session crashes FFXIV. The running build is the Debug DLL deployed 2026-07-06 23:27 — effectively the current uncommitted working tree.
- **Desired end state:** A cited root cause, a decided+wargamed fix, and a plugin that survives repeated disable→re-enable cycles without crashing.
- **Five Ws:** who — anyone toggling the plugin live (and every dev save, since `AutomaticReloading=true`); what — whole-game crash (native AV); where — Dalamud plugin lifecycle, ParseLord5 (WrathCombo fork); when — on/after re-enable in the same process; why now — the plugin is under active daily iteration and each reload risks a crash.
- **In scope:** teardown/re-init symmetry in `WrathCombo.cs` + `ActionWatching.cs` + IPC `Leasing`/`Provider`. **Out of scope:** the SCH/AST heal-leak (separate mission), committing the 9 pending fixes, upstream PR.
- **Constraints (non-negotiable):** plugin-side only; must not regress the 9 uncommitted fixes; must not touch the 3 protected user files (BRD/DRK/RDM); one work branch (`AGENTS.md:32`); repo stays read-only until the user accepts orders.
- **Commander's intent:** *the user can toggle ParseLord5 off and on repeatedly without a crash, and this dossier proves — by cited code — why it crashed before.*

## 2 · Recon

### Facts
| # | Fact | Evidence |
| --- | --- | --- |
| F1 | `UiBuilder.Draw += ws.Draw` subscribed in ctor but Dispose unsubscribes a *different* delegate, `DrawUI` → `ws.Draw` survives unload as a per-frame callback | WrathCombo.cs:231 (sub) vs :503 (`-= DrawUI`); `DrawUI` defined :432, never subscribed. Found by 3 independent agents |
| F2 | `Svc.Framework.Update += Leasing.CheckIfLeaseePluginsUnloaded` subscribed in ctor, never unsubscribed; `Provider.Dispose()` only calls `SuspendLeases` → second per-frame callback survives | Leasing.cs:743 (sub); Provider.cs:154-158 (Dispose, no `-=`) |
| F3 | `ActionWatching` static hooks created once via `??=`, disposed in `Dispose()` but fields never nulled; static ctor won't re-run on re-enable → `Enable()` re-enables disposed hooks | Data/ActionWatching.cs:82-93 (`??=` init), :195-206 (Dispose, no null-reset), Enable :182-192; ctor calls `ActionWatching.Enable()` at WrathCombo.cs:203 |
| F4 | Missing `-=` in Dispose for: `Svc.Toasts.ErrorToast` (:266), `UiBuilder.OpenMainUi` (:232), `PluginInterface.LanguageChanged` (:210); static `OnStatusChanged` (AutoRotationController.cs:100) unsubscribes only `OnPartyCombatChanged` (:128-132) | WrathCombo.cs lines cited; AutoRotationController.cs:100 vs 128-132 |
| F5 | Leaked `CancellationTokenSource` in `ActionWatching` recreated without disposing; RunOnTick callbacks not cancelled; new `StallWatch._nextStallWarnAt` static never reset | ActionWatching.cs:76,552; WrathCombo.cs RunOnTick :161,271-281; AutoRotationController.cs:192 |
| ~~F6~~ | **RETRACTED 2026-07-07** — originally claimed as "hook re-registration failures on ParseLord5 load." Direct re-inspection of the cited lines shows both exceptions are misattributed: `dalamud.log:4219`'s `HookVerificationException @0x6FFFFAA71370` names `Hook creation caller: PortraitFixer`; `:4265`'s signature-scan `KeyNotFoundException` is explicitly logged as `[ActionStacksEX] Failed to find signature ...` from `Hypostasis/AsmPatch.cs`. **Neither implicates ParseLord5.** This was an error in the original recon synthesis (Explorer B's report characterized nearby log noise as ParseLord5-relevant; I did not verify the caller attribution before citing it as corroboration). Caught by an independently-run baseline agent in the skill's own eval loop, not by this dossier's Phase 7 self-review — see §7. | ~/.xlcore/logs/dalamud.log:4219,4265 (verified 2026-07-07, see correction) |
| F7 | The leaks are upstream WrathCombo behavior, not fork-specific: ParseLord5's only source delta is a `/pl5` command alias | SOURCE_DIFFS.txt; Commands.cs:34,37. Explains why upstream has no issue filed (users restart to update) |
| F8 | Config asymmetry `LoadSync:false` + `CanUnloadAsync:false` (loads async, unloads sync) widens the toggle race window | ParseLord5.json:22-23; `AutomaticReloading=true` in dalamudConfig.json |
| F9 | Toolchain (verified): dotnet 10.0.301 at `~/.dotnet/dotnet`; build `export PATH="$HOME/.dotnet:$PATH" && dotnet build WrathCombo/WrathCombo.csproj -c Release` → baseline 0 err / 3 warn; tests 34-37 pass; deploy `~/.xlcore/devPlugins/ParseLord5/` | stability battle plan §toolchain; F-C6 recon |

### Assumptions
| # | Assumption | Settling check (read-only) | Routing |
| --- | --- | --- | --- |
| A1 | On disable→re-enable, one of the surviving per-frame delegates (F1/F2) or a disposed hook (F3) is what actually faults | MANUAL VERIFY: `/xllog` at VRB, toggle off→on, watch for HookVerificationException + AV stack | if AV names ws.Draw/Leasing/ActionWatching → confirmed; if a different subsystem → widen scope (see ABORT-SCOPE) |
| A2 | The surviving subscriptions prevent the collectible ALC from unloading, producing a zombie instance that coexists with the new one | Not settleable read-only; inferred from F1/F2 rooted in Dalamud host objects. **Weakened by the F6 retraction** — the specific hook-verification failures once cited as corroboration belong to other plugins, so this is now pure code-level inference with no supporting log signal | fix does not depend on this being the exact GC mechanism; symmetric teardown dissolves it either way — but A1's MANUAL VERIFY repro carries correspondingly more weight now, since it's the only remaining path to observing the actual failure mode |

### RECON NEEDED
- [R1] Confirm `AutoRotationController` ctor actually subscribes `OnStatusChanged` (F4 assumes sub/unsub asymmetry). Check: `grep -n "OnStatusChanged" WrathCombo/AutoRotation/AutoRotationController.cs`. If subscribed → add `-=`; if never subscribed → drop that item, no-op.

### The captured-crash caveat (DOX honesty)
No on-disk crash log captured the actual ParseLord5 disable→re-enable event — the three appcrash dumps on 2026-07-06 belong to **other** plugins (winevulkan/GPU, LMeter, Umbra), not ParseLord5. So the root cause is **proven as a set of code defects** (F1–F5, confirmed by multiple independent agents) and **consistent with** the logged hook-verification failures (F6), but the specific first-faulting handler is confirmed only by the A1 repro. The fix is robust to which one faults first — every survivor is a genuine teardown bug.

## 3 · Criteria *(locked before COAs)*

**Screening (pass/fail):** plugin-side only · addresses the real reload mechanism (no symptom masking) · does not regress the 9 uncommitted fixes · covers BOTH teardown (unsubscribe/dispose) AND re-init (static hooks).

**Comparison (weighted):**
| Criterion | Weight | Why it matters here |
| --- | --- | --- |
| Confidence in fix | 3 | Each move must map to a cited defect, not a guess |
| Blast radius / regression risk | 3 | Landing right after 9 unverified fixes; a wide change endangers them |
| Completeness vs the whole chain | 3 | Half-fixes leave a live crash and a memory leak |
| Time-to-fix | 2 | Plugin is under daily iteration; the crash blocks the dev loop now |
| Reversibility | 2 | Must be easy to back out if it interacts with pending work |
| Durability vs future leaks | 2 | Same asymmetry class will recur as code grows |

## 4 · Courses of Action

### COA-0 — Baseline: change the habit, not the code
- **Purpose:** stop toggling live; fully relaunch the game to load a new build.
- **Contract:** touches nothing.
- **Verification:** no crash because the trigger is never pulled.
- **Screened: KILLED** — fails "addresses the real mechanism." Every `AutomaticReloading=true` file-save still re-triggers it during dev, and any user who toggles still crashes. Kept as the comparison floor.

### COA-1 — Surgical symmetric teardown + hook re-init *(selected)*
- **Purpose:** make `Dispose()`/`Disable()` exactly reverse construction, and make the static hooks re-initializable. Severs every cited link directly.
- **Contract:** edits `WrathCombo.cs` Dispose, `ActionWatching.cs` (Dispose + static-init), `Services/IPC/Leasing.cs`/`Provider.cs` Dispose. Must not touch protected user files or the 9 fixes' logic.
- **Work guidance:** (1) F1 fix `Draw -= ws.Draw`; (2) F2 add `Framework.Update -= CheckIfLeaseePluginsUnloaded`; (3) F3 null the static hook fields after Dispose so `??=` re-inits (or re-create if disposed); (4) F4 add the missing `-=` (ErrorToast/OpenMainUi/LanguageChanged/OnStatusChanged pending R1); (5) F5 dispose the CTS, cancel RunOnTick, reset StallWatch static.
- **Verification:** build 0 err/≤3 warn; 37/37 tests; MANUAL VERIFY toggle ×5 with `/xllog` — no HookVerificationException, no AV.

### COA-2 — Structural: disposal registry + de-static hooks *(runner-up)*
- **Purpose:** kill the asymmetry *class* — `CompositeDisposable` for every subscription, move `ActionWatching` hooks to instance lifetime.
- **Contract:** broad edits across the static→instance boundary many combos depend on.
- **Verification:** as COA-1 + full regression.
- **Screened: survives**, but heavy.

### COA-3 — Defensive: idempotent Enable/Disable + null-guarded survivors
- **Purpose:** make stale fires harmless rather than guaranteeing teardown.
- **Screened: KILLED** — fails "completeness": it no-ops the managed fault but leaves the ALC zombie-leaking and the native double-hook (F6) still throwing. Symptom mask + growing memory each cycle.

## 5 · Wargame

### COA-1 fought (sketch depth)
| Move | Expected observation | Most-likely failure → cause | Counter-move |
| --- | --- | --- | --- |
| Fix F1 `Draw -= ws.Draw` | build green; toggle no longer AVs during draw | still crashes → a *second* per-frame leak (F2) also fires | land F1+F2 together, not singly |
| Fix F2 Leasing unsub | Leasing callback stops after disable | `Provider.Dispose` isn't the right teardown site → leak persists | unsubscribe in `Leasing` itself, symmetric to its ctor |
| Fix F3 null+reinit hooks | re-enable re-hooks cleanly; no `HookVerificationException`/`AccessViolation` attributed to ParseLord5 in `/xllog` (not "the F6 errors disappear" — those were never ParseLord5's; this is a fresh observation, not a before/after diff) | nulling races a pending detour still executing → AV | cancel/await ActionWatching work (F5) *before* nulling |
| Fix F4/F5 remaining unsubs | clean teardown, no leaked CTS | R1 shows OnStatusChanged never subscribed → spurious `-=` | gate that one line on the R1 grep |

**Red-team verdict:** the sharpest objection is *"you fixed the three you found; a fourth per-frame `Framework.Update`/`Draw` subscriber elsewhere still crashes."* Counter: the battle plan includes a grep sweep for **all** `Framework.Update +=` / `UiBuilder.Draw +=` / `.Update +=` sites and checks each for a matching `-=` — turning "the ones we found" into "all of them." That sweep is move 1, before edits.
**Second-order effects:** nulling+reinit of static hooks touches the same `ActionWatching` file as the uncommitted SendActionDetour/StallWatch work → must diff-check those hunks aren't disturbed (reversibility criterion).

### Decision matrix
| Criterion (weight) | COA-0 | COA-1 | COA-2 | COA-3 |
| --- | --- | --- | --- | --- |
| Confidence in fix (3) | 1 | 3 | 3 | 2 |
| Blast radius (3) | 3 | 3 | 1 | 3 |
| Completeness (3) | 0 | 3 | 3 | 1 |
| Time-to-fix (2) | 3 | 3 | 1 | 3 |
| Reversibility (2) | 3 | 3 | 1 | 3 |
| Durability (2) | 0 | 2 | 3 | 1 |
| **Weighted total** | **21** | **48** | **34** | **32** |

## 6 · Decision record

- **Selected:** **COA-1 — surgical symmetric teardown + hook re-init** (48). It scores max on the three weight-3 criteria that matter most here (confidence, blast radius, completeness): every move maps to a cited defect, it stays surgical right after 9 unverified fixes, and it closes the whole chain rather than masking it.
- **Why the losers lost:** COA-2 (34) is the *right* long-term shape but de-statifying `ActionWatching` is a wide change landing on top of unverified work — its blast-radius and reversibility scores sink it *for now* (revisit once the pending fixes commit). COA-3 (32) masks the managed fault but leaves the ALC leaking (F1/F2, still independently code-proven) and the disposed-hook reuse hazard (F3, still independently code-proven) live — fails completeness. This conclusion does not depend on the retracted F6. COA-0 (21) leaves a live crash on every dev save.
- **Residual risk accepted:** an unfound fourth per-frame subscriber (mitigated by the move-1 grep sweep); the ALC-zombie mechanism (A2) is inferred not logged, so the A1 repro is the confirmation gate; the static→instance debt from COA-2 remains as future work.
- **Orders:** battle plan at [parselord5-reload-crash-battle-plan.md](parselord5-reload-crash-battle-plan.md). Read-only until the user accepts.

## 7 · Supervision & after-action

- **Execution log:** *(pending user acceptance of orders — not yet executed)*
- **Re-convene events:** none yet.
- **Reviewer verdict (original pass):** Phase 7 review completed as an explicit fresh-eyes pass (subagents were exhausting the usage-credit budget, so the skill's no-subagent Reviewer-hat fallback was used, not skipped). Found and fixed the MOVE 3 null-only defect (promoted Enable-guarded re-create to primary). **Missed the F6 citation error below** — the same-session self-review did not re-verify the log-line caller attribution, only the reasoning built on top of it.
- **Correction (2026-07-07, post-hoc):** the skill's own eval loop — an independently-launched baseline agent working the identical crash prompt with no memory of this dossier — re-derived the code-level defects from scratch (F1/F2/F3/F4 all independently confirmed) but flagged that the "corroborating" log lines I cited (F6) don't belong to ParseLord5. Direct re-inspection confirmed the baseline agent was right: `dalamud.log:4219` names `PortraitFixer` as the hook-creation caller, `:4265` is explicitly logged as `[ActionStacksEX]`. F6 is retracted (marked in §2); A2's confidence is downgraded to reflect it; COA-3's kill reasoning was re-derived without depending on it (still fails completeness on F1/F2/F3 alone). **Process lesson:** a same-session "Reviewer hat" sharing context with the Advisor is weaker than a genuinely independent agent for catching citation errors specifically — it tends to re-check reasoning, not re-verify source facts it already believes are settled. Recorded as a durable lesson (see write-back below) rather than silently fixed.
- **Quality gate:** `quality-gate` build check to run before "done" per `AGENTS.md:31` (note: on Linux only the `build` gate runs; pwsh-based domain-evals are skipped — see the quality-gate-linux-port finding).
- **Written back:** on execution, promote the reload-crash mechanism to `AgentBrain/pages/parselord5-plugin-reload-teardown-crash.md` and log to `state/log.md`, so the next session inherits it. (The existing `parselord5-autorotation-two-pass-heal-leak.md` is the model.)
