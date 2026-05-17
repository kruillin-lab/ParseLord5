---
tags:
  - type/roadmap
  - project/parselord5
  - status/active
type: roadmap
project: parselord5
status: active
aliases: []
---
# ParseLord5 Roadmap

ParseLord5 will be developed as a WrathCombo fork.

## Direction

- WrathCombo is the base.
- RSR is not the base.
- Do not directly import RSR logic.
- Future changes should be feature-flagged.
- Existing WrathCombo behavior should remain unchanged unless ParseLord5 experimental mode is enabled.
- Job tuning should happen one job at a time.

## Near-Term Milestones

1. Baseline fork build.
2. Minimal identity separation.
3. Architecture map of WrathCombo combo, feature, and auto-rotation systems.
4. Add ParseLord5 experimental mode behind a config flag.
5. Add improved debug tracing for one selected job.
6. Tune one job at a time using WrathCombo's existing style.
7. Only after stable tuning, consider deeper architecture changes.

## First Experiment Job

- Do not begin with Bard, Ninja, Monk, or Viper.
- Recommended first job for ParseLord5 experiments: Warrior or Dragoon.
- Warrior is cleaner for learning the fork.
- Dragoon is better if comparing against previous ParseLord work.

## Guardrails

- Do not rewrite WrathCombo architecture up front.
- Do not change combat execution behavior without a separate plan.
- Do not change auto-rotation behavior without a separate plan.
- Do not begin job tuning until baseline, identity, architecture map, and experimental flag are complete.
