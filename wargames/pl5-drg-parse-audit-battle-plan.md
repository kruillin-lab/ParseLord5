---
tags:
  - type/plan
  - project/parselord5
  - status/executed
type: plan
project: parselord5
status: executed
aliases: []
---
# Battle Plan - Dragoon parse audit tool

**Origin:** direct suggestion (2026-08-15) — user asked for the single most compelling addition to
ParseLord5; agreed to prototype on Dragoon first. No war room convened; this plan is the first
artifact for the feature.
**Status:** EXECUTED 2026-08-15/16 on `main`. Moves 0-5 complete: real ACT log format captured
(Deucalion/FFXIV_ACT_Plugin "Network" log, pipe-delimited type codes 21/26/30), real fixture saved
(`tests/fixtures/sample_real.log`, 471 lines, a genuine Dragoon dummy pull), parser + spec + audit
engine + CLI built against it. 9/9 pytest pass, CLI smoke test legible, zero `WrathCombo/` impact
(build 0/0, same pre-existing diff), red check passed and restored. Tool found a real, verified
issue in the captured pull: first Dive Ready proc held 14.7s of its 15s duration before Mirage Dive
consumed it — a genuine near-miss.
**Tracking:** none yet (open an issue before Move 1 if the repo's issue-tracker workflow is required).
**Never clobber:** sibling to the other wargames plans; different mission (offline analysis tool,
not a plugin-behavior change); do not merge.

---

## 0 · Theatre map

- **Repo:** `C:\Users\kruil\orca\ParseLord5` (Windows). Exactly one branch, `main` (`AGENTS.md`
  forbids more). Tree currently has unrelated uncommitted WIP (`MechanicPrediction`, encounter
  awareness) — **do not touch those files.**
- **Baseline (2026-08-15, `e367c8220`):** `dotnet build WrathCombo/WrathCombo.csproj -c Release` →
  0 errors / 0 warnings. Re-verify at HEAD before Move 1.
- **This feature is deliberately out-of-band:** a standalone Python CLI, not a Dalamud plugin
  change. It reads combat logs a player already has (ACT) and compares them against the *real*
  rotation logic already encoded in `DRG.cs`/`DRG_Helper.cs`. It does not touch the `.csproj`,
  does not compile into the plugin, and ships nothing to `devPlugins`.
- **Reference behavior already in tree (reuse, do not reinvent):**
  - `WrathCombo/Combos/PvE/DRG/DRG.cs:158-285` `DRG_ST_AdvancedMode.Invoke` — the real single-target
    priority list this tool audits against. Read verbatim, not paraphrased, at Move 1.
  - `WrathCombo/Combos/PvE/DRG/DRG_Helper.cs:21-95` `DoBasicCombo` — real GCD combo chain:
    `TrueThrust → VorpalThrust → OriginalHook(Disembowel)/ChaosThrust → WheelingThrust/FangAndClaw →
    Drakesbane`, with `Role.TrueNorth` interjections and `PowerSurge`/positional gating.
  - `WrathCombo/Combos/PvE/DRG/DRG_Helper.cs:101-258` — real oGCD gates: `CanLifeSurge`,
    `CanLanceCharge(hpThreshold)`, `CanBattleLitany(hpThreshold)`, `CanUseWyrmwind`
    (`FirstmindsFocus is 2`), `CanMirageDive` (`Buffs.DiveReady`), `CanUseGeirskogul`,
    `CanStarcross` (`Buffs.StarcrossReady`), `CanRiseOfTheDragon` (`Buffs.DragonsFlight`),
    `CanNastrond` (`Buffs.NastrondReady && LoTDActive`), `CanHighJump`, `CanDragonfireDive`
    (`!Buffs.DragonsFlight`), `CanStardiver` (`LoTDActive && !Buffs.StarcrossReady`).
  - **Correction vs. earlier fabricated design:** there is no "Life Window" gauge mechanic in the
    current game version. The real burst gate is `LoTDActive` (Life of the Dragon), which arms
    `Nastrond` and `Stardiver`. `FirstmindsFocus` (0–2) independently gates `WyrmwindThrust`. Any
    audit logic must use these real names.
  - `C:\Users\kruil\AppData\Roaming\Advanced Combat Tracker\FFXIVLogs\` — confirmed real log
    directory on this machine (user pointed to it directly); format not yet captured verbatim —
    Move 1 reads one real line before writing the parser regex.

### Mission (one sentence)

Given a real ACT log of a Dragoon dummy/fight, report where the player's oGCD sequence deviated
from `DRG_ST_AdvancedMode`'s real priority order (buff alignment, `LoTDActive` burst usage,
`FirstmindsFocus` spend) with a plain-English explanation per deviation — no fabricated mechanics,
no invented DPS-delta model presented as fact.

### Explicit non-goals (first slice)

- No DPS-delta estimation. Earlier draft invented potency numbers and "DPS loss" figures with no
  source; a real DPS model requires the game's damage formula (crit/DH rates, buff stacking,
  weapon damage) which this slice does not have. Report **what deviated**, not **how many DPS**.
- No FFLogs JSON support — user's data source is ACT logs on this machine. Add FFLogs later if
  actually needed.
- No other jobs. DRG only, using the real `DRG_ST_AdvancedMode` class as ground truth.
- No changes to any `.cs` file or the plugin build. This tool is read-only with respect to
  `WrathCombo/`.
- No AoE rotation (`DRG_AoE_AdvancedMode`) — single-target only, matching the dummy test case.

---

## 1 · Design contract

### Step 0 (blocking): capture one real log line

Before writing any parser, read the actual most-recent file in
`C:\Users\kruil\AppData\Roaming\Advanced Combat Tracker\FFXIVLogs\` and print 20 raw lines
verbatim. The field layout (column count, delimiter, timestamp format, ability-name spelling,
actor-name field) is unknown until then — every previous version of this parser in this
conversation guessed the format and was never validated against real bytes. **Do not write
`ACTLogNormalizer` regex until this step's output is inspected.**

### Core (pure — new folder `tools/drg-parse-audit/`)

```
tools/drg-parse-audit/
  rotation_spec.py     # real DRG_ST_AdvancedMode priority order, transcribed from DRG.cs/DRG_Helper.cs
  log_parser.py         # ACT log line -> CombatAction, built from Step 0's real sample
  audit.py               # trace actual vs. expected priority order, emit deviations
  cli.py                    # `python -m drg-parse-audit <log_path>` entry point
  tests/
    test_log_parser.py    # parses a captured real sample fixture (not synthetic)
    test_audit.py           # deviation detection on hand-built CombatAction sequences
```

`rotation_spec.py` transcribes the **real** priority order as an ordered list of
`(condition_name, ability_name, source_line)` tuples, e.g.:

```python
DRG_ST_PRIORITY = [
    # source: WrathCombo/Combos/PvE/DRG/DRG.cs:188-224 (inside CanDRGWeave() block)
    Rule("LanceCharge",   condition="CanLanceCharge",   source="DRG.cs:188-189"),
    Rule("BattleLitany",  condition="CanBattleLitany",  source="DRG.cs:192-193"),
    Rule("LifeSurge",     condition="CanLifeSurge",     source="DRG.cs:195-197"),
    Rule("MirageDive",    condition="CanMirageDive",    source="DRG.cs:202-204"),
    Rule("Geirskogul",    condition="CanUseGeirskogul", source="DRG.cs:206-208"),
    Rule("WyrmwindThrust",condition="CanUseWyrmwind",   source="DRG.cs:210-212"),
    Rule("Starcross",     condition="CanStarcross",     source="DRG.cs:214-216"),
    Rule("RiseOfTheDragon",condition="CanRiseOfTheDragon", source="DRG.cs:218-220"),
    Rule("Nastrond",      condition="CanNastrond",      source="DRG.cs:222-224"),
    # second CanDRGWeave(0.8f) pass:
    Rule("HighJump/MirageDive-hook", condition="CanHighJump", source="DRG.cs:250-252"),
    Rule("DragonfireDive",condition="CanDragonfireDive", source="DRG.cs:254-257"),
    # CanDRGWeave(1.5f, true) pass:
    Rule("Stardiver",     condition="CanStardiver",     source="DRG.cs:260-263"),
]
```

Each `condition` is documented in `rotation_spec.py` as a **plain-English restatement** of the
real `Can*` method (e.g. `CanNastrond`: "Nastrond is off cooldown, the Nastrond-ready proc is up,
and Life of the Dragon is active"), not a re-derived formula — this tool never recomputes game
math it cannot verify (crit rates, exact HP thresholds the user configured).

### Audit logic (`audit.py`)

For each observed oGCD cast in the log:

1. At that timestamp, determine which `DRG_ST_PRIORITY` rules *would* have been eligible, using
   only observable state from the log (buff applications/removals, gauge events if ACT exposes
   them, ability off-cooldown inference from prior-cast timestamps).
2. If the cast matches the **highest-eligible** rule → no issue.
3. If a **higher-priority** rule was eligible but not cast → flag `SkippedHigherPriority` with
   the rule name, real source line, and the plain-English condition text (not a DPS number).
4. If gauge/buff state can't be inferred from the log (ACT doesn't expose `FirstmindsFocus` or
   `LoTDActive` directly in the base log line) → flag `Unverifiable` rather than guessing. This
   is a real limitation to surface, not paper over.

### Config surface

None. CLI-only, single positional arg (log path), optional `--player <name>` (log's actor-name
field, captured at Step 0).

---

## 2 · Move sequence

### MOVE 0 — Preflight baseline (read-only)

```powershell
git status --porcelain    # note: WILL show existing unrelated WIP; confirm no tools/ dir yet
python3 --version           # confirm Python available
```

- **TRIGGER — `tools/drg-parse-audit/` already exists:** stop, reconcile (ABORT-ENV).

### MOVE 1 — Capture real log format (blocking on everything else)

- Find latest file in `C:\Users\kruil\AppData\Roaming\Advanced Combat Tracker\FFXIVLogs\`.
- Print 20 raw lines verbatim, plus the exact filename/extension.
- Identify: delimiter, timestamp format, which field is ability name, which is actor name, how
  the user's own character is denoted (may be `You`, may be actual character name — do not
  assume).
- **PASS:** a real sample is saved to `tools/drg-parse-audit/tests/fixtures/sample_real.log`
  (first ~200 lines of the real file, or the whole dummy pull if short) for use as the test
  fixture. Ask the user before including it if the filename/content might contain other players'
  names — trim to solo-dummy lines if so.

### MOVE 2 — Real rotation spec (pure, no I/O)

- Write `rotation_spec.py` transcribing the priority list above, each `Rule` citing its real
  `DRG.cs`/`DRG_Helper.cs` line range from Step 0's reads in this plan.
- **PASS:** no game-math invention; every `Rule.condition` text traceable to a real `Can*` method
  read in this session.

### MOVE 3 — Log parser, built from the real sample

- Write `log_parser.py` against the actual fixture from MOVE 1 — not a guessed format.
- **PASS:** `test_log_parser.py` parses `sample_real.log` and asserts the exact ability names and
  timestamps for the first 10 real actions, hand-verified against the raw file.

### MOVE 4 — Audit engine

- Write `audit.py` per the design contract's 4-step logic.
- **PASS:** `test_audit.py` covers: a clean sequence (no issues), a skipped-higher-priority case,
  and an `Unverifiable` case (gauge state not observable) — each built from real ability names
  only.

### MOVE 5 — CLI

- Write `cli.py`: `python -m drg-parse-audit <log_path> [--player NAME]` → prints each issue with
  timestamp, real source line, plain-English condition, and severity tier
  (`SkippedHigherPriority` vs `Unverifiable`).
- **PASS:** running against the real dummy log (MOVE 1's fixture) produces output the user
  confirms is legible and matches what they remember doing in that pull.

---

## 3 · Verification

All from `C:\Users\kruil\orca\ParseLord5`.

1. **Unit tests:** `python3 -m pytest tools/drg-parse-audit/tests/ -v` → all pass.
2. **Real-data smoke test:** `python3 -m drg-parse-audit "<latest ACT log path>"` → non-empty,
   legible output; user eyeballs it against their memory of the pull.
3. **No plugin impact:** `dotnet build WrathCombo/WrathCombo.csproj -c Release` still 0/0 (this
   tool touches nothing under `WrathCombo/`).
4. **Red check:** temporarily mutate one `Rule` priority order in `rotation_spec.py` (swap two
   entries) and confirm a previously-clean sequence now flags `SkippedHigherPriority`; restore.

---

## 4 · Trigger forks

- **ABORT-ENV:** the real ACT log format doesn't expose enough fields to infer *any* oGCD casts
  (e.g. only damage numbers, no ability names). Counter: check for a companion Plugin Combatant
  log or ACT plugin export with ability-level detail before abandoning.
- **ABORT-SCOPE:** gauge/buff state (`LoTDActive`, `FirstmindsFocus`, proc buffs) turns out to be
  fully unobservable from the base ACT log. Counter: report `Unverifiable` for every oGCD (still
  useful for GCD-combo-adherence checks) rather than fabricating buff state — do not invent a
  buff-tracking model not backed by the log.
- **TRIGGER-1 (real log format differs wildly from any FFXIV ACT plugin convention we know):**
  stop after MOVE 1, show the user the raw sample, ask before guessing a parser.

## 5 · Deferred (future slices, not this plan)

- **DPS-delta estimation:** requires a real damage-formula model (crit/DH rates, buff stacking) —
  deferred until there's a verified source for potency/formula data, not invented numbers.
- **FFLogs JSON input:** deferred — no evidence user has a real export to validate against yet.
- **Other jobs:** deferred until DRG slice is proven against a real pull.
- **DPS overlay / in-game display:** would require actual PL5/Dalamput integration; deferred, out
  of scope for an offline analysis tool.
</content>
