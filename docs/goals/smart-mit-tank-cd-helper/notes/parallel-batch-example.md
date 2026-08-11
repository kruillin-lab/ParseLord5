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
# Parallel batch example (smart-mit + TCH)

Retroactive pattern for work that could have run in parallel after Judge T002 approved scope.

## Independent streams

| Task id | parallel_group | Repo | Objective |
|---------|----------------|------|-----------|
| T010 | `integration-research` | ParseLord5 | Map SmartMitigation services + WAR bridge |
| T011 | `integration-research` | TankCooldownHelper | Map DangerCalculator + planned IPC |

No shared files; both read-only Scouts. **Depends on:** T002 done (Judge picked integration slice).

## state.yaml snippet

```yaml
rules:
  one_active_task: false
  parallel_execution:
    enabled: true
    max_parallel_tasks: 2
    allow_parallel_types: [scout]

active_task: null
active_tasks: [T010, T011]

tasks:
  - id: T010
    type: scout
    status: active
    depends_on: [T002]
    parallel_group: integration-research
    objective: "PL5 smart mit + bridge map (read-only)."
    constraints: ["Read-only.", "ParseLord5 only."]
    receipt: null
  - id: T011
    type: scout
    status: active
    depends_on: [T002]
    parallel_group: integration-research
    objective: "TCH danger telemetry + IPC contract (read-only)."
    constraints: ["Read-only.", "TankCooldownHelper only."]
    receipt: null
```

## Execute (Cursor PM)

1. Enable `parallel_execution` as above.
2. One message → two `Task` subagents (`explore`, `readonly: true`, `run_in_background: true`).
3. When both return → write receipts → `active_tasks: []` → queue **sequential** Judge T012 to merge.

## Not parallel

- WAR Worker (T003) vs TCH IPC Worker — same repo writes or ordering; one Worker at a time.
- Judge phase reviews — main thread, one at a time.

Full spec: `~/.cursor/skills/goal-workflow/reference.md` § Parallel execution.
