---
tags:
  - type/plan
  - project/parselord5
  - status/done
type: plan
project: parselord5
status: done
aliases: []
---
# Battle Plan - WHM trust heal lane

**Origin:** [whm-trust-heal-lane-war-room.md](whm-trust-heal-lane-war-room.md)
**Selected COA:** COA-4 - end-to-end WHM-only instrumentation.

## Objective

Add temporary, throttled WHM-only instrumentation that proves why WHM trust healing is not using the dedicated heal lane, without changing healing behavior or restoring DPS-heal helper calls.

## Proof Before Edit

- The user reports AST trust improved but WHM trust still heals mostly from incidental Afflatus Rapture and Assize.
- WHM ST/AoE heal auto-actions are correctly marked as heal actions in preset metadata.
- `OneButtonHealLogic` should use `AutorotHealTarget` when controller selection succeeds.
- `CanAoEHeal()` excludes `IsOutOfPartyNPC`, making trust AoE counting a plausible but unproven cause.
- Saved config currently has `IncludeNPCs=true`, advanced WHM heal auto-actions enabled, and `RotationConfig.Enabled=false`; the live retest must confirm whether auto-rotation is active during the repro.

## Moves

### Move 1 - Add a WHM trace helper

- **Edit:** add a private WHM-only logging helper in `AutoRotationController.cs` using `Svc.Log.Information` and an `EzThrottler` key.
- **Expected:** every log line starts with `[PL5-WHM-HEAL]` and logs only when `Player.Job is Job.WHM`.
- **Failure cause:** missing namespace or logger mismatch.
- **Counter:** use existing `Svc.Log` and `ECommons.Throttlers.EzThrottler` already imported by the controller.

### Move 2 - Log controller heal decision

- **Edit:** after `needsHeal`, `actCheck`, `lowestHp`, `effectiveHealDelay`, and `canHeal` are computed, log a summary including selected heal target, lowest HP, party count, out-of-party NPC count, AoE low-member count, `needsHeal`, `aoeheal`, `actCheck`, `canHeal`, and `TimeToHeal` age.
- **Expected:** one trust pull can show whether WHM never enters heal mode, is delayed, or lacks an actionable heal.
- **Failure cause:** calculating counts throws on transient party objects.
- **Counter:** catch per-summary count errors and log `-1` for unknown counts rather than affecting behavior.

### Move 3 - Log heal execution boundaries

- **Edit:** in `AutomateHealing`, log early exits for casting and `HealThrottle`, then log ST/AoE preset execution result.
- **Expected:** logs show whether `WHM_STHeals` or `WHM_AoEHeals` were attempted and whether execution succeeded.
- **Failure cause:** logs do not expose combo return action.
- **Counter:** Move 4 logs WHM combo choices.

### Move 4 - Log WHM heal combo returned actions

- **Edit:** add a small local helper in `WHM.cs` that logs `[PL5-WHM-HEAL] combo=<name> reason=<branch> target=<name/id/hp> action=<ActionName/id>` and returns the action unchanged; call it only from WHM ST/AoE heal combo branches.
- **Expected:** logs show whether WHM returns expected Cure/Cure2/Tetra/Rapture/Assize/Medica/etc. while preserving exact behavior.
- **Failure cause:** wrapping many returns increases edit risk.
- **Counter:** instrument final chosen return points in ST and AoE heal combos first; leave simple/manual features untouched if scope grows.

### Move 5 - Build and deploy the instrumentation DLL

- **Command:** `distrobox enter dev-arch -- bash -lc 'cd /home/kruillin/Projects/Projects/ParseLord5 && dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore'`
- **Command:** `distrobox enter dev-arch -- bash -lc 'cd /home/kruillin/Projects/Projects/ParseLord5 && dotnet build WrathCombo/WrathCombo.csproj -c Release'`
- **Deploy target:** `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`
- **Expected:** user can disable/re-enable PL5 and run one WHM trust pull.
- **Failure cause:** deploy writes outside workspace and needs approval.
- **Counter:** request escalation only for the copy/verification step.

## Trigger Forks

- **TRIGGER-CONFIG-OFF:** if logs show no WHM controller loop or saved `RotationConfig.Enabled=false` is still live, stop code debugging and fix the test/config state.
- **TRIGGER-ST-NO-TARGET:** if `needsHeal=false` because no ST target is selected while trust NPCs are low HP, inspect `HealTargets()` filters and line-of-sight/range.
- **TRIGGER-AOE-NPC:** if trust NPCs are present and low but AoE low-member count excludes them below threshold, re-convene for an `IncludeNPCs`-aware AoE count patch.
- **TRIGGER-COMBO-NO-HEAL:** if WHM heal combos return non-heal or fallback actions while targets are low, patch WHM priority/config logic.
- **TRIGGER-EXECUTE-BLOCK:** if combo returns a healing action but `ExecuteST`/`ExecuteAoE` returns false, instrument readiness/range/retarget failure at that exact boundary.

## Abort Conditions

- **ABORT-SCOPE:** stop if instrumentation requires touching AST/SGE/SCH, DPS-heal helpers, or broad retargeting code.
- **ABORT-BUILD:** stop if the instrumentation does not compile cleanly.
- **ABORT-NOISY:** stop if logs cannot be throttled enough for practical Dalamud log review.
- **ABORT-BEHAVIOR:** stop if an edit changes returned action or targeting behavior instead of only logging it.

## Verification

- **V1:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` passes.
- **V2:** `dotnet build WrathCombo/WrathCombo.csproj -c Release` passes.
- **V3:** deployed DLL timestamp and SHA256 are reported after copy.
- **V4:** quality gate is run; environment/doc failures are reported separately from build/test.
- **V5:** live WHM trust retest produces `[PL5-WHM-HEAL]` lines sufficient to choose one trigger fork.

## Manual Verify

- Disable and re-enable PL5 after deployment, or restart the game if hot reload does not pick up the DLL.
- Enable WHM auto-rotation and run one trust pull where party members take visible damage.
- Capture Dalamud log lines containing `[PL5-WHM-HEAL]`.
- Report whether WHM cast dedicated ST/AoE heals, only incidental Rapture/Assize, or no healing.

## Report Skeleton

- **Changed:** instrumentation files only.
- **Build/test/deploy:** pass/fail plus DLL timestamp/hash.
- **Quality gate:** pass/fail reason.
- **Live log outcome:** selected trigger fork.
- **Next implementation:** exact behavior patch or config/test correction.

## Execution Report

- **Changed:** added WHM-only `[PL5-WHM-HEAL]` instrumentation in `WrathCombo/AutoRotation/AutoRotationController.cs` and `WrathCombo/Combos/PvE/WHM/WHM.cs`; created this WHM war-room/battle-plan pair.
- **Behavior:** no healing thresholds, priorities, target selection, or returned actions were intentionally changed; logs wrap existing decisions and returned actions.
- **Tests:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` passed, 45/45.
- **Build:** plain Linux Release build failed because Dalamud/FFXIVClientStructs references were not resolved; `dotnet build WrathCombo/WrathCombo.csproj -c Release -p:DalamudLibPath=/home/kruillin/.xlcore/dalamud/Hooks/15.0.2.2/` passed with 3 existing warnings.
- **Deploy:** copied instrumentation DLL to `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`; timestamp `2026-07-07 12:51:21 -0400`; SHA256 `49cb818b6cb49b8bae6dc88010f52564810faee870887377d936761e6c04e9a8`.
- **Quality gate:** failed, run `20260707T165135Z-34d27c57`; build/domain-eval commands require missing Linux `powershell`, and older unrelated wargame docs are missing metadata. Direct test/build validation passed.
- **Manual next:** disable/re-enable PL5, run one WHM trust pull, and capture Dalamud lines containing `[PL5-WHM-HEAL]`.

## WC Reference Follow-Up

- **User direction:** use working WrathCombo as the WHM reference.
- **Finding:** no `[PL5-WHM-HEAL]` lines were present in the current Dalamud logs, so the previous instrumentation build did not produce a WHM autorotation trace.
- **Reference delta:** live WrathCombo config had `HealerRotationMode=2`, `UseCustomHealStack=true`, `CustomHealStack=[UIMouseOverTarget, ModelMouseOverTarget, HardTarget, LowestHPPAllyIfMissingHP, Self]`, and WHM ST heal thresholds at WC values such as Solace/Tetra/Benison/Temperance/Asylum `100`; live ParseLord5 had `HealerRotationMode=0`, default heal stack, and most WHM ST thresholds at `70`.
- **Changed:** updated `WrathCombo/Core/ConfigurationHelper.cs` so the existing WrathCombo import copies healer target-stack settings and `Custom*ValuesV6` maps, which hold WHM thresholds, priorities, and weave toggles.
- **Live config:** backed up `/home/kruillin/.xlcore/pluginConfigs/ParseLord5.json` to `/home/kruillin/.xlcore/pluginConfigs/ParseLord5.before-wc-whm-reference-20260707-130433.json`, then synced WHM-focused config from `/home/kruillin/.xlcore/pluginConfigs/WrathCombo.json`.
- **Deploy:** copied the rebuilt DLL to `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`; timestamp `2026-07-07 13:01:49 -0400`; SHA256 `4dc82dc47f3dc568c7bc16ae4b3e47324a5cf3afa95f3fd859475325e590bac2`.
- **Tests:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` passed, 45/45.
- **Build:** `dotnet build WrathCombo/WrathCombo.csproj -c Release -p:DalamudLibPath=/home/kruillin/.xlcore/dalamud/Hooks/15.0.2.2/` passed with 3 existing warnings.
- **Manual next:** disable/re-enable PL5 or restart if needed, then run one WHM trust pull using the WC-reference PL5 config.
- **Live result:** user reported WHM now seems to work properly in trusts after the WC-reference config sync.
- **Optimization backlog:** user noted WHM, like AST, could later be tuned to heal more like the user's personal playstyle; this is accepted as future optimization, not a blocker for the current trust-healing fix.
- **Current routing:** treat the observed WHM trust failure as a PL5-vs-WC config/import mismatch unless a new live repro contradicts it; keep the import fix so future WC migration includes the custom WHM thresholds, priorities, and heal stack.
