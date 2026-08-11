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
# T005 — Tank Cooldown Helper IPC

## Contract

- **IPC name:** `TankCooldownHelper.GetLocalDangerSnapshot`
- **Signature:** `ICallGateProvider<string>` / `InvokeFunc()` → JSON (`ok`, `incomingDps`, `incomingHps`, `dangerRatio`, `dangerLevel`, `inDanger`, `secondsUntilDeath`)
- **dangerLevel:** TCH `DangerLevel` enum as `int` (0=Safe, 1=Warning, 2=Critical, 3=Emergency)
- **inDanger:** `dangerLevel >= Warning`
- **Gate:** TCH `EnableParseLord5Integration` (settings → Plugin Integration)

## PL5 usage

- `TankCooldownHelperIpcClient.TryGetPlayerPressure` → `PlayerPressureState` with `TankCooldownDangerLevel` set
- WAR smart mit: TCH first, `CombatTelemetryService` fallback
- Framework HP telemetry skipped when TCH plugin is loaded (experimental mode)

## Fix (2026-05-31): TCH Emergency but no PL5 mit

**Root cause (combined):**
1. PL5 stopped `CombatTelemetryService.Update()` whenever TCH was loaded — if IPC failed (`EnableParseLord5Integration` off, JSON/ready), pressure was empty.
2. Threat required `NetDps > 0` even when TCH showed Emergency (HP%/sec path can have net ≈ 0).
3. Coverage calculator returned null at high HP (no TB); fallback was TB-only.

**Fix:** Always run telemetry fallback; Emergency bypasses threat/coverage gates; TCH danger fallback picks one CD; trace `tch_ipc_miss` / `tch_emergency`.

## Damnation gating (`ShouldOfferDamnation`)

Damnation/Vengeance (Large) is offered only when:

1. **`HasIncomingTankBusterEffect`** (strict telegraph — not soft `IsPlayerTargeted`), or  
2. **TCH Emergency** + **in combat** + **IPC danger** + player HP **&lt; 50%**

**Not** offered for soft tankbuster, Critical-only, Emergency at high HP, or pull targeting.

Trace when Damnation fires: `damnation_gate=tb_confirmed` or `tch_emergency_lowhp`.

### Fix (pull instant Damnation)

Soft tankbuster (`IsPlayerTargeted` on boss pull) was included in the gate — removed.

## CD overlap (`LongMitigationRecastSeconds = 60`)

- **Short** (Reprisal at 60s, Rampart, Raw Intuition, Arms Length): may overlap each other and with an active long buff.
- **Long** (Vengeance/Damnation, Holmgang, Shake It Off — recast &gt;60s / explicit long actions): no second long while any long buff is active (`IsWarLongMitigationActive` includes Shake shield buff).
- **Trash:** `TrySelectWarTrashReprisalFirst` runs before personal mit when threat + NonBoss Reprisal ready (shared `TrashMitigationOrdering`).

## Heals vs tank CDs + Bloodwhetting (2026-05-31)

- **Removed** from smart mit: Thrill of Battle, Equilibrium (still in `WAR_Helper` / rotation heal presets).
- **Blocked** while `BloodwhettingDefenseLong` / `BloodwhettingDefenseShort` active: all smart mit in `TrySmartMits` (personal, party, Holmgang emergency path included).

## In-game check

1. Build/install both plugins to devPlugins
2. Enable ParseLord5 experimental mode + WAR mitigation presets
3. TCH `/tch` — confirm meter shows danger under damage
4. Verify smart mit fires on danger (not all CDs at once) and respects tankbuster telegraphs
