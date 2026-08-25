"""
Tests for audit.py — deviation detection on the real fixture plus hand-built
CombatAction/BuffEvent sequences for edge cases.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from log_parser import parse_log, CastEvent, BuffEvent
from audit import audit_proc_discipline

FIXTURE = os.path.join(os.path.dirname(__file__), "fixtures", "sample_real.log")


def test_real_fixture_finds_five_tracked_procs():
    casts, buffs = parse_log(FIXTURE, player_name="Maomi Gato")
    findings = audit_proc_discipline(casts, buffs)
    # Real fixture has exactly 5 gain events across the 4 tracked proc buffs
    # (Dive Ready fires twice: High Jump at t=24.7s and t=54.8s relative).
    assert len(findings) == 5


def test_real_fixture_flags_near_expired_dive_ready():
    """
    Ground truth: the real fixture's first Dive Ready (from High Jump at
    22:37:54.670) is a 15s-duration proc consumed by Mirage Dive at
    22:38:09.385 — a 14.7s hold, right at the wire. This must be flagged.
    """
    casts, buffs = parse_log(FIXTURE, player_name="Maomi Gato")
    findings = audit_proc_discipline(casts, buffs)
    dive_findings = [f for f in findings if f.proc_buff == "Dive Ready"]
    assert len(dive_findings) == 2
    first_hold = dive_findings[0]
    assert first_hold.verdict == "held"
    assert first_hold.hold_time_s > 10.0  # real, verified: ~14.7s


def test_clean_immediate_consume_is_ok():
    """Synthetic: proc gained and consumed within one weave slot -> ok."""
    casts = [CastEvent(timestamp_s=100.0, caster_name="P", ability_name="Nastrond",
                        target_name="Dummy")]
    buffs = [
        BuffEvent(timestamp_s=99.0, buff_name="Nastrond Ready", is_gain=True,
                  source_name="P", target_name="P"),
        BuffEvent(timestamp_s=110.0, buff_name="Nastrond Ready", is_gain=False,
                  source_name="P", target_name="P"),
    ]
    findings = audit_proc_discipline(casts, buffs)
    assert len(findings) == 1
    assert findings[0].verdict == "ok"
    assert findings[0].hold_time_s == 1.0


def test_proc_expired_completely_unused():
    """Synthetic: proc gained, lost, never consumed -> expired_unused, no fabricated cast."""
    casts: list[CastEvent] = []  # player never cast the consumer ability at all
    buffs = [
        BuffEvent(timestamp_s=50.0, buff_name="Starcross Ready", is_gain=True,
                  source_name="P", target_name="P"),
        BuffEvent(timestamp_s=70.0, buff_name="Starcross Ready", is_gain=False,
                  source_name="P", target_name="P"),
    ]
    findings = audit_proc_discipline(casts, buffs)
    assert len(findings) == 1
    assert findings[0].verdict == "expired_unused"
    assert findings[0].consumed_at_s is None
