---
tags:
  - type/evaluation
  - project/parselord5
  - status/active
type: evaluation
project: parselord5
status: active
aliases: []
---
# ParseLord5 IPC / API Compatibility Evaluation - 2026-05-17

## Purpose

Evaluate the current IPC and API identity surfaces inherited from WrathCombo. Determine side-by-side binding risks, identify what external consumers exist, decide on a policy for each surface, and recommend a forward path.

## Provider IPC Inventory

ParseLord5 registers IPC methods under the **single prefix `"WrathCombo"`** at `Provider.cs:84`:

```csharp
EzIPC.Init(output, prefix: "WrathCombo");
```

### Provider methods exposed (all under `WrathCombo` prefix)

| Method | Type | Description |
|---|---|---|
| `Test` | Action | IPC connection test |
| `IPCReady` | Func\<bool\> | Check if IPC fully initialized |
| `RegisterForLease` | Func\<string, string, Guid?\> | Register for lease control |
| `RegisterForLeaseWithCallback` | Func\<string, string, string?, Guid?\> | Register with IPC callback |
| `GetAutoRotationState` | Func\<bool\> | Get auto-rotation state |
| `SetAutoRotationState` | Func\<Guid, bool, SetResult\> | Set auto-rotation state |
| `IsCurrentJobAutoRotationReady` | Func\<bool\> | Check if job is AR-ready |
| `SetCurrentJobAutoRotationReady` | Func\<Guid, SetResult\> | Set job as AR-ready |
| `ReleaseControl` | Action\<Guid\> | Release lease control |
| `IsCurrentJobConfiguredOn` | Func\<Dict\> | Check job combo config |
| `IsCurrentJobAutoModeOn` | Func\<Dict\> | Check auto-mode state |
| `GetComboNamesForJob` | Func\<uint, List\<string\>?\> | Get combo names for job |
| `GetComboOptionNamesForJob` | Func\<uint, Dict?\> | Get option names for job |
| `GetComboState` | Func\<string, Dict?\> | Get combo enabled/auto state |
| `SetComboState` | Func\<Guid, string, bool, bool, SetResult\> | Set combo state |
| `GetComboOptionState` | Func\<string, bool\> | Get option enabled state |
| `SetComboOptionState` | Func\<Guid, string, bool, SetResult\> | Set option state |
| `GetAutoRotationConfigState` | Func\<object, object?\> | Get AR config option |
| `SetAutoRotationConfigState` | Func\<Guid, object, object, SetResult\> | Set AR config option |
| `OnActionUsed` (provider) | ICallGateProvider | Broadcast action usage |

### Ancillary IPC identity

| Surface | File:Line | Value | Exposed? |
|---|---|---|---|
| Lease callback signature | `Helper.cs:506` | `ParseLord5Callback(int, string)` | Yes — consumers implement this method name |
| IPC status endpoint | `Helper.cs:390` | `PunishXIV/WrathCombo/…/ipc_status.txt` | Yes — remote kill-switch |
| IPC debug label | `Debug.cs:1020` | `"Wrath IPC"` | Yes — in-plugin debug UI |

---

## Subscriber IPC Inventory (ParseLord5 consuming others)

ParseLord5 subscribes to these external plugins' IPC. These are **not** affected by ParseLord5's own IPC identity.

| Plugin | Prefix | Purpose | File |
|---|---|---|---|
| PingPlugin | `PingPlugin.Ipc` | Latency data for combo timing | `PingPlugin.cs` |
| Orbwalker | `Orbwalker` | Movement state | `Orbwalker.cs` |
| BossMod | `BossMod` | Action queue + targeting conflicts | `BossMod.cs` |
| BossModReborn | `BossModReborn` | Same checks for reborn variant | `ConflictingPlugins.cs` |
| MOAction | `MOAction` | Retargeted actions | `MOAction.cs` |
| ReAction | `ReAction` | Retargeted action stacks | `Reaction.cs` |
| Redirect | `Redirect` | Mouseover redirects | `Redirect.cs` |
| RotationSolver | `RotationSolver` | Conflict detection | `ConflictingPlugins.cs` |

---

## WrathCombo.API Project

The `WrathCombo.API` project (`WrathCombo/WrathCombo.API/`) provides enums used by external consumers:

| Enum | Purpose | Consumers |
|---|---|---|
| `AutoRotationConfigOption` | Names and types for AR config IPC | All IPC consumers |
| `SetResult` | Status codes for Set IPC calls | All IPC consumers |
| `CancellationReason` | Lease cancellation reasons | IPC callback handlers |
| `ComboTargetTypeKeys`, `ComboStateKeys`, `ComboSimplicityLevelKeys` | Job config state enums | All IPC consumers |
| `ActionType` | Action type classification | OnActionUsed IPC |

External consumers reference this as a NuGet package or copy enums directly. Doc reference: `docs/IPC.md:17` points to `https://github.com/PunishXIV/WrathCombo.API`.

---

## Known External Consumers of WrathCombo IPC

From source analysis and docs:

| Consumer | Evidence | Connection Pattern |
|---|---|---|
| **AutoDuty** | `docs/IPC.md:358-417`, `CustomCombo/StancePartner.cs:45` | `EzIPC.Init(typeof(WrathIPC), "WrathCombo")` + `TryGetDalamudPlugin("WrathCombo")` |
| **Any plugin following docs/IPC.md** | `docs/IPC.md:250-297` example code | Same pattern as AutoDuty |

Note: AutoDuty and other documented consumers typically use BOTH:
1. `TryGetDalamudPlugin("WrathCombo")` for plugin existence check
2. `EzIPC.Init(typeof(WrathIPC), "WrathCombo")` for IPC binding

---

## Side-by-Side Binding Risk: The Critical Finding

### Two-part identity (resolved 2026-05-17)

ParseLord5 now has:
- **IPC registration prefix**: `"ParseLord5"` (at `Provider.cs:84`, changed from `"WrathCombo"`)
- **Plugin InternalName**: `"ParseLord5"` (at `ParseLord5.json:4`)

Both now match. External consumers can use:
- **Discovery**: `TryGetDalamudPlugin("ParseLord5")` — finds ParseLord5 by InternalName ✅
- **Binding**: `EzIPC.Init(typeof(ParseLord5IPC), "ParseLord5")` — connects to IPC by matching prefix ✅

### Previous binding matrix (before fix)

The prior mismatch was:
| Scenario | Result |
|---|---|
| Only ParseLord5 loaded (old: prefix `"WrathCombo"`) | **Broken** — consumers couldn't find ParseLord5 via InternalName |
| Both loaded (old: duplicate `"WrathCombo"` prefix) | **Race condition** — prefix collision between two plugins |

### Current binding matrix (after fix)

| Scenario | Discovery (`TryGetDalamudPlugin`) | IPC Binding (`EzIPC.Init`) | Result |
|---|---|---|---|
| Only ParseLord5 loaded | Finds `"ParseLord5"` ✅ | Binds to `"ParseLord5"` prefix ✅ | **Works** |
| Only WrathCombo loaded | Finds `"WrathCombo"` | Binds to `"WrathCombo"` | Works as expected |
| Both loaded | Each finds its own InternalName | Each binds to its own prefix | **No collision** — separate prefixes, separate plugins |

### Practical impact

**ParseLord5's IPC is now reachable by consumers that check for `"ParseLord5"`.** Consumers following a ParseLord5-aware pattern (using `TryGetDalamudPlugin("ParseLord5")` + `EzIPC.Init(..., "ParseLord5")`) can connect successfully. WrathCombo consumers continue to use their own pattern unchanged.

---

## WrathCombo.API Risk

The `WrathCombo.API` NuGet package is referenced by external consumers. ParseLord5 cannot rename this without breaking external references. However:

- ParseLord5's internal build references the same `WrathCombo.API` project
- The enums are identical to upstream — no compatibility break within those enums
- Future changes to enums could diverge if ParseLord5 adds values not in upstream

**Risk level**: Low for now. Medium if ParseLord5 adds API-breaking enum changes.

---

## Recommendation: Single Prefix (implemented 2026-05-17)

The provider IPC prefix has been changed from `"WrathCombo"` to `"ParseLord5"` at `Provider.cs:84`. This resolves the InternalName/prefix mismatch.

### Current consumer pattern (ParseLord5)

```csharp
// Check plugin is loaded
DalamudReflector.TryGetDalamudPlugin("ParseLord5", out _, false, true);

// Bind to ParseLord5 IPC
EzIPC.Init(typeof(ParseLord5IPC), "ParseLord5");
```

### WrathCombo consumers (unchanged)

```csharp
// Check plugin is loaded
DalamudReflector.TryGetDalamudPlugin("WrathCombo", out _, false, true);

// Bind to WrathCombo IPC
EzIPC.Init(typeof(WrathIPC), "WrathCombo");
```

Both patterns now use matching InternalName + prefix pairs. No collision between the two plugins.

### Remaining deferred IPC surfaces

| Surface | Value | Reason |
|---|---|---|
| IPC callback name | `ParseLord5Callback` | External consumers implement this method signature; changing it requires consumer coordination |
| IPC status endpoint | `PunishXIV/WrathCombo/…/ipc_status.txt` | Upstream dependency; requires ParseLord5-owned endpoint |
| WrathCombo.API namespace | `WrathCombo.API` | Public API surface; requires consumer coordination |

### Action Items

No further code changes recommended in this milestone. The prefix change is complete.
