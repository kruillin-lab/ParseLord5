---
tags:
  - type/doc
  - project/parselord5
  - status/active
type: doc
project: parselord5
status: active
aliases: []
---
# Smart Mitigation Stacking Rules

Date: 2026-08-01
Status: Approved
Scope: SmartMitigation shared services + tank job bridges

## Problem

SmartMitigation currently only prevents long+long mitigation overlap. Short CDs can
freely stack with each other, wasting resources. The user wants symmetric pool-based
exclusion: same-pool mits don't overlap, cross-pool mits can stack.

## Rules

### Pool Classification

| Pool | Criteria | Members (examples) |
|---|---|---|
| **Exempt** | Hardcoded per-action | Bloodwhetting (WAR 25s), Sheltron (PLD 5s), The Blackest Night (DRK 15s), Heart of Corundum (GNB 25s) |
| **Long** | Cooldown > 60s, not exempt | Rampart (90s), Nebula (120s), Shadow Wall (120s), Camouflage (90s), Heart of Light (90s), etc. |
| **Short** | Cooldown ≤ 60s, not exempt | Reprisal (60s), Aurora (60s), etc. |
| **TrashOnly** | Hardcoded per-action | Arm's Length (90s) — only offered in trash packs, excluded on bosses |
| **Invuln** | Tier = Invuln | Hallowed Ground, Holmgang, Superbolide, Living Dead — special-case, bypasses stacking |

### Stacking Rules

1. **Same-pool exclusion**: If a Long-pool buff is active, no other Long option is offered. Same for Short.
2. **Cross-pool allowed**: Long active + Short option = fine. Short active + Long option = fine.
3. **Exempt always offered**: Bloodwhetting/Sheltron/TBN/HoC ignore stacking state entirely.
4. **TrashOnly gated by encounter**: Arm's Length is excluded when `isBoss == true`.
5. **Invuln bypasses**: Invuln selection is unaffected by pool state.

### Threshold

- Cutoff: **60 seconds**. Cooldown strictly > 60s = Long. Cooldown ≤ 60s = Short.
- Reprisal at exactly 60s = Short (existing behavior preserved).
- Threshold is a hardcoded constant (`LongMitigationRecastSeconds = 60f`), already defined.

## Architecture (Approach A)

Extend existing `TrashMitigationOrdering` + selection filter. No new service file.

### Model Changes

**`MitigationModels.cs`**:
- New enum `MitigationPool { Exempt, Short, Long, TrashOnly }`
- `MitigationOption` record gains a `MitigationPool Pool` field
- `ActiveMitigationState` record gains `bool LongPoolActive` and `bool ShortPoolActive`

**`TrashMitigationOrdering.cs`** (may rename to `MitigationStacking.cs`):
- New: `ClassifyPool(float cooldownSeconds, bool isExemptAction, bool isTrashOnly) → MitigationPool`
- New: `ShouldExcludeForStacking(MitigationPool pool, ActiveMitigationState active, bool isBoss) → bool`
- Existing `ShouldExcludeLongMitigationOption` becomes a thin wrapper or is replaced

### Selection Integration

**`TankMitigationSelection.cs`**:
- `TryPickInTierRange` replaces `longMitigationBuffActive` + `isLongMitigationAction` params with `ActiveMitigationState` + `isBoss`
- Calls `ShouldExcludeForStacking` per-option before scoring

**`MitigationCoverageCalculator.cs`**:
- `SelectMinimumMitigation` gains the same stacking gate before scoring candidates
- Invuln paths remain unaffected

### Job Bridge Updates

Each tank bridge (WAR, DRK, GNB, PLD) must:
1. Tag each `MitigationOption` with its `Pool` when building the options catalog
2. Detect active buffs → set `LongPoolActive` / `ShortPoolActive`
3. Pass `isBoss` (already available via `InBossEncounter()` or equivalent)
4. Mark exempt actions in their catalog entries

### Exempt Action Registry

A small static set (or per-job constant) identifying exempt action IDs:
- WAR: Bloodwhetting
- PLD: Sheltron (and Holy Sheltron variant)
- DRK: The Blackest Night
- GNB: Heart of Corundum

These are hardcoded per-job since they're gameplay-specific, not derivable from cooldown alone.

## Testing

New xUnit tests in `WrathCombo.Tests` (pure logic, zero Dalamud deps):

| Test | Asserts |
|---|---|
| Long active → long excluded | `ShouldExcludeForStacking(Long, state{LongPoolActive=true}, isBoss) == true` |
| Long active → short allowed | `ShouldExcludeForStacking(Short, state{LongPoolActive=true}, isBoss) == false` |
| Short active → short excluded | Symmetric |
| Short active → long allowed | Symmetric |
| Exempt always passes | Both active → still false |
| TrashOnly on boss | `ShouldExcludeForStacking(TrashOnly, any, isBoss=true) == true` |
| TrashOnly on trash | `ShouldExcludeForStacking(TrashOnly, any, isBoss=false) == false` |
| Exactly 60s = Short | `ClassifyPool(60f, false, false) == Short` |
| 61s = Long | `ClassifyPool(61f, false, false) == Long` |
| Coverage calculator respects stacking | Integration-style: options list with active long → long options not selected |

## Out of Scope

- Structural refactor of job files / AutoRotationController (separate effort)
- Raid-mit coordination, phase awareness, multi-tank awareness
- Config UI for the 60s threshold
- Upstream WrathCombo compatibility
- Healer/DPS mitigation logic

## Acceptance Criteria

- [ ] `MitigationPool` enum + model fields added
- [ ] `ClassifyPool` and `ShouldExcludeForStacking` implemented in shared service
- [ ] `TankMitigationSelection` uses pool-aware exclusion
- [ ] `MitigationCoverageCalculator` uses pool-aware exclusion
- [ ] All 4 tank bridges updated (WAR, DRK, GNB, PLD)
- [ ] Arm's Length excluded on bosses in all bridges
- [ ] xUnit tests pass (new + existing 34)
- [ ] `dotnet build WrathCombo/WrathCombo.csproj -c Release` green
- [ ] `scripts/rotation-evals.ps1` passes
