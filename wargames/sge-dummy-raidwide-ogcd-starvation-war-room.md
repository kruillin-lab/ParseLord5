---
tags:
  - type/report
  - project/parselord5
  - status/done
type: report
project: parselord5
status: done
aliases: []
---
# War Room - SGE dummy raidwide oGCD starvation

**Status:** EXECUTED
**Date opened:** 2026-07-07
**Skill:** `war-room`
**Battle plan:** [sge-dummy-raidwide-ogcd-starvation-battle-plan.md](sge-dummy-raidwide-ogcd-starvation-battle-plan.md)

Convened to answer why Sage auto-rotation, while fighting a dummy, was spending weave slots on healer cooldowns and not reliably using offensive oGCDs.

## 1. Frame

- **Current state:** SGE can enter dummy combat with offensive actions available, but global healer raidwide handling can spend the single configured weave slot on raidwide/self-healing tools such as Kerachole, Physis II, Eukrasia, and Eukrasian Prognosis.
- **Desired state:** dummy/open-world combat should not trigger the global raidwide responder; offensive oGCD selection should not be displaced by raidwide healing unless the character is in a boss encounter or grouped duty context.
- **Non-negotiables:** keep the existing SGE combo-local boss gates intact; avoid broad SGE priority rewrites; do not disturb unrelated uncommitted work; preserve grouped-content raidwide handling.
- **Config fact:** live config has `MaximumWeavesPerWindow = 1`, so any false-positive healer oGCD fully consumes the weave window.

## 2. Recon

### Facts

| # | Fact | Evidence |
| --- | --- | --- |
| F1 | Prior post-execution notes already identified a first layer: SGE setup heals were widened outside boss encounters and could starve Psyche/Soteria under one-weave settings. | `wargames/parselord5-stability-battle-plan.md`, post-execution incident |
| F2 | Current SGE source already contains the first-layer repair shape: Psyche is before Soteria, Soteria/addersgall paths are suppressed during autorotation action selection, and some `!InBossEncounter()` gates are restored. | `WrathCombo/Combos/PvE/SGE/SGE.cs` |
| F3 | Live logs from the dummy session still show healer tools firing in dummy combat, including Kerachole, Physis II, Eukrasia, and Lucid Dreaming, interleaved with Dosis/Phlegma/Psyche. | `~/.xlcore/logs/dalamud.log`, 2026-07-07 around 09:31-09:33 |
| F4 | The top-level healer raidwide path runs before normal auto-action processing when `HandleRaidwides` is enabled. Before the fix, it required only `isHealer && GroupDamageIncoming(out var multi)`. | `WrathCombo/AutoRotation/AutoRotationController.cs` |
| F5 | `GroupDamageIncoming` can be satisfied by `RaidwideCasting` or broad shared-damage VFX detection; the shared-damage classifier does not require a party member target. | `WrathCombo/CustomCombo/Functions/Action.cs`, `WrathCombo/CustomCombo/Functions/VFX.cs` |
| F6 | `HandleRaidwide` uses the SGE raidwide action list directly: Panhaima, Kerachole, Physis, Physis II, Holos, Eukrasia, and Eukrasian Prognosis. | `WrathCombo/AutoRotation/AutoRotationController.cs` |
| F7 | `GetPartyAvgHPPercent()` returns `0` when no party members are counted, making solo false-positive raidwide state look like very low average party HP to `HandleRaidwide`. | `WrathCombo/CustomCombo/Functions/Party.cs` |

### Assumptions

| # | Assumption | Settling check | Routing |
| --- | --- | --- | --- |
| A1 | The dummy false positive is from shared-damage VFX or raidwide-cast classification rather than SGE combo-local logic. | In-game repro with verbose logs: confirm raidwide handler entry when solo on a dummy. | Selected fix blocks solo/open-world non-boss raidwide handling regardless of the exact classifier. |
| A2 | Grouped duty non-boss encounters still need the raidwide responder. | Dungeon/trial check with `HandleRaidwides=true`. | Fix keeps `InDuty() && IsInParty(2)` as the non-boss allowed case. |

## 3. Criteria

| Criterion | Weight | Reason |
| --- | --- | --- |
| Correctness against dummy symptom | 3 | The user-facing failure is solo dummy weave starvation. |
| Preserve raidwide support in real content | 3 | Do not disable healer safety tools in duties or bosses. |
| Blast radius | 3 | There is already substantial uncommitted rotation work. |
| Testability | 2 | The local environment cannot fully build the plugin without a valid Dalamud hook path. |
| Reversibility | 2 | The change should be easy to back out if in-game verification says otherwise. |

## 4. Courses of Action

### COA-0 - Config workaround

Disable `HandleRaidwides` or increase `MaximumWeavesPerWindow`.

- **Result:** rejected as a fix. It can reduce the symptom locally but leaves the controller able to classify solo dummy state as a raidwide.

### COA-1 - Context-gate the global healer raidwide responder

Only let the global healer raidwide path run for healers who are in combat and either in a boss encounter or in grouped duty content.

- **Result:** selected. It blocks the dummy/open-world false positive at the top-level choke point while preserving raidwide handling in bosses and party duties.
- **Edit:** add `ShouldHandleHealerRaidwides(isHealer)` and require it before `GroupDamageIncoming(out var multi)`.

### COA-2 - Reorder SGE priorities further

Reserve the first weave slot for offensive oGCDs or push all healing oGCDs lower in SGE.

- **Result:** rejected for this incident. The current failure route is above SGE-local priority logic, so further SGE ordering changes would not address the global raidwide caller.

### COA-3 - Narrow the shared-damage VFX classifier

Make `CheckForSharedDamageEffect` stricter so dummy/open-world effects do not look like shared damage.

- **Result:** kept as future hardening. It has higher blast radius because other jobs and helpers may rely on the current broad classifier.

## 5. Wargame

| Move | Expected observation | Failure mode | Counter |
| --- | --- | --- | --- |
| Add `ShouldHandleHealerRaidwides` before `GroupDamageIncoming` | Solo dummy combat skips `HandleRaidwide`; normal SGE DPS auto-actions continue. | A real open-world boss without `InBossEncounter()` stops using raidwide tools. | Treat that as intentional unless the project wants open-world raidwide handling; duty and boss contexts remain enabled. |
| Preserve `InDuty() && IsInParty(2)` | Dungeon/trial trash can still get raidwide handling if grouped. | Solo duty support needs raidwide handling. | Add a narrower solo-duty exception only after in-game evidence. |
| Add structural test | Future refactor cannot silently drop the context gate. | Test is source-text based rather than behavioral. | Accept as the available low-cost guard; behavioral plugin tests are not present. |

## 6. Decision record

- **Selected:** COA-1.
- **Why:** the problem is a top-level healer raidwide path consuming the only weave window, not a missing SGE offensive action. The safest repair is to prevent solo dummy/open-world combat from entering that global raidwide path.
- **Rejected alternatives:** config-only leaves the bug live; SGE priority work misses the top-level caller; VFX classifier hardening is valuable but wider than needed.
- **Residual risk:** in-game verification is still needed because the local Linux build cannot complete without the current Dalamud hook/reference setup.

## 7. Execution

- Added `ShouldHandleHealerRaidwides(bool isHealer)` in `WrathCombo/AutoRotation/AutoRotationController.cs`.
- Changed the raidwide condition from `isHealer && GroupDamageIncoming(out var multi)` to `ShouldHandleHealerRaidwides(isHealer) && GroupDamageIncoming(out var multi)`.
- Added `HealerRaidwideHandler_RequiresGroupedContentOrBossContext` to `WrathCombo.Tests/RotationStructureTests.cs`.

## 8. Verification

- `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` inside `dev-arch`: passed, 39/39.
- `dotnet build WrathCombo/WrathCombo.csproj -c Release` inside `dev-arch`: blocked by missing Dalamud/FFXIV reference resolution in the Linux environment, matching the known `docs/AGENTS_FULL.md` hook-path gotcha.
- Formal quality gate run `20260707T134001Z-25cdc358`: failed. The build and domain-eval commands are Windows `powershell` commands, and the Markdown audit also flags three pre-existing untracked wargame docs without frontmatter. This session therefore used the passing xUnit structure check plus explicit build-attempt evidence.

## 9. Live-test follow-up

- User reloaded PL5 and still observed SGE dumping healing cooldowns on a dummy.
- Follow-up recon found two routes not closed by the global raidwide gate:
  - `SGE_ST_Heal_AdvancedMode` and `SGE_AoE_Heal_AdvancedMode` had their own raidwide feature blocks that were not suppressed during auto-rotation selection.
  - `GetPartyAvgHPPercent()` returned `0` when no party members were counted, so solo dummy combat made every average-party HP threshold look satisfied.
- Applied second-layer fix:
  - Guarded SGE advanced heal raidwide blocks with `!AutoRotationController.IsSelectingAutorotAction`.
  - Changed no-party average HP from `0` to `100`.
  - Added structural regressions for both conditions.
- Verification:
  - `dotnet test WrathCombo.Tests/WrathCombo.Tests.csproj -c Release --no-restore` inside `dev-arch`: passed, 41/41.
  - `dotnet build WrathCombo/WrathCombo.csproj -c Release -p:DalamudLibPath=/home/kruillin/.xlcore/dalamud/Hooks/15.0.2.2/` inside `dev-arch`: passed with 3 baseline warnings.
  - Deployed host DLL to `/home/kruillin/.xlcore/devPlugins/ParseLord5/ParseLord5.dll`, which maps to `Z:\home\kruillin\.xlcore\devPlugins\ParseLord5\ParseLord5.dll`.
  - Deployed hash: `b5b6fcda03c430637dea48fae5d91ac9c7919afb8ec481302b6ece94302b3fce`.
  - Formal quality gate run `20260707T153632Z-dff5e00d`: failed on repo tooling (`powershell` missing for configured build/evals) plus unrelated untracked Markdown metadata issues, not on the focused xUnit or release build checks above.
