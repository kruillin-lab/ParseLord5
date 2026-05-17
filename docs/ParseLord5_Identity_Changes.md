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
# ParseLord5 Identity Changes

## Files Changed

- `WrathCombo/WrathCombo.csproj`
- `WrathCombo/WrathCombo.json` renamed to `WrathCombo/ParseLord5.json`

No source namespaces, folders, rotations, action execution paths, hooks, detours, `SendAction`, `UseAction`, or auto-rotation behavior were changed.

## Identity Fields Changed

`WrathCombo/WrathCombo.csproj`:

- `Description`: `XIVCombo for very lazy players` -> `ParseLord5 is a WrathCombo-based fork and iteration.`
- `PackageId`: `WrathCombo` -> `ParseLord5`
- `Product`: `WrathCombo` -> `ParseLord5`
- `AssemblyName`: `WrathCombo` -> `ParseLord5`
- `DalamudDevPlugins`: `%APPDATA%\XIVLauncher\devPlugins\WrathCombo\` -> `%APPDATA%\XIVLauncher\devPlugins\ParseLord5\`
- `DalamudPluginPath`: `%APPDATA%\XIVLauncher\installedPlugins\WrathCombo\$(Version)` -> `%APPDATA%\XIVLauncher\installedPlugins\ParseLord5\$(Version)`
- Release manifest item: `WrathCombo.json` -> `ParseLord5.json`

`WrathCombo/ParseLord5.json`:

- `Name`: `Wrath Combo` -> `ParseLord5`
- `InternalName`: `WrathCombo` -> `ParseLord5`
- `Punchline`: changed to `WrathCombo-based fork and iteration.`
- `Description`: changed to `ParseLord5 is a WrathCombo-based fork and iteration that preserves the original architecture for future experimentation.`

Preserved:

- `Author`: `Team Wrath`
- `RepoUrl`: `https://github.com/PunishXIV/WrathCombo`
- `IconUrl`: upstream WrathCombo icon
- License and attribution files

## Commands

- `/wrath` was preserved.
- `/scombo` legacy alias was preserved.
- `/pl5` was not added.

## Build Result

- Command: `dotnet build .\WrathCombo.slnx`
- Final identity build result: success
- Output: `%APPDATA%\XIVLauncher\devPlugins\ParseLord5\ParseLord5.dll`
- Errors: `0`
- Warnings: `8`

Warnings observed were the same baseline warnings:

- `CS8618` in `WrathCombo/Attributes/SettingAttributes.cs` for `Name`, `HelpMark`, `RecommendedValue`, `DefaultValue`
- `CS0219` in `WrathCombo/AutoRotation/AutoRotationController.cs` for `raidwideWarningFound`
- `CS0219` in `WrathCombo/Services/IPC/Helper.cs` for `lower`
- `CS8618` in `WrathCombo/Window/Functions/Setting.cs` for `CategoryName`
- `CS0649` in `WrathCombo/AutoRotation/AutoRotationController.cs` for `UnpauseSeconds`

## Identity Build Correction

After `AssemblyName` changed to `ParseLord5`, `DalamudPackager` failed because it expected a manifest matching the assembly name.

Exact failure:

```text
Plugin name is required in your manifest.
Author name is required in your plugin manifest.
Punchline is required in your plugin manifest.
```

Correction: rename `WrathCombo/WrathCombo.json` to `WrathCombo/ParseLord5.json` and update the csproj manifest item. This is a metadata/build identity change, not behavior work.

## Risks From Temporary WrathCombo Namespaces

- Namespace and type names still identify as `WrathCombo`.
- Some UI/localization strings still say Wrath or mention `/wrath`.
- Existing config type names and migration markers still reference WrathCombo.
- `/wrath` command collision remains possible if regular WrathCombo is loaded at the same time.
- Future broad rename should be planned separately after architecture map and feature flag work.
