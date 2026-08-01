# ParseLord5 — 7.5 Phase B Static Findings

**Date:** 2026-05-26
**Companion docs:**
- `ParseLord5_Balance_75_Survey_20260526.md` (Phase A audit)
- `ParseLord5_Balance_75_VerifyChecklist_20260526.md` (Phase B in-game checklist)

## Scope

Phase B static portion: cross-check the `OpenerActions` arrays for the six
7.5-buffed jobs against best-known Balance opener sequences. In-game
verification (the dummy-test checklist) remains for the maintainer to execute.

## Verdict

All six buffed-job opener arrays are structurally consistent with current
Balance 7.5 guidance. **No opener-array code changes recommended.**

## Per-job static review

### WAR — `WAR_Helper.cs:43-70`

Array (Lv100):

```
Tomahawk, Infuriate, HeavySwing, Maim, StormsEye, InnerRelease,
InnerChaos, Upheaval, Onslaught, FellCleave, Onslaught, FellCleave,
Onslaught, FellCleave, PrimalWrath, Infuriate, PrimalRend, PrimalRuination,
InnerChaos, HeavySwing, Maim, StormsPath, FellCleave, Infuriate, InnerChaos
```

Notes:
- Matches Balance "All-Around Opener" pattern: Storm's Eye applied before IR,
  IC fires immediately after IR, gapclosers (Onslaught) and FellCleaves alternate
  inside IR window, PrimalRend → PrimalRuination paired correctly.
- 7.5 buffed Inner Chaos / Primal Rend / Primal Ruination — all three already
  present and well-placed in this array.
- Onslaught x3 gated by `WAR_ST_BalanceOpener_GapcloserChoice` (line 76) — fine.

Static verdict: **in line with 7.5 Balance.**

### SAM — `SAM_Helper.cs:567-633`

Array (Lv100):

```
MeikyoShisui, TrueNorth, Gekko, Kasha, Ikishoten, Yukikaze,
TendoSetsugekka, Senei, TendoKaeshiSetsugekka, MeikyoShisui, Gekko,
Zanshin, Higanbana, OgiNamikiri, Shoha, KaeshiNamikiri, Kasha,
Shinten, Gekko, Gyoten, Gyofu, Yukikaze, Shinten, TendoSetsugekka,
Gyoten, TendoKaeshiSetsugekka
```

Notes:
- 7.5 buffed Midare Setsugekka (TendoSetsugekka in PL5 naming, step 7 and 24)
  and Kaeshi: Setsugekka (TendoKaeshiSetsugekka, step 9 and 26) — both fire
  inside the burst window.
- Higanbana applied at step 13 (secondary burst), not opener-first — matches
  Lv100 SAM theorycraft where opening burst is Senei+TendoSetsugekka, not DoT-first.
- Position-aware substitution: step 2 (TrueNorth) → step 11 if no positionals
  needed.

Static verdict: **in line with 7.5 Balance.**

### RPR — `RPR_Helper.cs:215-267`

Array (Lv100):

```
Harpe, ShadowOfDeath, SoulSlice, ArcaneCircle, Gluttony,
ExecutionersGibbet, ExecutionersGallows, SoulSlice, PlentifulHarvest,
Enshroud, VoidReaping, Sacrificium, CrossReaping, LemuresSlice,
VoidReaping, CrossReaping, LemuresSlice, Communio, Perfectio,
UnveiledGibbet, Gibbet, ShadowOfDeath, Slice
```

Notes:
- 7.5 buffed Gluttony (step 5, weaves after Arcane Circle — matches Balance
  "press as late as possible without clipping").
- 7.5 buffed Void/Cross Reaping (steps 11, 13, 15, 16) — all fire inside
  Enshroud window.
- Enhanced variants handled via positional substitution (`OnTargetsRear`,
  `EnhancedGibbet`, `EnhancedGallows`).

Static verdict: **in line with 7.5 Balance.**

### VPR — `VPR_Helper.cs:388-461`

Array already documented in Phase A survey. ~38 steps covering both Reawaken
sequences, Vicewinder/Coil dance, Uncoiled Fury triples.

Notes:
- 7.5 buffed Vicewinder (steps 4, 29), Hunter's Coil and Swiftskin's Coil
  (steps 5, 8, 33, 36) — all positional-aware via `OnTargetsRear` substitution.
- Reawaken splash falloff 80%→75% is server-side — no PL5 code impact.
- Standard Double Reawaken pattern: early Ire, Vicewinder dance into burst,
  Reawaken → Generations → Ouroboros → Reawaken cycle.

Static verdict: **in line with 7.5 Balance.**

### SMN — `SMN_Helper.cs:764-825`

Array already documented in Phase A survey. 27 steps.

Notes:
- 7.5 buffed Necrotize (steps 10, 13) and Sunflare-adjacent damage — already
  in opener.
- Painflare not in opener (single-target opener uses Necrotize). Painflare is
  an AoE primal-phase tool — confirm it's reachable in the AoE rotation
  separately if needed.
- Crimson Cyclone/Strike/Ruby Rite are Ifrit phase actions — primal phase is
  flexible per Balance ("you may use your primals in ANY order"), so they're
  handled by general rotation logic outside the opener array.

Static verdict: **in line with 7.5 Balance.**

### SGE — `SGE_Helper.cs:352-391` (Toxikon opener)

Array:

```
Toxikon, Eukrasia, EukrasianDosis3, Dosis3, Dosis3, Dosis3, Phlegma3,
Psyche, Phlegma3, Dosis3, Dosis3, Dosis3, Dosis3, Eukrasia,
EukrasianDosis3, Dosis3, Dosis3, Dosis3
```

Notes:
- 7.5 buffed Phlegma3 (steps 7, 9) and Psyche (step 8) — both inside burst
  window where +90 potency lands hardest.
- Eukrasian Dosis applied step 3, refreshed step 15 — tick +5 potency over
  full uptime.

Static verdict: **in line with 7.5 Balance.**

## Cross-cutting observations

- All six openers have proper `HasCooldowns()` gating — won't fire if
  cooldowns are not aligned.
- All six respect `ContentCheckConfig => *_Balance_Content` (Balance opener
  variants gated to appropriate content tiers).
- Positional substitution logic is consistent across VPR / SAM / RPR.
- No action IDs reference removed-in-7.5 abilities (none were removed).

## Recommendation

Proceed with in-game checklist verification per
`ParseLord5_Balance_75_VerifyChecklist_20260526.md`. Static review reveals
nothing requiring code change for 7.5.

Mark this branch as 7.5-verified after checklist is run and clean.
