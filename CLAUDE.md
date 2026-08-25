# CLAUDE.md — ParseLord5

FFXIV Dalamud plugin: configurable job combos, one-button rotations, Auto-Rotation. **The current version — ParseLord2/3/4 are superseded, don't reference them.**

The code is still largely named **WrathCombo** upstream. `WrathCombo/` is this plugin, not a vendored dependency.

## Build

```bash
dotnet build WrathCombo/WrathCombo.csproj -c Release
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj
```

Solution: `WrathCombo.slnx`. Sync to devPlugins with `scripts/sync-dev-build.ps1`.

## Layout

| Path | What |
|---|---|
| `WrathCombo/` | Plugin source (main project) |
| `WrathCombo/AutoRotation/AutoRotationController.cs` | Auto-Rotation core — the two-pass probe |
| `WrathCombo/Combos/PvE/<JOB>/` | Per-job combo definitions |
| `WrathCombo/Services/SmartMitigation/` | Smart mit — coverage calculator, telemetry |
| `WrathCombo/Services/TankCooldownHelperIPC/` | Consumes TankCooldownHelper danger data |
| `WrathCombo.Tests/` | Tests incl. structural rotation lints |
| `WrathCombo.API/` | Public IPC surface |
| `ECommons/`, `PunishLib/` | Vendored third-party libs — don't edit |
| `docs/goals/` | Active goal docs and state boards |

Commands: `/pl5`, `/wrath`. Config: `ParseLord5.json` in Dalamud's pluginConfigs.

## Gotchas that have burned real sessions

**Auto-Rotation is a two-pass probe.** `InvokeCombo` sets `IsSelectingAutorotAction = true` while probing the DPS preset and Heal preset *separately*. Non-damage actions sitting at the top of a **DPS** combo fire during the DPS probe and return a heal on a full-HP party. Fix pattern: gate every non-damage return in a DPS combo behind `!AutoRotationController.IsSelectingAutorotAction`.

- **Healers (WHM/SGE/SCH/AST) are already fixed.** WHM is the reference implementation.
- **Do NOT apply the same gating to tanks/DPS (19 jobs).** The DPS lane is their *only* autorot lane — gating it removes all mitigation. Wait for a dedicated utility lane.
- `WrathCombo.Tests/RotationStructureTests.cs` has a structural lint locking this in. Run it before touching healer DPS combos.

**"Healer not healing at all" is usually not the gating.** Check `RotationConfig.HealerRotationMode` first — `Manual` (0) only returns a target when the player's hard target is a friendly hurt ally, so with an enemy targeted the ST heal lane is dead.

**Check for cross-plugin interference before blaming the heal lane.** `[ActionStacksEXIPC]` log lines mean ActionStacksEX is applying buffs that shift heal thresholds.

`/pl5 trace` toggles decision-level logging for **any job** — `[PL5-AUTOROT]` lines cover execute paths, ASEX redirects, DPS-lane blocklist rejections, and heal readiness. Off by default; resets on plugin reload. The decision core lives in `Services/AutorotActionPolicy.cs` (pure, unit-tested in `AutorotActionPolicyTests`).

## More

Deep background, per-job status, and dead-end fixes not to retry: `Second Brain/wiki/ParseLord5.md`.
