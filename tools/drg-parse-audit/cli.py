"""
CLI entry point: python cli.py <log_path> --player "Character Name"
"""
import argparse
import sys

from log_parser import parse_log
from audit import audit_proc_discipline, format_report


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Audit a real FFXIV ACT log for Dragoon ready-proc discipline "
                     "against the real DRG_ST_AdvancedMode priority order."
    )
    parser.add_argument("log_path", help="Path to an ACT Network_*.log file")
    parser.add_argument("--player", required=True,
                         help='In-game character name as it appears in the log '
                              '(e.g. "Maomi Gato" — not "You")')
    args = parser.parse_args()

    casts, buffs = parse_log(args.log_path, player_name=args.player)

    if not casts:
        print(f"No Dragoon actions found for player '{args.player}' in {args.log_path}",
              file=sys.stderr)
        return 1

    findings = audit_proc_discipline(casts, buffs)
    report = format_report(findings, fight_start_s=casts[0].timestamp_s)

    print(f"Player: {args.player}")
    print(f"Actions parsed: {len(casts)}")
    print(f"Fight duration: {casts[-1].timestamp_s - casts[0].timestamp_s:.1f}s\n")
    print(report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
