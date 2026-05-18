---
tags:
  - type/policy
  - project/parselord5
  - status/active
type: policy
project: parselord5
status: active
aliases: []
---
# ParseLord5 Remaining Identity Policy - 2026-05-17

## Purpose

Document what Wrath-branded identity surfaces remain after the config-isolation, branding-shell, and MOTD/PunishLib/IPC cleanup milestones. Classify each remaining item as already-done, intentionally-deferred, or coordination-needed.

## Completed items (no further action needed)

| Item | File | Current value | Evidence |
|---|---|---|---|
| MOTD welcome text | `WrathCombo.cs:426` | `"Welcome to ParseLord5 v{version}!"` | Grep: zero `Welcome to WrathCombo` matches |
| MOTD prefix | `WrathCombo.cs:68` | `"[ParseLord5 Message of the Day] "` | Grep: zero `[Wrath Message of the Day]` matches |
| PunishLib init | `WrathCombo.cs:177` | `PunishLibMain.Init(…, "ParseLord5")` | Grep: zero `Wrath Combo` PunishLib matches |
| IPC log prefix | `Helper.cs:455` | `"[ParseLord5 IPC] "` | Grep confirms |
| Config isolation | `Search.cs:200` | `ConfigFile.FullName` | Grep: zero `WrathCombo.json` in `.cs` files |
| RepoCheck manifest | `RepoCheck.cs:25` | `"ParseLord5.json"` | Grep confirms |
| Window title / About | `ConfigWindow.cs:118` | `###ParseLord5` | Verified in branding-shell milestone |
| DTR labels | `WrathCombo.cs:226-235` | `"ParseLord5"` / `"ParseLord5 Opener"` | Verified in branding-shell milestone |
| Major Changes title | `MajorChangesWindow.cs:29,54` | `"ParseLord5 \| New Changes"` | Verified in branding-shell milestone |
| Plugin Name (P.Name) | `MainWindowUI.resx:179` | `"ParseLord5"` | Verified in branding-shell milestone |
| `/pl5` command | `Commands.cs:74` | `EzCmd.Add(ParseLord5Command, OnCommand)` | Registered |
| Manifest InternalName | `ParseLord5.json:4` | `"ParseLord5"` | Config isolation root cause |
| Build output | `WrathCombo.csproj` | `AssemblyName=ParseLord5`, `ParseLord5.dll` | Output paths use ParseLord5 |
| IPC provider prefix | `Provider.cs:84` | `EzIPC.Init(output, prefix: "ParseLord5")` | Prefix now matches InternalName |
| Manifest tags | `ParseLord5.json` | Includes `"parselord5"` | Tag added alongside upstream tags |
| Icon log strings | `ConfigWindow.cs:205,213` | `"ParseLord5 Icon"` | Cosmetic logs rebranded |

---

## IPC naming policy

### Current state (updated 2026-05-17)

| Item | File | Value |
|---|---|---|
| IPC prefix | `Provider.cs:84` | `EzIPC.Init(output, prefix: "ParseLord5")` |
| IPC callback name | `Helper.cs:506` | `WrathComboCallback` (deferred) |
| IPC status endpoint | `Helper.cs:390` | `https://raw.githubusercontent.com/PunishXIV/WrathCombo/main/res/ipc_status.txt` (deferred) |

### Policy decision: CHANGED to `"ParseLord5"`

The provider IPC prefix has been changed from `"WrathCombo"` to `"ParseLord5"`. This resolves the InternalName/prefix mismatch that was making ParseLord5's IPC unreachable by documented consumers.

**Rationale for the change:**

1. The prior `"WrathCombo"` prefix combined with InternalName `"ParseLord5"` meant consumers checking `TryGetDalamudPlugin("WrathCombo")` could never find ParseLord5.
2. Changing to `"ParseLord5"` makes the prefix match the InternalName, enabling the documented consumer pattern.
3. WrathCombo consumers continue to use `"WrathCombo"` prefix + InternalName — no collision.
4. See `docs/ParseLord5_IPC_API_Compatibility_Evaluation_20260517.md` for full analysis.

### Remaining deferred IPC surfaces

| Item | Status | Reason |
|---|---|---|
| IPC callback name `WrathComboCallback` | Deferred | External consumers implement this method signature; changing it requires consumer coordination |
| IPC status endpoint | Deferred | Upstream dependency; requires ParseLord5-owned endpoint |

---

## MOTD URL policy

### Current state

```csharp
// WrathCombo.cs:429
httpClient.GetAsync("https://raw.githubusercontent.com/PunishXIV/WrathCombo/main/res/motd.txt")
```

### Policy decision: KEEP AS UPSTREAM DEPENDENCY

The MOTD is fetched from the upstream WrathCombo repo. The local fallback text and prefix are already rebranded to ParseLord5.

**Rationale:**

1. The upstream MOTD URL serves live content from the WrathCombo team. Changing it to a ParseLord5-owned URL would either break MOTD functionality or require standing up a separate MOTD service.
2. The MOTD content is transient — users see the upstream team's messages, not ParseLord5's. This is acceptable while ParseLord5 tracks upstream content.
3. If ParseLord5 ever needs its own MOTD, create a separate endpoint and switch the URL. Do not remove the upstream URL unless a replacement exists.

---

## WrathCombo.API policy

### Current state

`WrathCombo.csproj:82`: `<ProjectReference Include="..\WrathCombo.API\WrathCombo.API.csproj" />`

### Policy decision: DEFER RENAME

The `WrathCombo.API` project and namespace remain unchanged.

**Rationale:**

1. External IPC consumers import `WrathCombo.API` for enums (`SetResult`, `CancellationReason`, `AutoRotationConfigOption`, etc.).
2. Renaming the project would break NuGet package references and using statements in consumer plugins.
3. This is a public API surface, not an internal implementation detail.

---

## Namespace/class policy

### Current state

All C# namespaces and the main plugin class remain under `WrathCombo.*`.

### Policy decision: DEFER RENAME

**Rationale:**

1. Namespace/class renames create massive diffs that destroy upstream mergeability.
2. They provide zero user-visible benefit — the plugin already shows as `ParseLord5` in Dalamud.
3. Stack traces and logs will still reference `WrathCombo.Combo.*` etc. This is cosmetic and not a bug.
4. A namespace rename should only be considered after all other ParseLord5-specific work is complete and upstream mergeability is no longer a priority.

---

## Manifest and build-metadata URLs

| Item | File | Current value | Policy |
|---|---|---|---|
| RepoUrl | `ParseLord5.json:8` | `PunishXIV/WrathCombo` | Defer until ParseLord5 has its own distribution repo |
| IconUrl | `ParseLord5.json:9` | `s3.puni.sh/…/wrathcombo.png` | Defer until ParseLord5 has its own icon asset |
| PackageProjectUrl | `WrathCombo.csproj:12` | `PunishXIV/WrathCombo` | Defer until ParseLord5 has its own NuGet/repo |
| Logo asset | `WrathCombo.csproj:69` | `res/plugin/wrathcombo.png` | Defer until ParseLord5 has its own asset |
| Manifest tags | `ParseLord5.json` | Includes `"wrathcombo"` (upstream) + `"parselord5"` (added) | Upstream tags preserved for discoverability; `parselord5` tag added |

---

## Cosmetic log strings (lowest priority)

These are debug/log strings visible only in developer logs. No user-facing impact.

| Item | File | Current value |
|---|---|---|---|
| ~~Icon log~~ | ~~`ConfigWindow.cs:205`~~ | **REBRANDED.** `"Using Local ParseLord5 Icon"` |
| ~~Icon log~~ | ~~`ConfigWindow.cs:213`~~ | **REBRANDED.** `"Using Remote ParseLord5 Icon"` |
| IPC doc comment | `Provider.cs:165` | `"Checks the state of the Wrath IPC."` |
| IPC doc comments | `Provider.cs` (various) | `"Wrath Combo"` references in xml docs |
| Debug UI label | `Debug.cs:1020` | `"Wrath IPC"` collapsing header |

**Policy:** These are safe to change but are the lowest priority. They can be cleaned up in a future cosmetic pass. Do not hold up any other milestone for them.

---

## Recommended future milestones

1. ~~**Live smoke + collision audit**~~ **DONE.** See `docs/ParseLord5_Runtime_Smoke_And_Collision_Audit_20260517.md`.
2. ~~**IPC identity**~~ **DONE.** Provider prefix changed to `"ParseLord5"`. See `docs/ParseLord5_IPC_API_Compatibility_Evaluation_20260517.md`.
3. ~~**Manifest and asset ownership** (light)~~ **PARTIALLY DONE.** Tags updated, cosmetic logs cleaned. RepoUrl/IconUrl/logo await repo ownership.
4. **WrathCombo.API evaluation** — determine whether the API project can remain as-is indefinitely or needs a forked version.
5. **Full manifest/asset ownership** — replace RepoUrl, IconUrl, logo asset, and PackageProjectUrl with ParseLord5-owned equivalents once a repo exists.
6. **Namespace/class rename** — only after all other ParseLord5 work is stable and upstream mergeability is no longer needed.
7. **Cosmetic log-string cleanup** (remaining) — IPC doc comments, debug UI labels. Lowest priority.
