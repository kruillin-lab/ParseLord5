# ParseLord5 — Balance FFXIV 7.5 Rotation Survey

**Date:** 2026-05-26
**Patch surveyed:** FFXIV 7.5 "Trail to the Heavens" (released 2026-04-28)
**PL5 branch:** `claude/hardcore-babbage-11499c`
**Surveyor:** Claude Code (Opus 4.7)

## TL;DR

Patch 7.5 is a pure numerical-tuning patch. No new actions, no removed actions,
no opener restructures, no rotation rewrites on either Square Enix's side or
The Balance's side. PL5 rotation logic is broadly in line with 7.5 Balance
guidance because the underlying opener sequences and oGCD priorities did not
change. Server-side potency adjustments do not require any plugin code change.

## Patch 7.5 server-side combat changes

Source priority: mmogah summary, The Balance VPR job-changes page, icy-veins
Dawntrail 7.5 overview, Lodestone preliminary patch notes.

| Job | 7.5 change | Magnitude |
|-----|-----------|-----------|
| WAR | Inner Chaos +40 potency, Primal Rend +20, Primal Ruination +20 | ~1% DPS |
| SAM | Midare Setsugekka +40 potency, Kaeshi: Setsugekka +40 | <1% DPS |
| RPR | Gluttony 520 → 560 potency, Void Reaping +20, Cross Reaping +20, enhanced variants +20 each | Burst increase under Enshroud |
| VPR | Vicewinder 500 → 540, Hunter's Coil & Swiftskin's Coil 620 → 680 (positional), Reawaken splash falloff 80% → 75% (5% more splash) | ~1% ST DPS, AoE buff |
| SMN | Painflare +70 potency, Necrotize +40, Crimson Cyclone / Crimson Strike / Ruby Rite +40 each | ~1.5% DPS (largest of patch) |
| SGE | Phlegma +90 potency, Psyche +90, Eukrasian Dosis tick +5 | Closes gap to SCH |
| WHM, SCH, AST | Generic healer role potency lifts (per press notes) | Healer tier rebalance |
| PLD, DRK, GNB, MNK, DRG, NIN, BRD, MCH, DNC, BLM, RDM, PCT | No combat changes | — |

## PL5 impact

**Zero hardcoded potency references in rotation logic.** A grep across all 21
covered jobs for "potency" / "Potency" hits exactly one match
(`WrathCombo/Combos/PvE/WAR/WAR_Helper.cs:833,837`), and those constants are
trait-level offset IDs (`MeleeMastery1 = 505`, `MeleeMastery2 = 654`), not
potency values used in any decision.

PL5 chooses **which action to fire**, not how much damage that action deals.
Potency tuning is resolved server-side. Therefore the 7.5 numerical patch has
no direct impact on PL5 code.

## Opener verification — affected jobs

### VPR — `WrathCombo/Combos/PvE/VPR/VPR_Helper.cs:388-461`

Current PL5 opener array:

```
ReavingFangs → SerpentsIre → SwiftskinsSting → Vicewinder → HuntersCoil →
TwinfangBite → TwinbloodBite → SwiftskinsCoil → TwinbloodBite → TwinfangBite →
Reawaken → 1stGen+1stLegacy → 2ndGen+2ndLegacy → 3rdGen+3rdLegacy →
4thGen+4thLegacy → Ouroboros → 3x(UncoiledFury+UncoiledTwinfang+UncoiledTwinblood) →
HindstingStrike → DeathRattle → Vicewinder → UncoiledFury triple →
HuntersCoil → TwinfangBite → TwinbloodBite → SwiftskinsCoil →
TwinbloodBite → TwinfangBite
```

Balance "Standard Double Reawaken" model: Serpent's Ire pressed early, buffer
GCDs (Vicewinder/Coil dance) into the 2-min raid buff window, then back-to-back
Reawaken with Uncoiled fillers between. PL5 array matches this model.

Status: in line with Balance 7.5. No code change required.

### WAR — `WrathCombo/Combos/PvE/WAR/WAR_Helper.cs`

7.5 buffed Inner Chaos, Primal Rend, Primal Ruination. All three are already in
PL5's existing IR (Inner Release) burst window logic. Balance opener page
(updated 2025-12-27) shows no 7.5 sequence change.

Status: in line. Recent commit `99b37a1ce` already gates experimental WAR
GCD/oGCD reorder to weave windows.

### SAM — `WrathCombo/Combos/PvE/SAM/SAM_Helper.cs`

7.5 buffed terminal Iaijutsu (Midare Setsugekka) and its Tsubame follow-up
(Kaeshi: Setsugekka). Both already terminal moves in PL5's Iaijutsu/Tsubame
chain. Balance opener page still labeled 7.05 because rotation hasn't changed
since Dawntrail launch.

Status: in line.

### RPR — `WrathCombo/Combos/PvE/RPR/RPR_Helper.cs`

7.5 buffed Gluttony plus all Void/Cross Reaping variants. Placement unchanged:
Gluttony weaves after Arcane Circle, Enshroud follows Plentiful Harvest.
Balance opener (updated 2026-02-19) confirms unchanged structure.

Status: in line.

### SMN — `WrathCombo/Combos/PvE/SMN/SMN_Helper.cs:764`

Current PL5 opener array:

```
Ruin3 → SummonSolarBahamut → UmbralImpulse → SearingLight →
UmbralImpulse → UmbralImpulse → EnergyDrain → UmbralImpulse →
EnkindleSolarBahamut → Necrotize → UmbralImpulse → Sunflare →
Necrotize → UmbralImpulse → SearingFlash → SummonTitan2 →
TopazRite → MountainBuster → (4x) → SummonGaruda2 →
Role.Swiftcast → Slipstream
```

Matches Balance Lv.100 General Opener: precast Ruin III, Solar Bahamut first,
Searing Light early, Energy Drain weave, then Titan phase with Topaz Rite +
Mountain Buster pairs. Balance changelog 2026-04-29 was image-only update; no
sequence change.

Status: in line. Painflare/Necrotize/Crimson Cyclone potency buffs are
server-side and require no PL5 change.

### SGE — `WrathCombo/Combos/PvE/SGE/SGE_Helper.cs`

7.5 buffed Phlegma, Psyche, and Eukrasian Dosis. All three already in PL5's
DPS priority list. Balance opener page still labeled 7.3 because no rotation
change.

Status: in line.

## Non-affected jobs (15)

PLD, DRK, GNB, AST, WHM, SCH, MNK, DRG, NIN, BRD, MCH, DNC, BLM, RDM, PCT —
no patch notes touched these. PL5 logic for these jobs has not gone out of date
due to 7.5. Existing experimental flags (`ParseLord5ExperimentalMode`) for
oGCD reorders are independent of Balance and out of scope for this survey.

## Caveats

1. Several Balance opener pages still display patch labels older than 7.5
   (WAR 7.4, SAM 7.05, SGE 7.3, RPR 7.4). The site has not aggressively
   re-versioned because openers did not change. Only VPR (2026-04-30) and
   SMN (2026-04-29) got 7.5 page updates, and both were visual/image refreshes
   rather than sequence changes.
2. Balance opener cheat sheets are JPEG images; WebFetch cannot read them.
   Step-by-step opener verification was done via cross-references (basic guide,
   intermediate guide, icy-veins, search results) rather than image OCR. For
   byte-perfect verification, screenshot the Balance image or ask in the
   Balance Discord.
3. Existing PL5 experimental flags (DRG LanceCharge/BattleLitany swap, WAR
   weave gating, BLM/SMN/DNC/MCH/RDM/BRD/NIN tweaks behind
   `ParseLord5ExperimentalMode`) may diverge from Balance by design. They were
   not evaluated as "out of date" for this survey.

## Recommendation

No rotation code changes required for 7.5. Proceed to Phase B (in-game
verification) per the checklist in `ParseLord5_Balance_75_VerifyChecklist_20260526.md`.

## Sources

- The Balance — VPR job-changes: https://www.thebalanceffxiv.com/jobs/melee/viper/job-changes/
- The Balance — job index: https://www.thebalanceffxiv.com/
- mmogah — 7.5 Job Balance Changes: https://www.mmogah.com/news/ffxiv/ffxiv-patch-75-job-balance-changes-and-ultimate-job-picks
- Icy Veins — Dawntrail 7.5: https://www.icy-veins.com/ffxiv/dawntrail-patch-7-5
- Lodestone — 7.5 preliminary patch notes: https://na.finalfantasyxiv.com/lodestone/topics/detail/33c47e6b7aa6fe0750ebb167841e35bea26ceb7e
- Square Enix press — 7.5 reveal: https://press.na.square-enix.com/FINAL-FANTASY-XIV-PATCH-75-TRAIL-TO-THE-HEAVENS-REVEALED-FOR-APRIL-28-
