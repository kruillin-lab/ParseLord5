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
# ParseLord5 WrathCombo Fork Audit

## Intake

- Upstream repo used: <https://github.com/PunishXIV/WrathCombo>
- Local path: `C:\Users\kruil\Documents\Projects\ParseLord5`
- Repo intake result: current folder was empty, so upstream was cloned recursively into this folder.
- Branch: `parselord5-wc-base`
- Upstream commit: `21c3e7b4a3baa571c89f165fe406f801dd34bdcb`
- Remote: `origin https://github.com/PunishXIV/WrathCombo`

## Detected Baseline

- Solution file: `WrathCombo.slnx`
- Main plugin project: `WrathCombo/WrathCombo.csproj`
- Plugin manifest before identity changes: `WrathCombo/WrathCombo.json`
- Target framework: `net10.0-windows`
- Dalamud API level: `15`
- Original dev plugin output path: `%APPDATA%\XIVLauncher\devPlugins\WrathCombo\`
- Original installed plugin path: `%APPDATA%\XIVLauncher\installedPlugins\WrathCombo\$(Version)`
- Original plugin identity:
  - Author: `Team Wrath`
  - Name: `Wrath Combo`
  - InternalName: `WrathCombo`
  - AssemblyName: `WrathCombo`
  - PackageId/Product: `WrathCombo`

## Submodules And Dependencies

Live gitlinks from `git submodule status --recursive`:

- `ECommons`: `9b70aed7b9773af922c87ef48bf9d85354e6bfd6`
- `PunishLib`: `f8b3e807c02f02f0f05fae3cdf7d92bc9d0516b5`
- `WrathCombo.API`: `db0f2978862e0c6a4672fb8f16f026e72f37f5b1`

`.gitmodules` also declares `FFXIVClientStructs` and `lib/FFXIVClientStructs`, but those paths are not tracked gitlinks at the audited commit.

Main build dependencies:

- NuGet: `DalamudPackager` `14.0.1`, `System.Speech` `10.0.5`
- Project references: `ECommons`, `PunishLib`, `WrathCombo.API`
- Local Dalamud dev libraries from `%APPDATA%\XIVLauncher\addon\Hooks\dev\`: `Dalamud`, `FFXIVClientStructs`, `Newtonsoft.Json`, ImGui/ImPlot/ImGuizmo bindings, `Lumina`, `Lumina.Excel`, `InteropGenerator.Runtime`

## Architecture Locations

- PvE job combo logic: `WrathCombo/Combos/PvE/*`
- PvP combo logic: `WrathCombo/Combos/PvP/*`
- Combo preset registry: `WrathCombo/Combos/CustomComboPreset.cs`
- Action replacement and hook layer: `WrathCombo/Core/ActionReplacer.cs`
- Retargeting layer: `WrathCombo/Core/ActionRetargeting.cs`
- Settings/configuration: `WrathCombo/Core/Configuration.cs`, `WrathCombo/Core/ConfigurationHelper.cs`, `WrathCombo/Core/ConfigurationChanges.cs`
- Auto-rotation logic: `WrathCombo/AutoRotation/AutoRotationController.cs`
- Auto-rotation config: `WrathCombo/AutoRotation/AutoRotationConfig.cs`, `WrathCombo/AutoRotation/AutoRotationConfigIPCWrapper.cs`
- UI/settings windows: `WrathCombo/Window/*`, especially `WrathCombo/Window/Tabs/*`

## Commands

- Main command: `/wrath` in `WrathCombo/Commands.cs`
- Legacy alias: `/scombo`
- Auto-rotation command entry: `/wrath auto`
- Auto target mode command: `/wrath auto target <damage|healer> <mode>`
- No `/pl5` command existed in the baseline.

## Baseline Build Result

- Command: `dotnet build .\WrathCombo.slnx`
- Result before identity changes: success
- Errors: `0`
- Warnings: `8`
- Baseline output: `%APPDATA%\XIVLauncher\devPlugins\WrathCombo\WrathCombo.dll`

Warnings observed:

- `CS8618` in `WrathCombo/Attributes/SettingAttributes.cs` for `Name`, `HelpMark`, `RecommendedValue`, `DefaultValue`
- `CS0219` in `WrathCombo/AutoRotation/AutoRotationController.cs` for `raidwideWarningFound`
- `CS0219` in `WrathCombo/Services/IPC/Helper.cs` for `lower`
- `CS8618` in `WrathCombo/Window/Functions/Setting.cs` for `CategoryName`
- `CS0649` in `WrathCombo/AutoRotation/AutoRotationController.cs` for `UnpauseSeconds`

## Known Risks Before Further Changes

- Build depends on local XIVLauncher Dalamud dev libraries existing at `%APPDATA%\XIVLauncher\addon\Hooks\dev\`.
- `.gitmodules` contains stale-looking `FFXIVClientStructs` entries that are not live gitlinks.
- Keeping `/wrath` avoids behavior churn, but conflicts with regular WrathCombo if both plugins register the same command.
- Keeping namespaces as `WrathCombo` avoids churn, but logs, config type names, and stack traces will still reference WrathCombo.
- Repo URL and icon still point at upstream WrathCombo metadata until a ParseLord5 remote/icon exists.
- Future work must avoid touching action execution, hooks, auto-rotation behavior, or job rotations without a separate feature-flagged plan.
