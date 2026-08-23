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
# War Room - PL5-native Action Stacks

**Status:** DECIDED
**Date opened:** 2026-07-08
**Skill:** `war-room`, wargames execution-planning mode
**AER route:** Advisor/Reviewer role-hat fallback; no subagents spawned because this session did not explicitly request parallel agents.
**Battle plan:** [pl5-native-action-stacks-battle-plan.md](pl5-native-action-stacks-battle-plan.md)

User reported that disabling ActionStacksEX appears to fix the Sage problem, then selected the option of putting the Action Stacks behavior inside ParseLord5 instead of coordinating two plugins at runtime.

## 1. Frame

- **Current state:** PL5 and ActionStacksEX both participate in action use. Disabling ActionStacksEX appears to make SGE behave, so the working theory is a cross-plugin action/queue/target interaction rather than a Sage-only rotation defect.
- **Desired end state:** PL5 owns a native Action Stacks tab and resolver so PL5 can tell when a stack trigger has consumed or locked a button, without depending on ActionStacksEX timing.
- **Five Ws:** user testing SGE; PL5 autorotation plus ActionStacks-style manual stack buttons; repo `/home/kruillin/Projects/Projects/ParseLord5`; 2026-07-08 after ActionStacksEX-off observation; reduce two-plugin conflict by collapsing the stack feature into PL5.
- **In scope:** stack config model, stack resolver, PL5 config/UI tab, PL5 `UseAction` integration, import/export compatibility for ActionStacksEX stack strings, focused structural tests, build/deploy verification.
- **Out of scope:** porting ActionStacksEX turbo hotbars, queue adjustments, auto-target, auto-focus, decombos, camera-relative actions, spell auto-attacks, or custom placeholder UI in the first pass.
- **Constraints:** preserve existing PL5 rotation/autoration fixes; do not overwrite unrelated dirty worktree changes; do not require ActionStacksEX to be installed; keep the first pass reversible behind a PL5 config toggle; quality gate before declaring implementation complete.
- **Commander's intent:** A manually pressed PL5-native stack trigger must resolve, block, or lock inside PL5 before autorotation/manual-queue logic can replay or overwrite the same button.

## 2. Recon

### Facts

| # | Fact | Evidence |
|---|------|----------|
| F1 | Prior shared context already treats disabling ActionStacksEX as a likely PL5/ActionStacks interaction path and names `ActionWatching` plus `AutoRotationController` as the relevant PL5 seams. | `/home/kruillin/.codex/memories/MEMORY.md:142-149` |
| F2 | ActionStacksEX keeps stack data in a small model: stack item action, target id, enabled flag, HP ratio, status check, trigger action, adjusted-trigger flag, modifier mask, block-original, range, and cooldown flags. | `/home/kruillin/Projects/Projects/ActionStacksEX/Configuration.cs:17-37` |
| F3 | ActionStacksEX config contains many unrelated QoL settings, so importing the whole plugin would drag in more behavior than the selected option requires. | `/home/kruillin/Projects/Projects/ActionStacksEX/Configuration.cs:78-121` |
| F4 | ActionStacksEX already exports/imports individual stack definitions with the `ASEX_` prefix, which gives PL5 a migration path without reading ActionStacksEX live state. | `/home/kruillin/Projects/Projects/ActionStacksEX/Configuration.cs:127-143` |
| F5 | ActionStacksEX's stack runtime tracks a 3-second execution window, the currently executing stack, and the locked trigger action. | `/home/kruillin/Projects/Projects/ActionStacksEX/ActionStackManager.cs:24-35` |
| F6 | ActionStacksEX matches only action uses with stack-eligible use types, adjusted action ids, modifier keys, and trigger action checks before resolving the first usable stack item. | `/home/kruillin/Projects/Projects/ActionStacksEX/ActionStackManager.cs:43-107` |
| F7 | On success, ActionStacksEX replaces the action id and target, records the lock, invokes the original game `UseAction`, and handles ground-target queue bookkeeping. | `/home/kruillin/Projects/Projects/ActionStacksEX/ActionStackManager.cs:144-210` |
| F8 | Stack item resolution checks enabled state, action adjustment, target resolution, level, target kind, HP ratio, status, range, cooldown/charges, and casting state. | `/home/kruillin/Projects/Projects/ActionStacksEX/ActionStackManager.cs:231-402` |
| F9 | ActionStacksEX already has a usable stack editor shape: stack list, add/delete/reorder, export/import buttons, modifier flags, block/range/cooldown flags, trigger editor, item target/action/HP/status controls. | `/home/kruillin/Projects/Projects/ActionStacksEX/PluginUI.cs:74-370` |
| F10 | PL5 currently routes config body tabs through `OpenWindow`, with `CustomActions` already present as a sidebar tab; adding an `ActionStacks` tab is a local `ConfigWindow`/tab-file change. | `WrathCombo/Window/ConfigWindow.cs:227-241`, `WrathCombo/Window/ConfigWindow.cs:279-312`, `WrathCombo/Window/ConfigWindow.cs:373-383` |
| F11 | PL5's action interception seam is `ActionWatching.UseActionDetour`; it currently handles custom-action clicks, manual queue override, retargeting, queued target updates, ground retargeting, and original `UseAction` dispatch. | `WrathCombo/Data/ActionWatching.cs:557-736` |
| F12 | PL5 config already stores non-setting collections such as custom actions and auto-action config, so a `NativeActionStacks` collection can live beside those rather than in per-job preset settings. | `WrathCombo/Core/Configuration.cs:432-443` |
| F13 | PL5 build guidance says this is an in-process Dalamud plugin; verification is build/tests/domain checks, and in-game loading needs XIVLauncher/Dalamud. | `docs/AGENTS_FULL.md:49-83` |

### Assumptions

| # | Assumption | Settling check (read-only or first implementation proof) | Routing |
|---|------------|----------------------------|---------|
| A1 | The first PL5-native slice can ignore ActionStacksEX's unrelated QoL modules without losing the behavior needed for the Sage conflict. | Compare user stacks against `ActionStacksEX.Config.ActionStacks`; confirm conflict reproducer uses stack triggers, not queue/turbo/decombo settings. | If user relies on QueueMore/TurboHotbars, keep those in ActionStacksEX disabled during PL5 stack testing and plan a separate module later. |
| A2 | PL5 can reuse existing `SimpleTarget`/`PronounService` targets for the stack target ids ActionStacksEX uses most often. | Build a target-id mapping table for ids used by imported stacks; log or surface unsupported ids. | If a target id is unsupported, import the stack disabled and mark it with an unsupported-target warning. |
| A3 | The correct PL5 hook order is before manual queue override, so a stack trigger can be consumed before PL5 replays the original action. | Structural test plus manual queue test during implementation. | If this breaks normal manual overrides, move only locked-trigger detection before manual queue and apply replacement after original PL5 retargeting. |
| A4 | Autorotation should not run stack replacement on actions PL5 issues internally. | Check `AutoRotationController.IsIssuingAutorotAction` when stack resolver is called. | If user wants autorotation to trigger stacks intentionally, add an explicit opt-in later. |

## 3. Criteria

**Screening:** feasible; suitable; acceptable; distinguishable; complete

| Criterion | Weight | Why it matters here |
|-----------|--------|---------------------|
| Stops PL5/ActionStacks conflict | 4 | This is the live Sage-facing problem. |
| Keeps PL5 ownership local | 4 | The selected option is to collapse the stack feature into PL5, not coordinate two live plugins. |
| Limits blast radius | 3 | The repo already has broad in-flight rotation changes. |
| Preserves existing stacks | 3 | User should not have to rebuild stack config from scratch. |
| Testability | 3 | The implementation must be guardable with structural/pure resolver tests plus build. |
| UI completeness | 2 | The option specifically calls for a PL5 tab, not a hidden config-only feature. |

## 4. Courses Of Action

### COA-0 - Keep ActionStacksEX disabled

- **Purpose:** avoid the conflict by not running ActionStacksEX.
- **Contract:** no code change.
- **Work guidance:** user keeps ActionStacksEX disabled while PL5 runs.
- **Verification:** SGE remains stable.
- **Screen:** killed. Not complete; loses stack behavior.

### COA-1 - IPC awareness bridge

- **Purpose:** keep ActionStacksEX as the owner and teach PL5 to ask whether a trigger is locked/resolved.
- **Contract:** touch both repos; add ActionStacksEX IPC provider and PL5 subscriber.
- **Work guidance:** expose trigger state and resolution over IPC, then have PL5 back off from locked triggers.
- **Verification:** PL5 behaves with ActionStacksEX enabled and disabled.
- **Screen:** survives, but lower-ranked because timing and deployment of two plugins remains part of the problem.

### COA-2 - PL5-native Action Stacks module

- **Purpose:** port only ActionStacksEX's stack model/resolver/editor into PL5 and run it inside PL5's action detour.
- **Contract:** touch PL5 config, a new `WrathCombo/ActionStacks/` module, config window tab, `ActionWatching`, and tests. Do not port unrelated ActionStacksEX modules.
- **Work guidance:** add a disabled-by-default native stack feature, `ASEX_` import/export, resolver result contract, hook integration before manual queue override, and visible debug state.
- **Verification:** structural tests, resolver tests where practical, release build, deploy, manual SGE/stacks test with ActionStacksEX disabled.
- **Screen:** selected.

### COA-3 - Full ActionStacksEX merge into PL5

- **Purpose:** absorb all ActionStacksEX features into PL5.
- **Contract:** very broad; would bring queue, target, macro, hotbar, decombo, camera, and module framework behavior into PL5.
- **Work guidance:** copy much of ActionStacksEX and reconcile frameworks.
- **Verification:** full plugin test matrix across every imported QoL feature.
- **Screen:** killed. Not acceptable for the first pass; too much unrelated behavior and risk.

## 5. Wargame

### COA-1 fought

| Move | Expected observation | Most-likely failure and cause | Counter-move |
|------|----------------------|-------------------------------|--------------|
| Add ActionStacksEX IPC state provider | PL5 can ask if a trigger is locked or resolved. | IPC returns stale state because ActionStacksEX mutates inside `OnUseAction` after PL5 has already made decisions. | Move to COA-2 or add pre-use IPC, which increases coupling. |
| PL5 backs off locked triggers | SGE conflict reduces while both plugins are enabled. | User disables or updates one plugin and the bridge silently degrades. | Keep a no-op adapter and show compatibility status in PL5 debug. |
| Preserve separate UIs | Existing ActionStacksEX UI remains source of truth. | User has to reason about two plugin states when debugging PL5. | Prefer COA-2 for locality. |

**Red-team verdict:** COA-1 is attractive for a small patch, but it preserves the cross-plugin ordering hazard that caused this investigation.
**Second-order effects:** future support would require checking two plugin versions and two configs.

### COA-2 fought

| Move | Expected observation | Most-likely failure and cause | Counter-move |
|------|----------------------|-------------------------------|--------------|
| Add disabled-by-default PL5 stack model/config | Build still passes; config migration is inert. | Config serialization breaks because nested types or defaults are not JSON-friendly. | Keep public POCO records/classes with default constructors and add a structural serialization smoke test. |
| Add resolver result contract | Resolver returns `NoMatch`, `UseOriginal`, `Replace`, `Block`, or `Locked` without invoking the game action. | Resolver grows shallow and leaks too many ActionManager details to callers. | Keep ActionManager-specific checks inside the resolver; expose one result object. |
| Integrate before manual queue override | Stack trigger is consumed before PL5 stores/replays the original manual action. | Normal manual queue behavior regresses. | Add trigger-fork test: only stack matches bypass manual queue; non-stack actions retain current path. |
| Add tab/editor/import | User can recreate/import stacks inside PL5. | UI port drags ActionStacksEX ImGui helpers not present in PL5. | Rebuild UI using PL5's existing ImGui/ECommons helpers; import editor fields incrementally. |
| Disable ActionStacksEX during manual test | Only PL5 mutates the action path. | User still has QoL needs from ActionStacksEX. | Run ActionStacksEX with stack module disabled later only after PL5-native stack behavior is proven. |

**Red-team verdict:** COA-2 can still fail if ActionStacksEX's target-id/pronoun behavior is more important than expected, but it gives the cleanest PL5-owned control over trigger state.
**Second-order effects:** PL5 grows a general action-stack module; future job-specific stack behavior should use this module rather than new per-job hacks.

### Decision Matrix

| Criterion (weight) | COA-0 | COA-1 | COA-2 | COA-3 |
|--------------------|-------|-------|-------|-------|
| Stops PL5/ActionStacks conflict (4) | 3 | 3 | 4 | 4 |
| Keeps PL5 ownership local (4) | 0 | 1 | 4 | 4 |
| Limits blast radius (3) | 4 | 2 | 3 | 0 |
| Preserves existing stacks (3) | 0 | 4 | 3 | 3 |
| Testability (3) | 1 | 2 | 4 | 1 |
| UI completeness (2) | 0 | 1 | 4 | 4 |
| **Total** | **25** | **43** | **78** | **58** |

## 6. Decision Record

- **Selected:** COA-2 - PL5-native Action Stacks module.
- **Why losers lost:** COA-0 drops functionality; COA-1 leaves cross-plugin timing in the critical path; COA-3 imports too much unrelated behavior for the first slice.
- **Residual risk accepted:** target-id compatibility may be incomplete on the first pass; unsupported imported targets should be visible and disabled rather than guessed.
- **Orders:** execute [pl5-native-action-stacks-battle-plan.md](pl5-native-action-stacks-battle-plan.md).

## 7. Supervision And After-Action

- **Execution log:** not started.
- **Re-convene events:** reconvene from Phase 2 if the user's active stacks depend on unsupported target ids, if non-stack manual queue behavior regresses, or if the first live test still reproduces the Sage conflict with ActionStacksEX disabled.
- **Reviewer verdict:** role-hat review agrees with selecting a scoped native module, with one condition: do not copy ActionStacksEX's unrelated modules or hook framework into PL5 during the first implementation.
- **Quality gate:** to run after implementation; for this planning-only change, run markdown/repo gate if available and record environment failures separately.
- **Written back:** this dossier and sibling battle plan capture the decision and execution route.

## Outcome — 2026-08-11

Reconciled read-only against branch `main` @ `aafddadd5`. **COA-2 was never executed and remains STILL VALID** — see the battle plan's `## Reconciliation — 2026-08-11` for the per-target table.

- **Nothing from COA-2 exists.** No `WrathCombo/ActionStacks/` folder, no `NativeActionStacks` config fields, no `ActionStacksTab.cs`, no `OpenWindow.ActionStacks` (`WrathCombo/Window/ConfigWindow.cs:373-383` still ends at `CustomActions = 7`), and no `NativeActionStacks_*` tests (grep `ActionStacks` over `WrathCombo.Tests/` → 0 matches).
- **COA-1 landed instead of the selected COA.** Commit `76912de5a` added `WrathCombo/Services/IPC_Subscriber/ActionStacksEXIPC.cs:8-51` and wired it at `WrathCombo/AutoRotation/AutoRotationController.cs:335-341` and `:356-362`. This is the IPC awareness bridge this room screened as lower-ranked, not the native module.
- **That bridge is inert and is on the wrong path.** It calls the gate `ActionStacksEX.PrepareAction` (`ActionStacksEXIPC.cs:10`), which ActionStacksEX @ `2a693f3` does not register — the only providers in that repo are Hypostasis debug gates (`Hypostasis/Debug/DebugIPC.cs:130-150`), so `TryPrepareAction` always returns `false` via the catch at `ActionStacksEXIPC.cs:45-49`. It also runs exclusively inside `UseAutorotAction`, i.e. only where `IsIssuingAutorotAction` is `true` — the exact case assumption A4 and the battle plan's Design Contract said to exclude. The Commander's intent (a *manual* stack trigger resolving inside PL5 before manual-queue replay) is therefore unmet.
- **One incidental win:** the ABORT-HOOK hazard ("`Service.ActionReplacer` can remain disabled after an early return") was closed independently in the same commit `76912de5a`, which added the `originalCalled` guard and a try/catch re-enable in `WrathCombo/Data/ActionWatching.cs`.
- **Stale citations in this dossier:** F1-F9 and the Five Ws cite `/home/kruillin/...` paths from the dead Linux checkout. The live equivalents are repo `C:\Users\kruil\orca\ParseLord5` (single branch `main`) and reference repo `C:\Users\kruil\Documents\Projects\ActionStacksEX`. Facts F2-F9 were not re-verified line-by-line on this pass; treat their line numbers as approximate against `2a693f3`.
- **Execution log:** still not started. No re-convene trigger has fired; the decision stands and the battle plan is executable once its Reconciliation corrections are applied.
