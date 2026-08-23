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
# Battle Plan - SGE dummy heal spam third attempt

**Origin:** [sge-dummy-heal-spam-third-war-room.md](sge-dummy-heal-spam-third-war-room.md)
**Status:** EXECUTED
**Selected COA:** controller DPS-lane deny guard plus in-combat preemptive shield gate.

## 0. Theatre map

- **Primary file:** `WrathCombo/AutoRotation/AutoRotationController.cs`
- **Regression file:** `WrathCombo.Tests/RotationStructureTests.cs`
- **Live deploy target:** `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`
- **Windows/Wine view:** `Z:\home\kruillin\.xlcore\devPlugins\ParseLord5\ParseLord5.dll`

## 1. Proof before edit

1. Confirm logs are stale relative to live test.
   - Expected: `dalamud.log` mtime is earlier than the deployed DLL.

2. Confirm DPS-lane trust boundary.
   - Inspect `ProcessAutoActions`, `ExecuteST`, and `ExecuteAoE`.
   - Expected: preset filtering uses `AutoAction.IsHeal == canHeal`, but returned `outAct` is not revalidated against that lane.

3. Confirm SGE DPS combos can return non-damage actions.
   - Inspect SGE DPS combo branches.
   - Expected: Kardia, Lucid, Rhizomata, and related utility actions can be returned by a DPS-tagged preset.

4. Confirm preemptive shield can run during dummy combat.
   - Inspect `PreEmptiveShield()`.
   - Expected: it checks `PartyInCombat()` but not `InCombat()`.

## 2. Edit

1. Add a controller helper that rejects SGE defensive/recovery actions when the current auto-action preset is DPS.
   - Block: Kardia, Rhizomata, Soteria, Druochole, Taurochole, Haima, Krasis, Zoe, Pepsis, Kerachole, Ixochole, Holos, Panhaima, Philosophia, Physis, Physis II, Eukrasian Diagnosis, Eukrasian Prognosis.
   - Do not block: Psyche, Phlegma, Toxikon, Pneuma, Dosis, Dyskrasia, Eukrasian Dosis, Eukrasian Dyskrasia, Eukrasia.

2. Call the helper after combo invocation in both DPS `ExecuteST` and DPS `ExecuteAoE`.

3. Add `InCombat()` to the `PreEmptiveShield()` early-return guard.

## 3. Tests

Add source-structure tests:

- `AutorotationDpsLane_BlocksSgeDefensiveActions`
  - Assert helper exists.
  - Assert it contains known blocked actions.
  - Assert both `ExecuteST` and `ExecuteAoE` call it after `InvokeCombo`.

- `PreemptiveShield_DoesNotRunDuringCombat`
  - Assert `PreEmptiveShield()` starts with an `InCombat()` guard.

## 4. Verification

1. Run:

```bash
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore
```

PASS: all tests pass.

2. Build:

```bash
dotnet build WrathCombo/WrathCombo.csproj -c Release -p:DalamudLibPath=/home/kruillin/.xlcore/dalamud/Hooks/15.0.2.2/
```

PASS: release build succeeds with baseline warnings only.

3. Deploy:

Copy the dev-container build output to `/home/kruillin/.xlcore/devPlugins/ParseLord5/`.

PASS: live DLL timestamp/hash changes and contains the new helper string.

4. Quality gate:

```bash
python /home/kruillin/Projects/Projects/quality-gate/gate.py normal --repo /home/kruillin/Projects/Projects/ParseLord5 --task sge-dummy-heal-spam-third
```

PASS if available. If it fails on missing Linux `powershell`, record that separately from xUnit/build proof.

## 5. Trigger forks

- **TRIGGER-1:** If user still sees Eukrasia/Diagnosis only, inspect focus target and preemptive shield alternatives.
- **TRIGGER-2:** If user still sees Kerachole/Physis/Holos/Panhaima, deploy `[DEBUG-SGEHEAL]` instrumentation around `UseAutorotAction` call sites.
- **TRIGGER-3:** If PL5 no longer uses offensive SGE DOT setup, revisit whether Eukrasia needs a context-sensitive allow rule.
- **ABORT-1:** Build fails for code reasons, not environment reasons.
- **ABORT-2:** Structural test shows the guard is not on both ST and AoE DPS paths.

## 6. Report skeleton

- Files changed:
  - `WrathCombo/AutoRotation/AutoRotationController.cs`
  - `WrathCombo.Tests/RotationStructureTests.cs`
  - `wargames/sge-dummy-heal-spam-third-war-room.md`
  - `wargames/sge-dummy-heal-spam-third-battle-plan.md`
- Tests: `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` passed 43/43 inside `dev-arch`.
- Build: `dotnet build WrathCombo/WrathCombo.csproj -c Release -p:DalamudLibPath=/home/kruillin/.xlcore/dalamud/Hooks/15.0.2.2/` succeeded with 3 baseline warnings.
- Deployed DLL: `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`, timestamp `2026-07-07 11:52:43 -0400`, hash `33ba23b1265f3bf3bd208e63505e14063714b6f6a49895d565380594ccdb3fa1`.
- Quality gate: failed as `20260707T155315Z-5ddcb7a3` because repo gate commands require Windows `powershell` in this Linux session and unrelated pre-existing untracked wargame docs are missing metadata.
- Manual test ask: disable/re-enable PL5 again and retest SGE on the dummy. If this still fires healing actions, trigger COA-3 instrumentation with `[DEBUG-SGEHEAL]` source tags.

## 7. Follow-up fork - solo healer target fallback

- **Live trigger:** user reported WHM is fixed, but SGE still blows healing cooldowns while fighting a target dummy.
- **Finding:** the earlier DPS-lane and raidwide protections were still present; the remaining path was the heal lane itself. `HealerTargeting.GetHighestCurrent()` and `GetLowestCurrent()` returned `Player.Object` whenever no party members existed, even if the player was at full HP. That made solo dummy combat eligible for SGE heal auto-actions, whose heal presets can return support/cooldown actions.
- **Changed:** solo healer fallback now returns the player only when the player is below the configured single-target heal threshold, including shield/regen/excog threshold handling.
- **Regression:** added `SoloHealerTargeting_RequiresPlayerBelowHealThreshold` to `WrathCombo.Tests/RotationStructureTests.cs`.
- **Red check:** focused test failed before the fix because the unconditional `return Player.Object` fallback was present.
- **Tests:** focused regression passed after the fix; full `WrathCombo.Tests` passed 46/46.
- **Build:** release build passed with the existing three warnings.
- **Deploy:** copied rebuilt DLL to `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`; timestamp `2026-07-07 13:52:04 -0400`; SHA256 `0ae28401a8265a1daa272991b706c8338805a108c47aea35f6d857da1839fe17`.
- **Quality gate:** `PASS_WITH_WARNINGS`, run `20260707T175132Z-ad77635a`; build, domain tests, and markdown audit passed. Warnings were the existing test-hack detector notices for changed test/gate-critical files.
- **Manual next:** disable/re-enable PL5, then retest SGE on a solo target dummy at full HP. Expected result: SGE should stay in the DPS lane and stop spending heal cooldowns unless the player actually drops below the configured heal threshold.
