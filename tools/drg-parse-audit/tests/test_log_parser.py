"""
Tests for log_parser.py, built against a real captured ACT log
(tests/fixtures/sample_real.log — a real Dragoon dummy pull, 2026-08-15).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from log_parser import parse_log

FIXTURE = os.path.join(os.path.dirname(__file__), "fixtures", "sample_real.log")


def test_parses_real_fixture_nonempty():
    casts, buffs = parse_log(FIXTURE, player_name="Maomi Gato")
    assert len(casts) > 0
    assert len(buffs) > 0


def test_first_ten_real_casts_match_verbatim():
    """
    Hand-verified against the raw fixture file: the real opener sequence.
    """
    casts, _ = parse_log(FIXTURE, player_name="Maomi Gato")
    expected = [
        "attack", "Lance Barrage", "attack", "Heavens' Thrust", "attack",
        "Fang and Claw", "attack", "Drakesbane", "attack", "Raiden Thrust",
    ]
    actual = [c.ability_name for c in casts[:10]]
    assert actual == expected


def test_casts_sorted_by_timestamp():
    casts, _ = parse_log(FIXTURE, player_name="Maomi Gato")
    timestamps = [c.timestamp_s for c in casts]
    assert timestamps == sorted(timestamps)


def test_filters_to_named_player_only():
    """The fixture is a solo dummy pull, but the parser must not silently
    include the Striking Dummy's own DoT-tick / status lines as player casts."""
    casts, _ = parse_log(FIXTURE, player_name="Maomi Gato")
    assert all(c.caster_name == "Maomi Gato" for c in casts)


def test_nastrond_ready_proc_gain_and_loss_both_captured():
    """Real proc: Geirskogul grants Nastrond Ready, Nastrond consumes it."""
    _, buffs = parse_log(FIXTURE, player_name="Maomi Gato")
    nastrond_events = [b for b in buffs if b.buff_name == "Nastrond Ready"]
    assert len(nastrond_events) == 2
    assert nastrond_events[0].is_gain is True
    assert nastrond_events[1].is_gain is False
    assert nastrond_events[1].timestamp_s > nastrond_events[0].timestamp_s
