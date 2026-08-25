"""
Audit engine: compare a real parsed DRG log against the real proc-discipline rules
in rotation_spec.py. Reports ready-proc holding (buff gained, not spent promptly)
and any proc that expired unused.

No DPS numbers. No fabricated formulas. Only what's directly observable in the log.
"""
from __future__ import annotations
from dataclasses import dataclass
from typing import List

from log_parser import CastEvent, BuffEvent
from rotation_spec import PROC_RULES, ProcRule


@dataclass
class AuditFinding:
    proc_buff: str
    gained_at_s: float
    consumed_at_s: float | None  # None if never consumed before the proc expired
    expired_at_s: float | None
    hold_time_s: float | None    # gained_at -> consumed_at
    verdict: str                 # "ok" | "held" | "expired_unused"
    source: str


HOLD_WARNING_THRESHOLD_S = 3.0  # a real oGCD weave slot is ~2.5s (one GCD); >3s idle after a proc is a real hold


def audit_proc_discipline(casts: List[CastEvent], buffs: List[BuffEvent]) -> List[AuditFinding]:
    """
    For each ProcRule (e.g. Nastrond Ready -> Nastrond), walk every gain event and
    find the matching consumer cast and/or loss event, in order.
    """
    findings: List[AuditFinding] = []

    for rule in PROC_RULES:
        gains = [b for b in buffs if b.buff_name == rule.proc_buff and b.is_gain]
        losses = [b for b in buffs if b.buff_name == rule.proc_buff and not b.is_gain]
        consumer_casts = [c for c in casts if c.ability_name == rule.consumer_ability]

        for gain in gains:
            # matching loss: earliest loss after this gain
            loss = next((l for l in losses if l.timestamp_s > gain.timestamp_s), None)
            # matching consumer cast: earliest cast of the consumer ability strictly
            # after this gain and at/before the matching loss (or before next gain)
            window_end = loss.timestamp_s if loss else float("inf")
            consume = next(
                (c for c in consumer_casts
                 if gain.timestamp_s <= c.timestamp_s <= window_end + 0.5),
                None,
            )

            if consume is None:
                findings.append(AuditFinding(
                    proc_buff=rule.proc_buff,
                    gained_at_s=gain.timestamp_s,
                    consumed_at_s=None,
                    expired_at_s=loss.timestamp_s if loss else None,
                    hold_time_s=None,
                    verdict="expired_unused",
                    source=rule.source,
                ))
                continue

            hold = consume.timestamp_s - gain.timestamp_s
            verdict = "held" if hold > HOLD_WARNING_THRESHOLD_S else "ok"
            findings.append(AuditFinding(
                proc_buff=rule.proc_buff,
                gained_at_s=gain.timestamp_s,
                consumed_at_s=consume.timestamp_s,
                expired_at_s=loss.timestamp_s if loss else None,
                hold_time_s=hold,
                verdict=verdict,
                source=rule.source,
            ))

    findings.sort(key=lambda f: f.gained_at_s)
    return findings


def format_report(findings: List[AuditFinding], fight_start_s: float) -> str:
    lines = []
    issues = [f for f in findings if f.verdict != "ok"]
    lines.append(f"Proc-discipline audit: {len(findings)} ready-procs observed, "
                 f"{len(issues)} with a timing issue.\n")

    for f in findings:
        rel_t = f.gained_at_s - fight_start_s
        if f.verdict == "ok":
            lines.append(f"  [{rel_t:6.1f}s] OK      {f.proc_buff:<18} -> consumed after "
                         f"{f.hold_time_s:.1f}s")
        elif f.verdict == "held":
            lines.append(f"  [{rel_t:6.1f}s] HELD    {f.proc_buff:<18} -> not consumed for "
                         f"{f.hold_time_s:.1f}s (weave slot is ~2.5s; this delayed the proc)")
            lines.append(f"           source: {f.source}")
        else:
            lines.append(f"  [{rel_t:6.1f}s] EXPIRED {f.proc_buff:<18} -> never consumed before "
                         f"the proc fell off")
            lines.append(f"           source: {f.source}")

    return "\n".join(lines)
