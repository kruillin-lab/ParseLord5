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
# ParseLord5 Runtime Smoke And Collision Audit — 2026-05-17

## 1. Purpose / Scope

This is a **source-backed runtime readiness document**. No live Dalamud or
FFXIV smoke test was performed for this milestone.

**Audience:** A future tester who has real Dalamud + FFXIV access and can load
ParseLord5 in-game.

**Milestone is doc-only.** The goal is to define what must be checked at
runtime and to document every collision surface that still carries WrathCombo
ancestry, so a live tester can work through it without ambiguity.

Completed prior milestones that this document reflects:

1. `/pl5` command alias added while preserving `/wrath` and `/scombo`.
2. Plugin identity and config isolation hardened:
   - `InternalName = ParseLord5` in manifest
   - config persistence uses Dalamud-native `GetPluginConfig()` / `SavePluginConfig()`
   - `Search.cs` uses `Svc.PluginInterface.ConfigFile.FullName`
   - no runtime `.cs` references to a hard-coded `WrathCombo.json` path
3. Minimal visible branding shell completed:
   - main UI title and About name now resolve to `ParseLord5`
   - DTR label and opener label now `ParseLord5` / `ParseLord5 Opener`
   - major changes window and ImGui IDs updated
   - IPC / API / MOTD / PunishLib / namespaces deliberately deferred

---

## 2. Manual Smoke-Test Checklist

Run in this order when a live tester is available.

1. Load **only** ParseLord5 (no WrathCombo).
2. Open Dalamud plugin installer list. Confirm plugin appears as **ParseLord5**.
3. Run `/pl5`. Confirm main config UI opens.
4. Run `/wrath`. Confirm same config UI opens.
5. Run `/scombo`. Confirm same config UI opens.
6. Confirm main window title bar says **ParseLord5 \<version\>**.
7. Open the **About** tab / section. Confirm header says **ParseLord5**.
8. If the major-changes popup appears, confirm its title bar says **ParseLord5 | New Changes**.
9. In the DTR (Server Info Bar), confirm the label reads **ParseLord5**.
10. If the DTR opener entry is visible, confirm it reads **ParseLord5 Opener**.
11. Hover the DTR entry. Confirm tooltip reads:
    ```
    Click to toggle ParseLord5's Auto-Rotation.
    Disable this icon in /xlsettings -> Server Info Bar
    ```
12. Toggle one harmless UI setting (e.g., toggle Short DTR Text on/off).
13. Close the plugin UI. Re-open with `/pl5`. Confirm the setting persisted.
14. Locate the Dalamud plugin config directory on disk
    (`%APPDATA%\XIVLauncher\pluginConfigs\` or equivalent). Confirm ParseLord5
    writes to **`ParseLord5.json`** (Dalamud derives filename from `InternalName`).
15. Confirm **`WrathCombo.json`** is **not** created or modified by ParseLord5
    during the same test session.
16. If WrathCombo has any prior saved config on disk, confirm it is untouched.
17. Confirm no gameplay or action-replacement behavior is observably changed
    during basic UI smoke (no combat testing required).
18. If safe: unload ParseLord5, then load both ParseLord5 **and** WrathCombo
    side-by-side after a clean restart.
19. Record command collision behavior:
    - `/wrath` — expected direct collision (both plugins register this)
    - `/scombo` — expected direct collision (both plugins register this)
    - `/pl5` — expected ParseLord5-only
20. Record whether duplicate command registration causes:
    - only one plugin to respond
    - last-loaded plugin wins
    - error or log spam
    - nondeterministic behavior
21. Repeat harmless setting toggle in each plugin while both are loaded.
22. Confirm no config cross-write: ParseLord5 config does not appear in
    WrathCombo's file and vice versa.
23. Capture screenshots for:
    - plugin listing in Dalamud
    - config window title
    - About tab header
    - major changes popup (if shown)
    - DTR label and tooltip
24. Capture Dalamud log snippets for:
    - command registration lines
    - config save path
    - IPC or plugin-load warnings
    - any PunishLib init line mentioning "Wrath Combo"

---

## 3. Side-by-Side Collision Matrix

| Surface | ParseLord5 current state | WrathCombo collision risk | Recommendation |
| --- | --- | --- | --- |
| Dalamud `InternalName` | `ParseLord5` (`ParseLord5.json` line 4) | **Low** | Good isolation. Keep. |
| Config file path | `Svc.PluginInterface.ConfigFile.FullName` — Dalamud resolves from `InternalName`; expected filename: `ParseLord5.json` | Low to medium until live-verified | Verify on disk. Confirm `WrathCombo.json` is untouched. |
| Assembly / output DLL name | `AssemblyName = ParseLord5` (`WrathCombo.csproj` line 19) | **Low** | Good isolation. Keep. |
| Dev plugin path | `devPlugins\ParseLord5\` (`WrathCombo.csproj` line 41) | **Low** | Good isolation. Keep. |
| `/pl5` | Registered in `Commands.cs` line 74: `EzCmd.Add(ParseLord5Command, OnCommand)` | **None** vs upstream WrathCombo | Use for ParseLord5-specific testing. |
| `/wrath` | Still registered (`Commands.cs` line 67) | **High** | Expected direct command collision if both loaded. Test load order. |
| `/scombo` | Still registered (`Commands.cs` line 73) | **High** | Expected direct command collision if both loaded. Test load order. |
| DTR labels | `Svc.DtrBar.Get("ParseLord5")` and `Svc.DtrBar.Get("ParseLord5 Opener")` (`WrathCombo.cs` lines 226, 235) | **Low** | Good visible separation. Verify live. |
| ImGui window IDs | Main window: `###ParseLord5` (`ConfigWindow.cs` line 118); TargetHelper: `###ParseLord5TargetHelper` (`TargetHelper.cs` line 16); left/right child panels still use `###WrathLeftSide`, `###WrathRightSide` (`ConfigWindow.cs` lines 190, 267) | Low to medium | Visible shell acceptable. Internal hidden IDs can change later if needed. |
| IPC prefix | `EzIPC.Init(output, prefix: "WrathCombo")` (`Provider.cs` line 84) | **High** | Do not change casually. Shared prefix risks colliding with WrathCombo IPC subscribers/providers. |
| IPC status endpoint | `https://raw.githubusercontent.com/PunishXIV/WrathCombo/main/res/ipc_status.txt` (`Helper.cs` line 390) | Medium | Leave for now. Treat as deferred identity item. |
| `WrathCombo.API` | Still referenced as project dependency (`WrathCombo.csproj` line 82) | Medium to high | Leave for now. Coordinate before any rename/fork. |
| MOTD URL / text | `https://raw.githubusercontent.com/PunishXIV/WrathCombo/main/res/motd.txt`; MOTD prefix `"[Wrath Message of the Day] "` (`WrathCombo.cs` lines 68, 429) | Medium | Defer until ParseLord5 owns runtime messaging plan. |
| PunishLib init display string | `PunishLibMain.Init(pluginInterface, "Wrath Combo")` (`WrathCombo.cs` line 177) | Medium | Defer pending live validation and coordination. |
| Namespaces / classes | `namespace WrathCombo;`, class `WrathCombo`, all `WrathCombo.*` namespaces | Medium to high | Intentional defer. Do not mass-rename before stronger reason. |
| Manifest `RepoUrl` / `IconUrl` / tags | `"RepoUrl": "https://github.com/PunishXIV/WrathCombo"`, `"IconUrl"` pointing to upstream S3 asset, tags include `"wrath"` / `"wrathcombo"` (`ParseLord5.json` lines 8–19) | Medium | Defer until ParseLord5 distribution surface is ready. |

---

## 4. Remaining WrathCombo Identity Pre-Audit

| Item | Current state | Classification | Why |
| --- | --- | --- | --- |
| MOTD visible text | `"Welcome to WrathCombo v{version}!"` (`WrathCombo.cs` line 426) | safe to change later | Visible shell only. No external contract surface. |
| MOTD URL | `https://raw.githubusercontent.com/PunishXIV/WrathCombo/main/res/motd.txt` (`WrathCombo.cs` line 429) | do not change without coordination | Needs a ParseLord5-owned endpoint and content plan. Runtime remote dependency. |
| `[Wrath Message of the Day]` prefix | `new TextPayload("[Wrath Message of the Day] ")` (`WrathCombo.cs` line 68) | safe to change later | Visible chat text shell. No external contract. |
| PunishLib init string | `PunishLibMain.Init(pluginInterface, "Wrath Combo")` (`WrathCombo.cs` line 177) | defer | Likely log or display integration. Runtime effect not yet live-tested. |
| IPC prefix `"WrathCombo"` | `EzIPC.Init(output, prefix: "WrathCombo")` (`Provider.cs` line 84) | do not change without coordination | Breaking external IPC contract risk for all WrathCombo IPC subscribers. |
| IPC status endpoint URL | `https://raw.githubusercontent.com/PunishXIV/WrathCombo/main/res/ipc_status.txt` (`Helper.cs` line 390) | do not change without coordination | Remote kill-switch semantics. |
| `WrathCombo.API` | Dependency project reference and namespace surface preserved (`WrathCombo.csproj` line 82) | do not change without coordination | Breaking downstream API contract risk. |
| C# namespaces / classes | `namespace WrathCombo;`, class `WrathCombo : IDalamudPlugin`, all child namespaces | do not change without coordination | Large churn. High integration risk. Requires full test pass. |
| Upstream repo references | Manifest `RepoUrl`, project `PackageProjectUrl` both point to `https://github.com/PunishXIV/WrathCombo` | defer | Good candidate after ParseLord5 distribution path exists. |
| Icon / image paths | `images\wrathcombo.png` linked in `.csproj` (`WrathCombo.csproj` line 69); fallback to manifest `IconUrl` pointing to upstream S3 asset | defer | Needs asset replacement plan. |
| Docs / comments | Mixed WrathCombo / ParseLord5 references remain in file headers and XML docs | safe to change later | No runtime contract. Cleanup can be incremental. |
| `[Wrath IPC]` log prefix | `private const string Prefix = "[Wrath IPC] "` (`Helper.cs` line 455) | safe to change later | Log/diagnostic only. No external contract. |
| `WrathComboCallback` IPC callback name | `Action<int, string> WrathComboCallback` (`Helper.cs` line 506) | do not change without coordination | External subscribers reference this name for IPC callbacks. |

---

## 5. Source Evidence

| File | Evidence | What it proves |
| --- | --- | --- |
| `WrathCombo/ParseLord5.json` | `"Name": "ParseLord5"`, `"InternalName": "ParseLord5"`, `"DalamudApiLevel": 15`; `"RepoUrl"` / `"IconUrl"` / tags still point upstream; `"Author": "Team Wrath"` | ParseLord5 manifest identity exists. Dalamud will isolate config to `ParseLord5.json`. Metadata still partly upstream-oriented. |
| `WrathCombo/WrathCombo.csproj` | `<AssemblyName>ParseLord5</AssemblyName>` (line 19); `<PackageId>ParseLord5</PackageId>` (line 13); `<Product>ParseLord5</Product>` (line 14); `<DalamudDevPlugins>…devPlugins\ParseLord5\</DalamudDevPlugins>` (line 41); `<None Update="ParseLord5.json">` (line 429) | Build, product, and dev-path isolation present. Output DLL is `ParseLord5.dll`. |
| `WrathCombo/WrathCombo.cs` | `PunishLibMain.Init(pluginInterface, "Wrath Combo")` (line 177); `pluginInterface.GetPluginConfig()` (line 183); `Svc.DtrBar.Get("ParseLord5")` (line 226); tooltip `"Click to toggle ParseLord5's Auto-Rotation.\n…"` (line 232); `Svc.DtrBar.Get("ParseLord5 Opener")` (line 235); MOTD prefix `"[Wrath Message of the Day] "` (line 68); MOTD URL upstream (line 429); `public string Name => MainWindowUI.Wrath_Combo` (line 454) | Config persistence now Dalamud-native. Visible DTR shell branded ParseLord5. Some runtime identity (PunishLib, MOTD) still Wrath-branded. |
| `WrathCombo/Commands.cs` | `private const string Command = "/wrath"` (line 32); `private const string OldCommand = "/scombo"` (line 33); `private const string ParseLord5Command = "/pl5"` (line 34); all three registered to the same `OnCommand` handler (lines 67–74) | `/pl5` alias exists now. `/wrath` and `/scombo` still registered and will collide if WrathCombo is loaded simultaneously. |
| `WrathCombo/Services/IPC/Search.cs` | `internal string ConfigFilePath => Svc.PluginInterface.ConfigFile.FullName` (line 200); comment `/// The path to the configuration file for ParseLord5.` (line 198) | IPC and config cache reads use the active Dalamud config file path, not a hard-coded `WrathCombo.json`. |
| `WrathCombo/Core/ConfigurationHelper.cs` | `Svc.PluginInterface.SavePluginConfig(config)` (lines 64, 94) | Config saves use Dalamud-native save path. No hard-coded filename. |
| `WrathCombo/Data/RepoCheck.cs` | `var manifest = Path.Join(f.DirectoryName, "ParseLord5.json")` (line 25); `IsFromPunishRepo()` still checks for `https://love.puni.sh/ment.json` (line 50) | Runtime manifest lookup updated to `ParseLord5.json`. Upstream Punish repo check logic remains. |
| `WrathCombo/Services/IPC/Provider.cs` | `EzIPC.Init(output, prefix: "WrathCombo")` (line 84) | IPC prefix still `WrathCombo`. Side-by-side IPC collision risk with a live WrathCombo instance still present. |
| `WrathCombo/Window/ConfigWindow.cs` | Constructor: `base($"{P.Name} {P.GetType().Assembly.GetName().Version}###ParseLord5")` (line 118); child IDs `###WrathLeftSide` (line 190), `###WrathRightSide` (line 267); log string `"Using Local WrathCombo Icon"` (line 205); fallback log `"Using Remote WrathCombo Icon"` (line 213); `PunishGui.AboutTab.Draw(P.Name)` (line 285) | Main config window base id isolated to `###ParseLord5`. Title dynamically uses `P.Name` which resolves to `ParseLord5` via localization. Hidden child ImGui IDs and log strings still contain "Wrath". About tab uses `P.Name` = `ParseLord5`. |
| `WrathCombo/Window/MajorChangesWindow.cs` | Constructor: `base("ParseLord5 | New Changes")` (line 29); `PadOutMinimumWidthFor("ParseLord5 | New Changes")` (line 54) | Major changes popup title branded ParseLord5. |
| `WrathCombo/Window/TargetHelper.cs` | Constructor: `base("###ParseLord5TargetHelper", …)` (line 16) | Target-helper ImGui window ID isolated to ParseLord5. |
| `WrathCombo/Resources/Localization/UI/MainWindow/MainWindowUI.resx` | `<data name="Wrath_Combo" xml:space="preserve"><value>ParseLord5</value>` (lines 178–180) | Main localized plugin-name string resolves to `ParseLord5`. This is what `P.Name` returns via `MainWindowUI.Wrath_Combo`. |
| `WrathCombo/Services/IPC/Helper.cs` | `private const string IPCStatusEndpoint = "https://raw.githubusercontent.com/PunishXIV/WrathCombo/main/res/ipc_status.txt"` (line 390); `private const string Prefix = "[Wrath IPC] "` (line 455); `Action<int, string> WrathComboCallback` (line 506) | IPC remote status control still upstream. IPC log prefix and callback name still Wrath-branded. |

---

## 6. Next Recommended Live-Test Procedure

When a live tester is available:

1. Start with **only ParseLord5 loaded** (no WrathCombo).
2. Run the full smoke checklist in section 2 above.
3. Inspect on-disk config directory. Verify `ParseLord5.json` is created and
   `WrathCombo.json` is not touched.
4. Capture screenshots (plugin listing, window title, About tab, DTR, popup).
5. Capture Dalamud log snippets (command registration, config save, PunishLib init).
6. If ParseLord5 is stable: restart, then load **both ParseLord5 and WrathCombo**
   simultaneously.
7. Record `/wrath` and `/scombo` collision behavior (which plugin responds,
   any log errors).
8. Record `/pl5` behavior (should go to ParseLord5 only).
9. Record whether IPC behaves safely with the shared `WrathCombo` prefix —
   note any log warnings or unexpected cross-plugin control.
10. Confirm no config cross-write in either direction.
11. **Stop if command or IPC collisions create unsafe or unrecoverable behavior.**
    Document the failure mode and cease side-by-side testing.
12. Attach all screenshots and log snippets to the next session handoff.

---

## 7. Explicit Non-Goals

This milestone does **not**:

- rename namespaces or classes
- change the IPC prefix (`WrathCombo`)
- change or fork `WrathCombo.API`
- change action replacement behavior
- change auto-rotation logic
- change job combo logic
- change config migration or import behavior
- remove `/wrath` or `/scombo` command aliases

---

## Verification (run after creating this document)

```
git diff --check   → no whitespace errors found
git status --short → doc is untracked (??) — no source code modified
```

`git diff --check` output: only CRLF normalization warnings on pre-existing
modified files; no whitespace errors.

No markdown audit command was available in this repo. Standard `git diff --check`
and `git status --short` were run instead.

**No source-code refactors were performed. No commits were made.**
```
```

---

## Summary

| Item | Result |
| --- | --- |
| File created | `docs/ParseLord5_Runtime_Smoke_And_Collision_Audit_20260517.md` |
| Sections included | 7 (Purpose, Checklist, Collision Matrix, Identity Pre-Audit, Source Evidence, Live-Test Procedure, Non-Goals) |
| Git verification | `git diff --check` clean; `git status --short` shows no source code modified |
| Unresolved risks | IPC prefix still `WrathCombo`; `/wrath`+`/scombo` command collision if both plugins loaded; PunishLib init string and MOTD still upstream-branded; namespace churn deferred |
| Recommended next step | Find a live tester with Dalamud access; run the smoke checklist; attach screenshots and log snippets before any further identity changes |
