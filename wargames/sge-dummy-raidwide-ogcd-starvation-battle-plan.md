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
# Battle Plan - SGE dummy raidwide oGCD starvation

**Origin:** [sge-dummy-raidwide-ogcd-starvation-war-room.md](sge-dummy-raidwide-ogcd-starvation-war-room.md)
**Status:** EXECUTED
**Selected COA:** context-gate the global healer raidwide responder.

This plan is the replayable route for the Sage dummy symptom: offensive oGCDs are available, but the plugin spends weave windows on healer raidwide tools while fighting a dummy.

## 0. Theatre map

- **Repo:** `/home/kruillin/Projects/Projects/ParseLord5`
- **Primary file:** `WrathCombo/AutoRotation/AutoRotationController.cs`
- **Guard test:** `WrathCombo.Tests/RotationStructureTests.cs`
- **Related but not primary:** `WrathCombo/Combos/PvE/SGE/SGE.cs`
- **User config pressure:** `MaximumWeavesPerWindow = 1`

## 1. Proof before edit

1. Confirm SGE local priorities are not the only active path.
   - Inspect `SGE.cs` for Psyche before Soteria and restored boss gates.
   - Expected: first-layer SGE fixes already exist.

2. Confirm global raidwide handling can bypass SGE local ordering.
   - Inspect `AutoRotationController.Execute()`.
   - Expected pre-fix shape: `if (isHealer && GroupDamageIncoming(out var multi)) HandleRaidwide(multi);`

3. Confirm `HandleRaidwide` includes SGE healing tools.
   - Inspect `RaidwideActions`.
   - Expected: SGE list contains Panhaima, Kerachole, Physis, Physis II, Holos, Eukrasia, and Eukrasian Prognosis.

4. Confirm solo dummy can look unsafe to the handler.
   - Inspect `GroupDamageIncoming`, `CheckForSharedDamageEffect`, and `GetPartyAvgHPPercent`.
   - Expected: broad shared-damage detection plus zero-party average HP can make solo false positives expensive.

## 2. Edit

Add a narrow context gate:

```csharp
private static bool ShouldHandleHealerRaidwides(bool isHealer)
{
    if (!isHealer || !InCombat())
        return false;

    if (InBossEncounter())
        return true;

    return InDuty() && IsInParty(2);
}
```

Then require it before `GroupDamageIncoming`:

```csharp
if (ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming(out var multi))
```

Do not change normal SGE combo priorities in this move.

## 3. Test

Add a structural regression test in `WrathCombo.Tests/RotationStructureTests.cs`:

- Assert the raidwide condition uses `ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming`.
- Assert the helper preserves both `InBossEncounter()` and `InDuty() && IsInParty(2)`.

This is intentionally source-structure based because the repo does not have a behavioral test harness for Dalamud combat state.

## 4. Verification route

1. Run xUnit tests:

```bash
dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore
```

Expected: all tests pass.

2. Attempt the release build:

```bash
dotnet build WrathCombo/WrathCombo.csproj -c Release
```

Expected in a correctly hydrated Dalamud environment: build completes with baseline warnings only. In the current Linux container, failure on missing Dalamud/FFXIV references is an environment blocker, not a proof of this change failing.

3. Manual in-game verification:

- Enable SGE auto actions.
- Keep `MaximumWeavesPerWindow = 1`.
- Fight a dummy solo outside a boss encounter.
- Expected: the global raidwide path does not fire Kerachole/Physis/Eukrasia just because dummy combat is active; offensive actions such as Psyche and Phlegma are no longer displaced by raidwide healing.
- Then verify one grouped duty/boss pull with `HandleRaidwides=true`.
- Expected: raidwide tools still fire in boss or grouped duty contexts.

## 5. Abort and fork map

- If SGE still dumps heals on a solo dummy after this fix, inspect whether the casts come from SGE local heal combos rather than `HandleRaidwide`; route to SGE-specific heal thresholds.
- If grouped duty raidwides stop firing, inspect `InDuty()` and `IsInParty(2)` behavior in that content; add the narrowest missing content predicate.
- If real open-world boss content needs raidwide handling, add a project-approved open-world exception rather than removing the context gate.
- If `CheckForSharedDamageEffect` keeps causing other jobs to overreact, reconvene for COA-3: narrow the shared-damage classifier.

## 6. Execution record

- Code gate added in `AutoRotationController.cs`.
- Structural test added in `RotationStructureTests.cs`.
- Test verification passed: 39/39.
- Release build attempted but blocked by Linux reference setup for Dalamud/FFXIV assemblies.

## 7. Follow-up fork execution

The first live reload still spammed SGE healing cooldowns on a dummy. The fork in section 5 was executed.

- Found SGE advanced heal combos had unguarded local raidwide feature blocks in `SGE_ST_Heal_AdvancedMode` and `SGE_AoE_Heal_AdvancedMode`.
- Found `GetPartyAvgHPPercent()` returned `0` when no party members were counted, so solo/no-party state looked like a fully damaged party.
- Guarded the SGE advanced heal raidwide blocks with `!AutoRotationController.IsSelectingAutorotAction`.
- Changed no-party average HP to `100`.
- Added structural tests for both protections.
- Verification passed: 41/41 xUnit tests, release build succeeded with the explicit Dalamud hook path, and the host dev-plugin DLL was refreshed at `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`.
