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
# T007 — Multi-tank smart mit port

**Date:** 2026-05-31  
**Scope:** PLD, GNB, DRK (+ WAR reference). All use TCH IPC + shared `TankSmartMitigationThreat` / `TrashMitigationOrdering`.

## Shared services (all tanks)

| File | Role |
| --- | --- |
| `Services/SmartMitigation/TankSmartMitigationThreat.cs` | Threat detection, TCH pressure, heavy-mit gate (TB + Emergency &lt;50% HP) |
| `Services/SmartMitigation/TankMitigationSelection.cs` | Tier pick + long-overlap skip |
| `Services/SmartMitigation/TrashMitigationOrdering.cs` | Trash Reprisal-first (≥3 enemies) |
| `Services/TankCooldownHelperIPC/TankCooldownHelperIpcClient.cs` | TCH JSON snapshot |

## Per-job files

| Job | Bridge | Smart mit | Entry |
| --- | --- | --- | --- |
| WAR | `WAR_MitigationBridge.cs` | `WAR_SmartMitigation.cs` | `WAR_Helper` CanUseBoss/NonBossMits |
| PLD | `PLD_MitigationBridge.cs` | `PLD_SmartMitigation.cs` | `PLD_Helper` |
| GNB | `GNB_MitigationBridge.cs` | `GNB_SmartMitigation.cs` | `GNB_Helper` |
| DRK | `DRK_MitigationBridge.cs` | `DRK_SmartMitigation.cs` | `DRK_ActionLogic` Mitigation provider |

## Long vs short (overlap rules)

| Job | Long (no overlap) | Heavy gated (TB / TCH Emergency &lt;50% HP) | Short (may overlap long) |
| --- | --- | --- | --- |
| WAR | Holmgang, Shake, Vengeance/Damnation | Damnation | Rampart, Arms, Raw/Bloodwhetting, Reprisal |
| PLD | Hallowed Ground, Sentinel/Guardian, Divine Veil (action) | Sentinel/Guardian | Rampart, Bulwark, Arms, Sheltron*, Reprisal |
| GNB | Superbolide, Nebula/Great Nebula, Heart of Light | Nebula/Great Nebula | Camouflage, Rampart, Arms, Heart of Stone*, Reprisal |
| DRK | Living Dead line, Shadow Wall/Vigil, Dark Missionary | Shadow Wall/Vigil | Dark Mind, Oblation, TBN*, Rampart, Arms, Reprisal |

\*Sheltron / Heart of Stone / TBN: prepass before coverage (not in calculator list).

## Heals excluded from smart mit

| Job | Notes |
| --- | --- |
| WAR | Thrill, Equilibrium — helper/rotation only |
| GNB | Aurora — not in smart option builders |
| PLD / DRK | No heal CDs in smart mit lists |

## Global test setup

1. ParseLord5 **experimental mode** ON  
2. TankCooldownHelper loaded, **Enable ParseLord5 integration** ON  
3. Job combo: **Advanced** mitigation ON (or Simple = all mit sub-options treated enabled)  
4. Boss: `*_Mitigation_Boss` parent + sub-options you want  
5. Trash: `*_Mitigation_NonBoss` parent + **NonBoss Reprisal** for Reprisal-first  
6. `/xllog` filter: `[ParseLord5][JOB_SmartMit]`

## Per-job testing checklist

### WAR (baseline — signed off)

- [ ] Trash 3+: Reprisal before Damnation/Rampart  
- [ ] Long buff up: second long blocked; Rampart still fires  
- [ ] Damnation only on TB telegraph or Emergency + HP &lt;50%  
- [ ] Bloodwhetting DR buff: no smart mit  

### PLD

**Presets:** `PLD_Mitigation_NonBoss` / `Boss`, NonBoss/Boss Reprisal, Sentinel, Bulwark, Rampart, Sheltron, Hallowed emergency  

- [ ] Trash Reprisal-first (3+ enemies)  
- [ ] Sheltron prepass at 50+ oath (non-boss)  
- [ ] Sentinel/Guardian not stacked; Rampart/Bulwark can overlap  
- [ ] Boss: Sentinel on strict TB gate; Rampart on TB/pressure  
- [ ] Divine Veil on raidwide / trash HP threshold (not overlapping long party mit)  

### GNB

**Presets:** `GNB_Mit_Advanced_NonBoss` / `Boss`, Reprisal, Nebula, Camouflage, Heart of Stone, Superbolide emergency  

- [ ] Trash Reprisal-first  
- [ ] Heart of Stone prepass when enabled  
- [ ] Nebula/Great Nebula gated like Damnation  
- [ ] Heart of Light not with Nebula/Superbolide active  
- [ ] Aurora never from smart mit path  

### DRK

**Presets:** `DRK_Mitigation_NonBoss` / `Boss`, Reprisal, Shadow Wall, Dark Mind, Blackest Night, Living Dead emergency  

- [ ] Trash Reprisal-first  
- [ ] TBN prepass when enabled  
- [ ] Shadow Wall/Vigil gated; Dark Mind/Oblation overlap OK  
- [ ] Dark Missionary party mit on raidwide; not during Shadow Wall buff  
- [ ] Living Dead emergency low HP only  

## DRK bug fix (2026-05-31)

**Symptom:** DRK only used base GCD combo (HardSlash chain); no mit CDs, no other oGCDs (Living Shadow, Delirium, etc.).

**Root cause:** `DRK.cs` Invoke seeds `newAction` with the combo anchor (`HardSlash` / `Unleash`). The Mitigation provider runs **first** and passes that same `ref action` into smart mit. When TCH reported threat but coverage + fallbacks picked nothing, `TrySelectSmartPersonalMitigation` used `if (selected is null && actionID != 0)` — **HardSlash is non-zero** — so it returned `true` without assigning a mit CD. Mitigation short-circuited the provider chain every weave window; Cooldown/Spender never ran.

**Fix:**

1. Clear `actionID = 0` at `TrySmartMits` entry; use a dedicated `fallbackAction` local for tier/TCH fallbacks (never treat caller anchor as fallback).  
2. Same fallback guard on WAR/PLD/GNB for parity.  
3. Experimental DRK mit: legacy `TryGetBossMitigation` / `TryGetNonBossMitigation` after smart path fails (WAR helper already had this pattern).  
4. Throttled trace `[ParseLord5][DRK_SmartMit] … source=enter` on smart mit entry.

**Verify:** `dotnet build .\WrathCombo\WrathCombo.csproj -c Release` — DRK in combat: oGCDs fire; mit CDs when threat + presets; no HardSlash “mit” spam in log.

## Known quirks / follow-ups

- PLD Divine Veil long overlap tracked by **action id**, not party buff id (no `Buffs.DivineVeil` constant).  
- DRK experimental mit: smart first, then legacy if smart returns false.  
- In-game tuning per duty — report job + log line + expected CD.
