"""
Real DRG_ST_AdvancedMode rotation facts, transcribed from the actual plugin source.

Every entry cites the exact WrathCombo source line range it was read from in this
session. Nothing here is invented: potencies are NOT modeled (no verified source for
the current damage formula), only priority order and proc/buff gating, which are
directly readable from the real `Can*` gate functions.

Sources:
  WrathCombo/Combos/PvE/DRG/DRG.cs:158-285        (DRG_ST_AdvancedMode.Invoke)
  WrathCombo/Combos/PvE/DRG/DRG_Helper.cs:21-95    (DoBasicCombo - GCD chain)
  WrathCombo/Combos/PvE/DRG/DRG_Helper.cs:101-258  (Can* oGCD gates)
"""
from dataclasses import dataclass
from typing import Optional


@dataclass(frozen=True)
class ProcRule:
    """A ready-proc gate: buff X must be up to cast ability Y."""
    proc_buff: str       # exact buff name as it appears in the ACT log (type 26/30 lines)
    consumer_ability: str  # exact ability name as it appears in the ACT log (type 21 lines)
    source: str           # DRG.cs / DRG_Helper.cs line citation
    note: str


# Real oGCD priority order inside CanDRGWeave(), first pass — DRG.cs:174-244
OGCD_PRIORITY_FIRST_PASS = [
    ("Lance Charge",    "DRG.cs:188-189 / DRG_Helper.cs:185-187 CanLanceCharge"),
    ("Battle Litany",   "DRG.cs:192-193 / DRG_Helper.cs:182-183 CanBattleLitany"),
    ("Life Surge",      "DRG.cs:195-197 / DRG_Helper.cs:101-147 CanLifeSurge"),
    ("Mirage Dive",     "DRG.cs:202-204 / DRG_Helper.cs:198-212 CanMirageDive"),
    ("Geirskogul",      "DRG.cs:206-208 / DRG_Helper.cs:213-224 CanUseGeirskogul"),
    ("Wyrmwind Thrust", "DRG.cs:210-212 / DRG_Helper.cs:189-197 CanUseWyrmwind"),
    ("Starcross",       "DRG.cs:214-216 / DRG_Helper.cs:226-227 CanStarcross"),
    ("Rise of the Dragon", "DRG.cs:218-220 / DRG_Helper.cs:229-230 CanRiseOfTheDragon"),
    ("Nastrond",        "DRG.cs:222-224 / DRG_Helper.cs:232-233 CanNastrond"),
]

# Second pass — DRG.cs:246-263, gated by a shallower CanDRGWeave(0.8f) / (1.5f, true)
OGCD_PRIORITY_SECOND_PASS = [
    ("High Jump",       "DRG.cs:250-252 / DRG_Helper.cs:235-245 CanHighJump"),
    ("Dragonfire Dive", "DRG.cs:254-257 / DRG_Helper.cs:247-253 CanDragonfireDive"),
    ("Stardiver",       "DRG.cs:260-263 / DRG_Helper.cs:255-257 CanStardiver"),
]

# Real ready-proc → consumer relationships. Each is directly observable in an ACT log
# as a type-26 buff-gain line for `proc_buff`, followed by a type-21 cast of
# `consumer_ability`, followed by a type-30 buff-loss line for `proc_buff`.
PROC_RULES = [
    ProcRule(
        proc_buff="Nastrond Ready",
        consumer_ability="Nastrond",
        source="DRG_Helper.cs:232-233 CanNastrond requires HasStatusEffect(Buffs.NastrondReady)",
        note="Geirskogul grants this 20s proc; DRG.cs priority 222-224 puts Nastrond above "
             "High Jump/Dragonfire Dive/Stardiver — holding it past its 20s window is a real loss.",
    ),
    ProcRule(
        proc_buff="Starcross Ready",
        consumer_ability="Starcross",
        source="DRG_Helper.cs:226-227 CanStarcross requires HasStatusEffect(Buffs.StarcrossReady)",
        note="Stardiver grants this 20s proc (DRG_Helper.cs:255-257 CanStardiver).",
    ),
    ProcRule(
        proc_buff="Dive Ready",
        consumer_ability="Mirage Dive",
        source="DRG_Helper.cs:198-212 CanMirageDive requires HasStatusEffect(Buffs.DiveReady)",
        note="High Jump grants this proc (DRG_Helper.cs:235-245 CanHighJump).",
    ),
    ProcRule(
        proc_buff="Dragon's Flight",
        consumer_ability="Rise of the Dragon",
        source="DRG_Helper.cs:229-230 CanRiseOfTheDragon requires HasStatusEffect(Buffs.DragonsFlight)",
        note="Dragonfire Dive grants this proc (DRG_Helper.cs:247-253 CanDragonfireDive requires "
             "!HasStatusEffect(Buffs.DragonsFlight), i.e. it won't refire while the proc is still up).",
    ),
]

# Real GCD combo chain (single-target, non-AoE) — DRG_Helper.cs:56-94 DoBasicCombo.
# Positional/TrueNorth branching omitted (not reliably observable from a base ACT log
# without positional data); this captures the linear finisher sequence only.
GCD_COMBO_CHAIN = [
    "TrueThrust",   # or Raiden Thrust (OriginalHook(TrueThrust) opener replacement)
    "Vorpal Thrust",  # or Disembowel branch depending on ChaosThrust debuff state
    "Heavens' Thrust",  # OriginalHook(ChaosThrust) at current level
    "Fang and Claw",   # or Wheeling Thrust depending on flank/rear position
    "Drakesbane",
]

BUFF_ABILITIES = {"Lance Charge", "Battle Litany"}  # DRG.cs:178-198
