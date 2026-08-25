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
# Battle Plan - PL5-native Action Stacks

**Origin:** [pl5-native-action-stacks-war-room.md](pl5-native-action-stacks-war-room.md)
**Status:** STILL VALID (reconciled 2026-08-11) — no COA-2 target has landed; the plan is unexecuted, not superseded. Execute as written *after* reading the Reconciliation section below, which corrects the theatre map and two PASS criteria that are already false on arrival.
**Selected COA:** port only ActionStacksEX's Action Stacks behavior into PL5 as a native, disabled-by-default module and tab.
**Tracking:** [#6](https://github.com/kruillin-lab/ParseLord5/issues/6) — scope call pending.

## Reconciliation — 2026-08-11

Verified read-only against branch `main` @ `aafddadd5`. **Classification: STILL VALID.** Every COA-2 deliverable is absent. The only ActionStacks-related code in PL5 is the sibling COA-1 IPC bridge, which the war room screened as the *losing* option; it targets the opposite code path from this plan and is inert against the checked-out ActionStacksEX source.

### Per-target status

| # | Plan target | Landed? | Evidence |
|---|-------------|---------|----------|
| 1 | Module folder `WrathCombo/ActionStacks/` (`ActionStackConfig.cs`, `ActionStackDecision.cs`, `ActionStackRuntimeState.cs`) | No | glob `WrathCombo/ActionStacks/**` → path does not exist |
| 2 | `Configuration.EnableNativeActionStacks` / `Configuration.NativeActionStacks` | No | grep `NativeActionStacks` over `WrathCombo/Core/Configuration.cs` → 0 matches |
| 3 | `ActionStackTargetResolver.cs` (Move 2) | No | glob `WrathCombo/ActionStacks/**` → path does not exist |
| 4 | `ActionStackResolver.cs` (Move 3) | No | glob `WrathCombo/ActionStacks/**` → path does not exist |
| 5 | Resolver call in `ActionWatching.UseActionDetour` before the manual-queue override | No | grep `ActionStacks` over `WrathCombo/` matches only `WrathCombo/Services/IPC_Subscriber/ActionStacksEXIPC.cs`, `WrathCombo/Services/IPC_Subscriber/AllStaticIPCSubscriptions.cs:11`, and `WrathCombo/AutoRotation/AutoRotationController.cs:336,357` — nothing in `ActionWatching.cs` |
| 6 | `WrathCombo/Window/Tabs/ActionStacksTab.cs` | No | `WrathCombo/Window/Tabs/` contains only `Settings.cs`, `Debug.cs`, `PvEFeatures.cs`, `PvPFeatures.cs`, `AutoRotationTab.cs`, `CustomActions.cs` |
| 7 | `OpenWindow.ActionStacks` + sidebar row + body route | No | `WrathCombo/Window/ConfigWindow.cs:373-383` — enum ends at `CustomActions = 7`; sidebar and body switch stop at `CustomActions` (`ConfigWindow.cs:240-241`, `ConfigWindow.cs:310-311`) |
| 8 | Preset / config toggle surface | No | grep `ActionStacks` over `WrathCombo/Combos/CustomComboPreset.cs` and `WrathCombo/Window/` → 0 matches |
| 9 | `NativeActionStacks_*` tests in `WrathCombo.Tests/RotationStructureTests.cs` | No | grep `ActionStacks` over `WrathCombo.Tests/` → 0 matches |

### What landed instead

| Item | Status | Evidence |
|------|--------|----------|
| COA-1's IPC bridge (the option this war room rejected) | Landed | `WrathCombo/Services/IPC_Subscriber/ActionStacksEXIPC.cs:8-51`; disposed at `WrathCombo/Services/IPC_Subscriber/AllStaticIPCSubscriptions.cs:11`; shipped in commit `76912de5a` |
| Bridge call sites | Autorotation path only | `WrathCombo/AutoRotation/AutoRotationController.cs:335-341` and `:356-362`, both inside `UseAutorotAction`, i.e. exactly where `IsIssuingAutorotAction` is `true` (`AutoRotationController.cs:332`, `:353`) |
| Bridge is currently inert | Yes | The subscriber calls the gate `ActionStacksEX.PrepareAction` (`ActionStacksEXIPC.cs:10`). ActionStacksEX @ `2a693f3` registers no such provider — grep for `CallGate`/`GetIpcProvider`/`RegisterFunc` over that repo returns only Hypostasis debug gates (`Hypostasis/Debug/DebugIPC.cs:130-150`). `InvokeFunc` therefore throws and is swallowed at `ActionStacksEXIPC.cs:45-49`, so `TryPrepareAction` always returns `false`. |
| Move 4 PASS criterion "action replacement does not leave `Service.ActionReplacer` disabled on early returns" | Satisfied independently | commit `76912de5a` on `WrathCombo/Data/ActionWatching.cs` added the `originalCalled` guard and wrapped `EnableActionReplacingIfRequired()` in try/catch on the exception path |

### Corrections a future executor must apply

- **Move 1 PASS criterion "No ActionStacksEX namespace references exist in PL5 code" is already false.** `WrathCombo/Services/IPC_Subscriber/ActionStacksEXIPC.cs` predates this execution. Either scope the criterion to `WrathCombo/ActionStacks/**` or delete the dead bridge as an explicit step in Move 4.
- **Move 6's `NativeActionStacks_DoesNotReferenceActionStacksEXNamespace` fails on arrival** for the same reason; scope its assertion to the new module folder.
- **Design Contract rule 3 ("must not run stack replacement for `IsIssuingAutorotAction`") now conflicts with landed code.** The IPC bridge runs *only* on autorotation-issued actions. If both survive and ActionStacksEX ever registers `PrepareAction`, autorotation actions get stack-prepared out-of-process while manual presses resolve natively — two stack engines on two paths. Resolve this explicitly before Move 4.
- **Sections 0, 4 and 8 were rewritten on 2026-08-11** from the dead Linux theatre (`/home/kruillin/...`, `parselord5-wc-base`) to the current Windows facts. The repo has exactly one branch, `main`.

## 0. Theatre Map

- **Repo:** `C:\Users\kruil\orca\ParseLord5` (Windows). Exactly one branch, `main`; `AGENTS.md` forbids creating others. The old Linux checkout `/home/kruillin/Projects/Projects/ParseLord5` and the branch `parselord5-wc-base` are dead — do not abort on their absence.
- **Reference repo (read-only):** `C:\Users\kruil\Documents\Projects\ActionStacksEX`, HEAD `2a693f3`.
- **Primary PL5 hook:** `WrathCombo/Data/ActionWatching.cs`
- **PL5 config:** `WrathCombo/Core/Configuration.cs`
- **PL5 UI entry:** `WrathCombo/Window/ConfigWindow.cs`
- **New PL5 module folder:** `WrathCombo/ActionStacks/` (does not exist yet)
- **Expected new tab:** `WrathCombo/Window/Tabs/ActionStacksTab.cs` (does not exist yet)
- **Pre-existing ActionStacksEX seam inside PL5:** `WrathCombo/Services/IPC_Subscriber/ActionStacksEXIPC.cs` — read the Reconciliation section before starting Move 1.
- **Regression tests:** `WrathCombo.Tests/RotationStructureTests.cs` plus focused pure resolver tests if the resolver can be separated from Dalamud structs.
- **Baselines to preserve (2026-08-11):** build 0 errors / 0 warnings; `dotnet test` 55 passed / 0 failed; rotation evals passed=14 failed=0 (14 since the teardown-event-subscription-symmetry fixture landed 2026-08-11).
- **Deploy target:** the Release build writes straight to `C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll`; no manual copy step.
- **Manual test target:** SGE on a target dummy with ActionStacksEX disabled and PL5-native stacks enabled.

## 1. Proof Before Edit

1. Capture current repo state.
   - Expected: worktree is dirty from existing PL5 fixes; do not revert unrelated changes.
   - Record: changed files that pre-exist before this implementation.

2. Confirm the user's active hypothesis.
   - Expected: ActionStacksEX disabled reduces or stops the SGE problem.
   - If uncertain: keep the implementation behind a default-off toggle and do not delete ActionStacksEX.

3. Confirm first-slice feature scope.
   - Read `ActionStacksEX/Configuration.cs`.
   - Expected: the needed stack model is `ActionStack` + `ActionStackItem`; unrelated QoL flags are out of scope.

4. Confirm runtime behavior to port.
   - Read `ActionStacksEX/ActionStackManager.cs`.
   - Expected: stack matching uses adjusted trigger, modifier keys, block-original, item target, item action override, HP/status/range/cooldown checks, execution lock, and duplicate trigger suppression.

5. Confirm PL5 hook insertion.
   - Read `WrathCombo/Data/ActionWatching.cs`.
   - Expected: native stack resolution should happen after `actionType is ActionType.Action` and before `AutoRotationController.IsManualActionOverrideCandidate(actionId)` stores/replays the original action.

6. Confirm PL5 UI insertion.
   - Read `WrathCombo/Window/ConfigWindow.cs`.
   - Expected: add an `ActionStacks` sidebar entry and `OpenWindow.ActionStacks` switch case, mirroring `CustomActions`.

## 2. Design Contract

Implement a deep PL5 module with one small caller-facing interface:

```csharp
internal enum ActionStackDecisionKind
{
    NoMatch,
    UseOriginal,
    Replace,
    Block,
    Locked,
}

internal readonly record struct ActionStackDecision(
    ActionStackDecisionKind Kind,
    uint TriggerActionId,
    uint ResolvedActionId,
    ulong TargetObjectId,
    string? StackName);
```

Rules:

- The resolver returns a decision; `ActionWatching` owns the final game action call.
- The resolver hides stack matching, modifier handling, cooldown/range/status checks, target lookup, execution windows, and lock state.
- The first pass must not run stack replacement for `AutoRotationController.IsIssuingAutorotAction`.
- Unsupported imported targets must be visible and disabled, not silently guessed.
- ActionStacksEX remains a separate plugin; PL5-native stacks do not require it.

## 3. Edit Sequence

### Move 1 - Config Model

Add PL5-native models under `WrathCombo/ActionStacks/`:

- `ActionStackConfig.cs`
  - `ActionStackItemConfig`
  - `ActionStackConfig`
  - `ActionStackSerializer`
  - import/export helpers for `ASEX_` strings
- `ActionStackDecision.cs`
- `ActionStackRuntimeState.cs`

Add to `WrathCombo/Core/Configuration.cs`:

- `public bool EnableNativeActionStacks = false;`
- `public List<ActionStackConfig> NativeActionStacks = [];`

PASS criteria:

- Config defaults are inert.
- Existing PL5 config can deserialize without the new fields present.
- No ActionStacksEX namespace references exist in PL5 code.

### Move 2 - Target Mapping

Add `ActionStackTargetResolver.cs`.

Implement the target ids used by the ActionStacksEX stack editor first:

- self/player
- hard target
- soft target
- focus target
- mouseover/UI mouseover where PL5 already exposes it
- target-of-target
- party slots 1-8
- lowest HP party/member targets already available through PL5 `SimpleTarget`
- hostile target fallbacks already available through PL5 `SimpleTarget`

TRIGGER-TARGET-UNSUPPORTED:

- If an imported stack uses an id not mapped in PL5, import it disabled and attach an unsupported-target warning for the tab.
- Do not invent target semantics.

PASS criteria:

- Mapping is centralized.
- Resolver returns null plus reason when unsupported or missing.

### Move 3 - Resolver

Add `ActionStackResolver.cs`.

Port behavior in this order:

1. skip when feature disabled or no local player
2. skip when autorotation is issuing the action
3. compute adjusted action id
4. enforce current execution lock for the same trigger
5. match modifier keys
6. match trigger action with `UseAdjustedTrigger`
7. enforce recent-stack and duplicate-trigger windows
8. resolve each enabled item
9. check level, target validity, HP ratio, status/missing status, range, cooldown/charges, and casting state
10. return `Replace`, `UseOriginal`, `Block`, or `Locked`

PASS criteria:

- The resolver does not call `UseActionHook.Original`.
- The resolver records lock state only when a replacement is actually dispatched successfully by the caller.
- The resolver exposes enough debug state for the UI/debug tab to show the last stack decision.

### Move 4 - Hook Integration

Modify `WrathCombo/Data/ActionWatching.cs`.

Insertion point:

- after `if (actionType is ActionType.Action)` opens
- after custom action `OnClick`
- before `var prioritizeManualQueue = AutoRotationController.IsManualActionOverrideCandidate(actionId);`

Caller behavior:

- `NoMatch`: continue current PL5 path unchanged.
- `Locked`: return `false`.
- `Block`: return `false`.
- `UseOriginal`: continue current PL5 path, but do not create a stack lock.
- `Replace`: call the original game action using the resolved action and target, then notify runtime state whether dispatch succeeded.

Ground target guidance:

- First pass should reuse PL5's existing ground-target support where possible.
- Do not port ActionStacksEX instant ground-target QoL in this move.
- If ground targets are not safely supported in the first pass, detect and surface `TRIGGER-GROUND-DEFERRED` instead of half-implementing it.

PASS criteria:

- Non-stack manual actions preserve current manual queue behavior.
- Stack triggers are consumed before PL5 stores/replays the original trigger action.
- Action replacement does not leave `Service.ActionReplacer` disabled on early returns.

### Move 5 - PL5 Tab

Add `WrathCombo/Window/Tabs/ActionStacksTab.cs`.

Modify `WrathCombo/Window/ConfigWindow.cs`:

- add sidebar row: `Action Stacks`
- add `OpenWindow.ActionStacks`
- route body to `ActionStacksTab.Draw()`

Tab MVP:

- enable/disable PL5-native Action Stacks
- stack list
- add/delete/reorder
- import/export single stack via `ASEX_` clipboard string
- name, modifiers, exact-match flag
- trigger action and adjusted-trigger flag
- item list with enabled flag, target, override action, HP ratio, status id, missing-status flag
- block original, range check, cooldown check
- visible warning for unsupported imported targets
- debug readout for last decision and active lock

PASS criteria:

- User can recreate or import a basic ActionStacksEX stack in PL5.
- UI does not require ActionStacksEX helper classes.
- Text fits the existing PL5 settings style.

### Move 6 - Tests

Add structural tests to `WrathCombo.Tests/RotationStructureTests.cs`:

- `NativeActionStacks_ConfigIsDefaultOff`
- `NativeActionStacks_TabIsRegistered`
- `NativeActionStacks_ActionWatchingRunsBeforeManualQueueOverride`
- `NativeActionStacks_DoesNotReferenceActionStacksEXNamespace`
- `NativeActionStacks_ImportsAsexStacks`

Add pure tests if resolver dependencies are separable:

- matching trigger with no modifiers
- exact modifier mismatch returns `NoMatch`
- locked trigger returns `Locked`
- block-original failed stack returns `Block`
- unsupported target returns disabled/warning on import

PASS criteria:

- Focused tests fail before the relevant edit when possible.
- Full `WrathCombo.Tests` passes after implementation.

## 4. Verification Route

1. Run focused tests:

```bash
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --filter NativeActionStacks
```

PASS: all NativeActionStacks tests pass.

2. Run all tests:

```bash
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release
```

PASS: 55 baseline tests plus the new NativeActionStacks tests, 0 failed. Baseline on 2026-08-11 is 55 passed / 0 failed.

3. Build release:

```bash
dotnet build WrathCombo/WrathCombo.csproj -c Release
```

PASS: 0 errors, 0 warnings. That is the 2026-08-11 baseline — any new warning is a regression, not "baseline warnings". No `DalamudLibPath` override is needed on this machine.

4. Run rotation evals:

```bash
pwsh -NoProfile -File scripts/rotation-evals.ps1
```

PASS: passed=14 failed=0, matching the 2026-08-11 baseline.

5. Deploy dev plugin:

- The Release build already writes to `C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll`. Do not hand-copy from `bin/Release`.
- Record timestamp and SHA256 of the deployed DLL.

## 5. Manual Verification

1. Disable ActionStacksEX or at least disable its Action Stacks behavior.
2. Enable PL5-native Action Stacks.
3. Import or recreate one known Sage-relevant stack.
4. Fight a target dummy as SGE with PL5 autorotation enabled.
5. Press the stack trigger during GCD and outside GCD.

Expected:

- PL5 does not replay the original trigger after a stack consumed it.
- PL5 does not overwrite the stack's resolved target.
- The debug readout shows the trigger action, resolved action, target, and lock state.
- SGE no longer shows the previous ActionStacksEX-on conflict while ActionStacksEX is disabled.

Then test one non-stack manual GCD and one non-stack manual oGCD.

Expected:

- Existing manual queue override behavior remains unchanged.

## 6. Trigger Forks

- **TRIGGER-TARGET-UNSUPPORTED:** imported stack references a target id PL5 does not map. Import disabled, show warning, and add the exact id to the follow-up list.
- **TRIGGER-GROUND-DEFERRED:** stack item resolves to a ground-target action and PL5 cannot safely place it. Leave original behavior untouched and mark ground stack support as Phase 2.
- **TRIGGER-MANUAL-QUEUE-REGRESSION:** non-stack manual queue behavior changes. Move the resolver call later or narrow it so only matched triggers bypass manual queue.
- **TRIGGER-AUTOROT-STACKS-REQUESTED:** user wants autorotation to intentionally trigger native stacks. Keep first pass manual-only; design a separate opt-in.
- **TRIGGER-ACTIONSTACKSEX-DEPENDENCY:** implementation accidentally references ActionStacksEX types/namespaces. Remove the reference; PL5 must be standalone.
- **TRIGGER-SGE-STILL-FAILS:** SGE conflict persists with ActionStacksEX disabled and PL5-native stacks enabled. Re-convene from recon: instrument PL5 native stack decisions and autorotation use calls.

## 7. Abort Conditions

- **ABORT-BUILD:** release build fails for code reasons.
- **ABORT-SERIALIZATION:** existing PL5 config cannot deserialize after adding stack config.
- **ABORT-HOOK:** `Service.ActionReplacer` can remain disabled after a stack early return.
- **ABORT-SCOPE:** implementation starts porting unrelated ActionStacksEX QoL modules.
- **ABORT-LOSS:** imported stack target/action semantics cannot be represented in PL5 without broad target-system work.

## 8. Report Skeleton

- Files changed:
  - `WrathCombo/ActionStacks/*`
  - `WrathCombo/Core/Configuration.cs`
  - `WrathCombo/Data/ActionWatching.cs`
  - `WrathCombo/Window/ConfigWindow.cs`
  - `WrathCombo/Window/Tabs/ActionStacksTab.cs`
  - `WrathCombo.Tests/RotationStructureTests.cs`
  - `wargames/pl5-native-action-stacks-war-room.md`
  - `wargames/pl5-native-action-stacks-battle-plan.md`
- Tests:
  - focused NativeActionStacks tests: `<result>`
  - full `WrathCombo.Tests`: `<result>`
- Build: `<result>` (baseline 0 errors / 0 warnings)
- Rotation evals: `<result>` (baseline passed=14 failed=0)
- Deployed DLL: `C:\Users\kruil\AppData\Roaming\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll`, timestamp `<timestamp>`, SHA256 `<hash>`
- Manual ask: disable ActionStacksEX, enable PL5-native stacks, import/recreate the Sage stack, then retest SGE on a dummy.

## 9. Deferred Items

| Item | Reason |
|------|--------|
| Turbo hotbars | Unrelated to the Sage conflict and higher input-risk. |
| QueueMore / queue adjustments | PL5 already has queue behavior; merging another queue system is a separate war-room. |
| Decombos | Unrelated behavior with separate hotbar semantics. |
| AutoTarget / AutoFocus / AutoRefocus | Could reintroduce target conflicts; defer until native stacks prove stable. |
| Custom placeholders beyond mapped target ids | Import unsupported ids disabled first; add exact mappings based on user stack evidence. |
| Autorotation-triggered stacks | First pass should make manual stacks safe; autorotation opt-in needs a separate contract. |
