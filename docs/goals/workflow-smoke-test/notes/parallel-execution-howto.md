---
tags:
  - type/doc
  - project/parselord5
  - status/active
type: doc
project: parselord5
status: active
aliases: []
---
# How to use parallel agents on a goal board

Smoke-test goal is **done**; this note documents the feature for the next goal prep.

## Quick steps

1. **Prep** — in `state.yaml` set `rules.parallel_execution.enabled: true` and add `depends_on` / `parallel_group` on tasks that can run together.
2. **Execute** — PM sets `active_tasks: [T010, T011]`, `active_task: null`, `one_active_task: false`.
3. **Spawn** — in one Cursor turn, launch N background Task subagents (Scouts only by default).
4. **Reconcile** — PM writes all receipts, clears `active_tasks`, continues sequentially (e.g. one Judge to merge).

## When to parallelize

| OK | Not OK |
|----|--------|
| Two Scouts, different repos or disjoint paths | Two Workers touching same files |
| Scout + Scout after shared Judge gate | Judge + Judge without readonly guard |
| Registry doc + code scout (read-only) | Build verify that must be serial |

## Defaults

New goals from `goal-workflow/templates/state.yaml` keep `parallel_execution.enabled: false` (sequential PM loop).

## Docs

- Skill: `~/.cursor/skills/goal-workflow/SKILL.md` § Parallel execution
- Reference + YAML example: `~/.cursor/skills/goal-workflow/reference.md`
- Realistic retro: `docs/goals/smart-mit-tank-cd-helper/notes/parallel-batch-example.md`

## GoalBuddy CLI

`npx goalbuddy board` watches `state.yaml`; multiple `active` tasks may show on the board. Scheduling is **not** in the CLI yet — Cursor PM follows the skill.
