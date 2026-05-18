---
tags:
  - type/audit
  - project/parselord5
  - status/active
type: audit
project: parselord5
status: active
aliases: []
---
# ParseLord5 Runtime Identity Audit - 2026-05-17

## Purpose

Determine whether ParseLord5's runtime identity and configuration are isolated from WrathCombo, or whether the two plugins can collide / overwrite each other's settings.

## Branch and commit

- Branch: `parselord5-wc-base`
- Commit: `b636dcfe9eed0fbfdbce6d0f411afc0085953ddb`

---

## 1. Manifest identity

### ParseLord5.json (active manifest)

| Field | Value |
|---|---|
| Author | Team Wrath |
| **Name** | **ParseLord5** |
| **InternalName** | **ParseLord5** |
| DalamudApiLevel | 15 |
| RepoUrl | https://github.com/PunishXIV/WrathCombo |
| IconUrl | https://s3.puni.sh/media/plugin/60/icon-2bwhkn3zf1f.png |
| Tags | combo, wrath, wrathcombo, auto, autorotation, rotation, pve, pvp |

### WrathCombo.json

- **Does not exist.** `WrathCombo.json` was not found in the WrathCombo/ folder.
- The `.csproj` (line 429) explicitly packages `ParseLord5.json` as the manifest:
  ```xml
  <None Update="ParseLord5.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  ```

### Remaining WrathCombo identity in manifest

- **RepoUrl** still points to `PunishXIV/WrathCombo`. This is cosmetic for this milestone.
- Tags still include `wrath` and `wrathcombo`.
- TestingDalamudApiLevel: not present in manifest.

---

## 2. Project/build identity

| Property | Value |
|---|---|
| Target framework | `net10.0-windows` |
| AssemblyName | `ParseLord5` |
| Output DLL | `ParseLord5.dll` |
| Dev plugin output path | `%appdata%\XIVLauncher\devPlugins\ParseLord5\` |
| Release output path | `bin\Release` |
| Installed plugin path | `%appdata%\XIVLauncher\installedPlugins\ParseLord5\{version}\` |
| PackageId | `ParseLord5` |
| Product | `ParseLord5` |

All build output paths use `ParseLord5`, not `WrathCombo`. No collision risk with WrathCombo output paths.

The logo asset (line 69) is `wrathcombo.png`, which is not a collision risk.

---

## 3. Configuration identity

### 3.1 Config load

`WrathCombo.cs` line 183:
```csharp
Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
```

`IPluginInterface.GetPluginConfig()` reads the config file from Dalamud's plugin config directory using the plugin's **InternalName**. Since the active manifest's InternalName is `"ParseLord5"`, the config is loaded from:

```
%appdata%\XIVLauncher\pluginConfigs\ParseLord5.json
```

### 3.2 Config save

`ConfigurationHelper.cs` lines 64 and 94:
```csharp
Svc.PluginInterface.SavePluginConfig(config);
```

`IPluginInterface.SavePluginConfig()` writes the config to the plugin config directory using the plugin's **InternalName**. Since InternalName is `"ParseLord5"`, the config is saved to:

```
%appdata%\XIVLauncher\pluginConfigs\ParseLord5.json
```

Internal `.Save()` calls (at least 35 call sites across the codebase) all flow through `ProcessSaveQueue()` → `SavePluginConfig(config)`.

### 3.3 Config file naming

There is no hardcoded `"WrathCombo"` string for config load/save. Both operations use `IPluginInterface` methods which derive the filename from the manifest `InternalName`.

### 3.4 IPC config path (updated 2026-05-17)

`WrathCombo/Services/IPC/Search.cs` now resolves `ConfigFilePath` through `Svc.PluginInterface.ConfigFile.FullName`. This is the authoritative Dalamud config file path and matches the file used by `GetPluginConfig()` / `SavePluginConfig()`. Earlier audits found a hardcoded `"WrathCombo.json"` path here; that has been removed and replaced with the Dalamud API call.

### 3.5 Verdict on config collision

| Question | Answer |
|---|---|
| Can ParseLord5 config overwrite WrathCombo config? | **No.** Different InternalName → different config files. |
| Can WrathCombo config be imported accidentally? | **No.** No config import logic exists. |
| Does ParseLord5 use "WrathCombo" as config filename? | **No.** It uses InternalName `"ParseLord5"`. |
| ParseLord5ExperimentalMode default? | **false** (Configuration.cs line 50). Stays off. |

---

## 4. Command identity

| Command | Const name | Registration line | Handler |
|---|---|---|---|
| `/wrath` | `Command` | `Commands.cs:67` | `OnCommand` |
| `/scombo` | `OldCommand` | `Commands.cs:73` | `OnCommand` |
| `/pl5` | `ParseLord5Command` | `Commands.cs:74` | `OnCommand` |

- All three commands exist and route to the same handler.
- `/wrath` remains the primary command with the most subcommands (`/wrath auto`, `/wrath debug`, `/wrath toggle`, etc.).
- No command behavior changed beyond adding `/pl5`.

---

## 5. IPC identity

### 5.1 IPC registration prefix

`WrathCombo/Services/IPC/Provider.cs` line 84:
```csharp
EzIPC.Init(output, prefix: "WrathCombo");
```

The IPC is registered under the prefix `"WrathCombo"`. This means:
- Other plugins looking for `WrathCombo` IPC will find ParseLord5.
- This is intentional for backward compatibility.
- Changing this to `"ParseLord5"` would break all existing IPC consumers.

### 5.2 IPC internal references (updated 2026-05-17)

| File | Line | Content | Impact |
|---|---|---|---|
| `Provider.cs` | 84 | `prefix: "WrathCombo"` | IPC is exposed as WrathCombo |
| `Search.cs` | 200 | `ConfigFile.FullName` | Uses authoritative Dalamud config path — no hardcoded filename |
| `Helper.cs` | 390 | `PunishXIV/WrathCombo` | IPC status check from upstream repo |
| `Provider.cs` | 261-262 | `WrathComboCallback` | Callback method name |

### 5.3 docs/IPC.md

- The entire document describes Wrath Combo IPC.
- Example code uses `"WrathCombo"` as IPC prefix and `DalamudReflector.TryGetDalamudPlugin("WrathCombo", ...)` to find the plugin.
- This is correct behavior for external consumers finding ParseLord5's IPC since it is still exposed as `"WrathCombo"`.

### 5.4 Recommendation on IPC

| Action | Recommendation |
|---|---|
| Change IPC prefix now | **No.** Would break all consumers (AutoDuty, etc.). |
| Document the situation | **Yes.** It is documented here. |
| Plan for future alias/cutover | **Yes.** Add to the config migration plan. |
| Change immediately | **No.** Not in this milestone. |

---

## 6. Risk verdict

| Verdict | Applies? | Evidence |
|---|---|---|
| SAFE_NO_FURTHER_CHANGE_NEEDED | **YES** | Config uses InternalName "ParseLord5" → isolated from WrathCombo. Two hardcoded `WrathCombo.json` paths were fixed (Search.cs, RepoCheck.cs). |
| NEEDS_CONFIG_IDENTITY_FIX | NO | No hardcoded "WrathCombo" in config load/save paths |
| NEEDS_MANIFEST_FIX | NO | Manifest already has Name="ParseLord5", InternalName="ParseLord5" |
| NEEDS_PROJECT_FILE_FIX | NO | AssemblyName=ParseLord5, output paths use ParseLord5 |
| NEEDS_IPC_PLAN_ONLY | YES | IPC prefix is "WrathCombo" — needs future plan but not now |

### Residual risks documented (updated 2026-05-17)

1. ~~**Search.cs:210** hardcodes `"WrathCombo.json"` for IPC file watch.~~ **FIXED.** Search.cs now uses `Svc.PluginInterface.ConfigFile.FullName` — the authoritative Dalamud config path. No manual path construction, no hardcoded filename.
2. **Helper.cs:390** fetches IPC status from `PunishXIV/WrathCombo`. ParseLord5's IPC would be remotely disabled by WrathCombo's IPC status file.
3. ~~**PunishLibMain.Init** on line 177 of WrathCombo.cs passes `"Wrath Combo"` as display name.~~ **FIXED.** Now passes `"ParseLord5"`. (MOTD local text and IPC log prefix also rebranded; see `docs/ParseLord5_Remaining_Identity_Policy_20260517.md`.)

---

## 7. Conclusion

**ParseLord5 is already safely isolated from WrathCombo configuration.** No further config-isolation code changes are required in this milestone.

The `InternalName` manifest field is the key mechanism: Dalamud uses it for config file naming (`ParseLord5.json` vs `WrathCombo.json`), and both `GetPluginConfig()` and `SavePluginConfig()` derive from it.

The IPC identity remains as `"WrathCombo"` to preserve backward compatibility with all existing consumers. This should be addressed in a future milestone, not this one.

---

## 8. Advisor follow-up (2026-05-17)

### ConfigFilePath hardened

During the initial hardening pass, `Search.cs` `ConfigFilePath` was changed to hardcode `"ParseLord5.json"` instead of `"WrathCombo.json"`. The advisor flagged this as still brittle — manually deriving the config path from `GetPluginConfigDirectory()` by walking up one directory and hardcoding a filename is fragile and may break if Dalamud changes config directory layout.

**Fix applied:** `ConfigFilePath` now uses `Svc.PluginInterface.ConfigFile.FullName`, the authoritative Dalamud API for the plugin's config file path. This:

- Eliminates all manual path construction logic
- Removes the last runtime `.cs` hardcoded `"WrathCombo.json"` or `"ParseLord5.json"` config-path reference
- Is consistent with existing usage at `WrathCombo.cs:277,279,297`
- Cannot diverge from Dalamud's actual config location

### RepoCheck.cs

`RepoCheck.cs:25` reads `ParseLord5.json` from the assembly directory (not the config directory). This is correct — it reads the shipped manifest to determine install source. The only downstream consumer is `DebugFile.cs:170` (debug output). No change needed.

### Remaining out-of-scope items (updated 2026-05-17)

- IPC prefix `"WrathCombo"` (`Provider.cs:84`) — compat-sensitive; documented in `ParseLord5_Remaining_Identity_Policy_20260517.md`
- IPC status endpoint `PunishXIV/WrathCombo` (`Helper.cs:390`) — upstream dependency
- ~~MOTD local branding~~ **REBRANDED.** Welcome text, MOTD prefix now say ParseLord5. MOTD URL remains upstream dependency.
- ~~PunishLib display name~~ **REBRANDED.** Now `"ParseLord5"`.
- MOTD fetch URL (`WrathCombo.cs:429`) — upstream dependency; documented in identity policy

These are deliberately deferred for compatibility or upstream-dependency reasons. See `docs/ParseLord5_Remaining_Identity_Policy_20260517.md` for full rationale.
