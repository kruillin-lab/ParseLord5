"""
Parser for the real FFXIV_ACT_Plugin "Network" log format.

Verified against tests/fixtures/sample_real.log — a real Dragoon dummy pull captured
from C:\\Users\\kruil\\AppData\\Roaming\\Advanced Combat Tracker\\FFXIVLogs\\ on 2026-08-15.

Format (pipe-delimited, fields vary by type code in field 0):
  Type 21/22 — ability use (single/AoE target):
    21|timestamp|casterID|casterName|abilityIdHex|abilityName|targetID|targetName|flags|...
  Type 26 — buff/debuff GAIN:
    26|timestamp|buffIdHex|buffName|durationSeconds|sourceID|sourceName|targetID|targetName|...
  Type 30 — buff/debuff LOSS (durationSeconds field reads 0.00):
    30|timestamp|buffIdHex|buffName|0.00|sourceID|sourceName|targetID|targetName|...
"""
from __future__ import annotations
import re
from dataclasses import dataclass
from datetime import datetime
from typing import List, Optional


TIMESTAMP_RE = re.compile(r"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+)")


def _parse_ts(raw: str) -> float:
    """Parse an ACT ISO-ish timestamp (with timezone offset) to epoch seconds."""
    # Format: 2026-08-15T22:37:29.9440000-04:00
    dt = datetime.strptime(raw[:26], "%Y-%m-%dT%H:%M:%S.%f")
    # Timezone offset is constant within one log; absolute epoch not needed —
    # relative ordering and deltas are all this tool uses.
    return dt.timestamp()


@dataclass
class CastEvent:
    timestamp_s: float
    caster_name: str
    ability_name: str
    target_name: str


@dataclass
class BuffEvent:
    timestamp_s: float
    buff_name: str
    is_gain: bool  # True = type 26 (gain), False = type 30 (loss)
    source_name: str
    target_name: str


def parse_log(path: str, player_name: str) -> tuple[List[CastEvent], List[BuffEvent]]:
    """
    Parse a real ACT network log file, filtered to one player's casts and buff events.

    Args:
        path: path to the .log file (or a fixture excerpt in the same format).
        player_name: the in-game character name as it appears in the log
            (e.g. "Maomi Gato") — NOT "You" (that spelling only appears in the
            human-readable type-00 chat lines, not the structured type-21/26/30 lines).

    Returns:
        (casts, buffs) — both sorted by timestamp, filtered to events where the
        player is the caster (casts) or source==target==player (self-buffs) or
        source==player (debuffs applied to a target, e.g. Chaotic Spring).
    """
    casts: List[CastEvent] = []
    buffs: List[BuffEvent] = []

    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line:
                continue
            fields = line.split("|")
            if len(fields) < 5:
                continue

            type_code = fields[0]

            if type_code in ("21", "22"):
                # 21|ts|casterID|casterName|abilityIdHex|abilityName|targetID|targetName|...
                if len(fields) < 8:
                    continue
                ts_raw, caster_name, ability_name, target_name = (
                    fields[1], fields[3], fields[5], fields[7]
                )
                if caster_name != player_name:
                    continue
                casts.append(CastEvent(
                    timestamp_s=_parse_ts(ts_raw),
                    caster_name=caster_name,
                    ability_name=ability_name,
                    target_name=target_name,
                ))

            elif type_code in ("26", "30"):
                # 26|ts|buffIdHex|buffName|duration|sourceID|sourceName|targetID|targetName|...
                if len(fields) < 9:
                    continue
                ts_raw, buff_name, source_name, target_name = (
                    fields[1], fields[3], fields[6], fields[8]
                )
                if source_name != player_name:
                    continue
                buffs.append(BuffEvent(
                    timestamp_s=_parse_ts(ts_raw),
                    buff_name=buff_name,
                    is_gain=(type_code == "26"),
                    source_name=source_name,
                    target_name=target_name,
                ))

    casts.sort(key=lambda c: c.timestamp_s)
    buffs.sort(key=lambda b: b.timestamp_s)
    return casts, buffs
