---
tags:
  - type/note
  - project/parselord5
  - status/active
type: note
project: parselord5
status: active
aliases: []
---
# Implement — COA-2 probe/execution split

- `InvokeCombo` now takes `bool selectingAutorotAction = false`.
- Flag assignment is `IsSelectingAutorotAction = selectingAutorotAction`.
- Only `actCheck` opts in with `selectingAutorotAction: true`.
- ExecuteAoE heal / ExecuteAoE DPS / ExecuteST stay 4-arg execution calls.
- `AutorotationProbeContext_IsOptIn` pins the contract (`Assert.Single` instead of `Assert.Equal(1, …)` to avoid xUnit2013).
