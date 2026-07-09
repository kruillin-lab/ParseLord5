---
tags:
  - type/plan
  - project/parselord5
  - status/active
type: plan
project: parselord5
status: active
aliases: []
---
# Battle Plan - Healer upstream baseline

## Objective

Restore the upstream WrathCombo healer DPS-button contract in ParseLord5 without overwriting unrelated fork work: AST/WHM DPS combos must not call fork-only healer-priority helpers, and WHM offensive oGCDs must not be suppressed by the defensive selection flag.

## Proof Before Edit

- `origin/main` fetched from `PunishXIV/WrathCombo`; latest fetched commit is `efe5d828bdc29ee62e4ca76a056ae4935a486a77`.
- Upstream has no `TryDpsSingleTargetHealPriority` or `TryDpsAoEHealPriority`.
- ParseLord5 calls those helpers in AST and WHM DPS combo paths.
- WHM offensive oGCD weave blocks are gated by `!AutoRotationController.IsSelectingAutorotAction`.

## Moves

### Move 1 - Remove fork-only DPS-heal calls from AST DPS buttons

- **Edit:** remove `TryDpsSingleTargetHealPriority` / `TryDpsAoEHealPriority` early returns from simple and advanced AST DPS combos.
- **Expected:** AST DPS combo no longer steals the healer lane.
- **Failure cause:** compile errors if a local variable becomes unused.
- **Counter:** remove now-unused `replacedActions` declarations only when they exist solely for the deleted call.

### Move 2 - Remove fork-only DPS-heal calls from WHM DPS buttons

- **Edit:** remove `TryDpsSingleTargetHealPriority` / `TryDpsAoEHealPriority` early returns from WHM simple and advanced DPS combos.
- **Expected:** WHM DPS combo no longer returns Medica/Cure/Tetra from DPS selection.
- **Failure cause:** helper methods remain unused but harmless.
- **Counter:** leave helpers for cleanup unless warnings become errors.

### Move 3 - Restore WHM offensive oGCD availability

- **Edit:** remove `!AutoRotationController.IsSelectingAutorotAction` from WHM offensive weave blocks for Assize, Presence of Mind, and Lucid Dreaming.
- **Expected:** WHM can weave DPS/support oGCDs in auto-rotation while defensive lily/raidwide guards remain.
- **Failure cause:** a heal branch also becomes ungated.
- **Counter:** keep guards on lily-overcap, raidwide, and Swiftcast-Holy defensive/special branches.

### Move 4 - Add structural regression coverage

- **Edit:** add tests that AST/WHM DPS combo source no longer calls DPS-heal priority helpers and WHM offensive weave guards are not flag-gated.
- **Expected:** tests fail if future patches reintroduce the fork-only leak.
- **Failure cause:** source-structure tests are brittle to formatting.
- **Counter:** assert on focused substrings around class bodies, not entire files.

## Trigger Forks

- **TRIGGER-AST-TRUST:** if AST still fails in a trust party, add tagged instrumentation around `GetPartyMembers`, `HealTargets`, and `CanAoEHeal`.
- **TRIGGER-SGE-SPAM:** if SGE still spams heals, instrument SGE combo branch output and convert it to explicit offensive priority rather than relying on the selection flag.
- **TRIGGER-WHM-MEDICA:** if WHM still casts Medica III with no damage, instrument `needsHeal`, `aoeheal`, `actCheck`, and `GetPartyAvgHPPercent`.

## Abort Conditions

- **ABORT-BUILD:** stop if the project fails to compile from these focused edits.
- **ABORT-SCOPE:** stop if applying upstream directly would overwrite unrelated dirty user work.
- **ABORT-TEST:** stop if structural tests cannot distinguish AST/WHM DPS buttons from heal buttons.

## Verification

- **V1:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` passes.
- **V2:** `dotnet build WrathCombo/WrathCombo.csproj -c Release -p:DalamudLibPath=/home/kruillin/.xlcore/dalamud/Hooks/15.0.2.2/` passes.
- **V3:** deployed DLL at `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll` has a fresh timestamp and hash.
- **V4:** quality gate runs; any unrelated environment/doc failures are reported explicitly.

## Manual Verify

- Disable and re-enable PL5 in Dalamud after deployment.
- Retest WHM on dummy for Assize/Presence of Mind/Lucid behavior.
- Retest SGE on dummy for heal spam and offensive oGCD behavior.
- Retest AST in trust party for actual healing response.

## Report Skeleton

- **Changed:** files and behavior.
- **Upstream baseline:** fetched commit.
- **Tests/build/deploy:** pass/fail plus DLL hash.
- **Quality gate:** pass/fail reason.
- **Manual next:** exact retest cases.

## Execution Report

- **Changed:** `WrathCombo/Combos/PvE/AST/AST.cs`, `WrathCombo/Combos/PvE/WHM/WHM.cs`, and `WrathCombo.Tests/RotationStructureTests.cs`.
- **Behavior:** AST/WHM DPS combos no longer call fork-only DPS-heal priority helpers; WHM offensive Assize/Presence/Lucid weave blocks are no longer suppressed by `IsSelectingAutorotAction`.
- **Upstream baseline:** `PunishXIV/WrathCombo` `FETCH_HEAD` `efe5d828bdc29ee62e4ca76a056ae4935a486a77` (`1.0.4.13`, 2026-06-29).
- **Tests:** `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` passed, 45/45.
- **Build:** `dotnet build WrathCombo/WrathCombo.csproj -c Release -p:DalamudLibPath=/home/kruillin/.xlcore/dalamud/Hooks/15.0.2.2/` passed with 3 existing warnings.
- **Deploy:** copied to `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`; timestamp `2026-07-07 12:12:16 -0400`; SHA256 `768c25f2a22cb0b318fcf909c89aa865ffbcde23696a5af3b67be31bd2a000f2`.
- **Quality gate:** failed for missing `powershell` in configured Windows commands and unrelated older markdown metadata issues; direct Linux test/build verification passed.
