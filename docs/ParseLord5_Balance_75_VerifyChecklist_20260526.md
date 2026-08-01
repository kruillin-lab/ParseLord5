# ParseLord5 — 7.5 In-Game Verification Checklist (Phase B)

**Date:** 2026-05-26
**Companion doc:** `ParseLord5_Balance_75_Survey_20260526.md`
**Audience:** Plugin maintainer running manual in-game verification.

## Purpose

Phase A (survey) found PL5 rotation logic is in line with Balance 7.5 guidance
on paper. Phase B confirms it in-game on a striking dummy / live content.

Code changes are **only** expected if Phase B observes a divergence. Default
outcome of Phase B is "no changes needed."

## Setup

- Patch level: 7.5x (current live).
- Build PL5 from this branch in Release mode (`dotnet build AutoRotationPlugin.csproj -c Release`).
- Load plugin in XIVLauncher devPlugins (PostBuild copies it automatically).
- Use a level 100 character per job tested.
- Test target: high-HP striking dummy in The Wanderer's Palace housing /
  Hingashi Aether or equivalent.

## Test matrix — 6 buffed jobs

For each job below, run the listed scenarios and tick the checkboxes.

### WAR

- [ ] Opener executes end-to-end without skipped GCDs (`/pl auto` on)
- [ ] Inner Chaos fires inside IR window
- [ ] Primal Rend fires while target is in melee range
- [ ] Primal Ruination follows Primal Rend without delay
- [ ] AoE rotation: Steel Cyclone / Decimate / Orogeny priority correct
- [ ] No animation lock breaks (>= 0.7s GCD remaining when oGCD weaves)

Expected: ~1% DPS bump on parse vs pre-7.5 baseline. Sequence unchanged.

### SAM

- [ ] Opener executes end-to-end
- [ ] Midare Setsugekka fires under Higanbana + Meikyo window
- [ ] Kaeshi: Setsugekka fires immediately after Midare
- [ ] Sen gauge reaches 3 before Iaijutsu cast (no clipping)
- [ ] Tsubame Gaeshi alignment with party buffs holds

Expected: <1% DPS bump. Sequence unchanged.

### RPR

- [ ] Opener executes end-to-end
- [ ] Gluttony weaves after Arcane Circle
- [ ] Enshroud window enters after Plentiful Harvest
- [ ] Void Reaping / Cross Reaping chain alternation correct
- [ ] Enhanced variants (Communio, etc.) fire when ready

Expected: burst-window damage up; placement unchanged.

### VPR

- [ ] Opener executes through full ~38-step array
- [ ] Serpent's Ire fires step 2 (early Ire)
- [ ] First Reawaken triggers after Vicewinder/Coil dance + Twin*Bites
- [ ] 1st/2nd/3rd/4th Generation+Legacy pairs all execute in order
- [ ] Ouroboros fires after 4th Legacy
- [ ] Second Reawaken sequence enters via Vicewinder again
- [ ] Hunter's Coil / Swiftskin's Coil rear-positional substitution works
  (`OnTargetsRear` triggers step-33 SwiftskinsCoil swap)
- [ ] AoE: confirm Reawaken splash falloff is 75% (server-side change visible
  in parse logs)

Expected: ~1% ST DPS bump, AoE more cleave damage. Sequence unchanged.

### SMN

- [ ] Opener executes end-to-end (27 steps)
- [ ] Precast Ruin III lands before pull timer 0
- [ ] Solar Bahamut summons at step 2
- [ ] Searing Light fires step 4 (delayed weave per opener config)
- [ ] Energy Drain at step 7
- [ ] Enkindle Solar Bahamut at step 9
- [ ] Necrotize fires both times (steps 10 and 13)
- [ ] Sunflare at step 12
- [ ] Searing Flash at step 15
- [ ] Titan summons at step 16, 4x TopazRite+MountainBuster pairs follow
- [ ] Garuda + Swiftcast + Slipstream end sequence executes
- [ ] Swiftcast skip option works when `SMN_Opener_SkipSwiftcast == 2`

Expected: ~1.5% DPS bump (largest of patch). Sequence unchanged.

### SGE

- [ ] DPS opener fires Eukrasian Dosis first, then GCD damage
- [ ] Phlegma charges spent inside burst window
- [ ] Psyche weaves correctly
- [ ] DoT refresh timing: Eukrasian Dosis refreshed before falloff
- [ ] Healing priority chain still respects user-configured target priority
- [ ] Kardia stays on configured target

Expected: damage gap to SCH closes; sequence unchanged.

## Cross-job spot checks (any of the 21 covered jobs)

- [ ] No log-spam errors in `/xllog` related to action IDs
- [ ] `/wrath debug <job>` outputs expected feature set
- [ ] Auto-rotation can be toggled mid-fight without crash
- [ ] Mouseover targeting still resolves correctly
- [ ] Tank double-Reprisal protection still triggers
- [ ] Healer raise feature still triggers on dead party member

## FFLogs comparison (optional)

Pull a current top-parse log for each of the 6 buffed jobs from FFLogs
(post-2026-04-28 logs). Compare opener action order to what PL5 produces.

| Job | Top-parse FFLogs sample | PL5 match? |
|-----|------------------------|------------|
| WAR |                        |            |
| SAM |                        |            |
| RPR |                        |            |
| VPR |                        |            |
| SMN |                        |            |
| SGE |                        |            |

## If divergence is found

1. Capture the FFLogs sequence step-by-step.
2. Compare to the PL5 `OpenerActions` array for that job.
3. Open a focused ticket / branch for that job only.
4. Apply minimal diff to the opener array; keep `ContentCheckConfig` gates intact.
5. Re-run this checklist for the affected job.

## Outcome record

- [ ] All boxes ticked → mark survey as "verified, no changes needed" in
  follow-up commit message.
- [ ] One or more boxes failed → open Phase C with specific job and step.
