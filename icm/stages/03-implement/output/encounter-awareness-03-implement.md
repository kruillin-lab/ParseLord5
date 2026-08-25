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
# Implement — Encounter Awareness (predictive healer raidwide + read-only IPC)

- `MechanicCastTracker` gained `HasImminentImpact(MechanicCastKind kind, float withinSeconds)`; the existing
  kind-agnostic `HasImminentImpact(float)`, `Update()`, `Last`, and `PredictedSpikeFraction()` are untouched.
- `HandleRaidwide`'s `numberOfCasts` is now `Math.Max(numberOfCasts, 2)` when
  `ParseLord5Experiments.PredictiveMechanics` is on **and** a `MechanicCastKind.Raidwide` is predicted within
  `PredictiveHealerLeadSeconds` (`3f`). Additive only — the HP-threshold switch (`<=30 => 3`, `<=60 => 2`) still
  computes the floor, so a hurt party is unaffected; only a healthy party gains a pre-impact second shield.
- Kind-scoping is load-bearing: `Tankbuster`/`Cleave` are single-target and must never inflate a party-wide heal
  count. Pinned by `PredictiveMechanics_HealerRaidwideCountFeedIsAdditiveAndKindScoped`, which also asserts the
  lead constant stays `<= MechanicCastClassifier.MaxLeadSeconds` (6f).
- `ShouldHandleHealerRaidwides` and the `AutoRotationController.cs` L526 gate were **not** touched — the
  pre-existing `RotationStructureTests` assertion on that substring stays green, and
  `PredictiveMechanics_HealerRaidwideGateUnchanged` now re-pins it plus both context predicates.
- Four read-only `[EzIPC]` getters live in a new `#region Encounter Awareness (ParseLord5)` in `Provider.cs`
  (between *Extra Job State Checks* and *Fine-Grained Combo Methods*): `GetNextMechanicImminent(float)`,
  `GetNextMechanicKind()`, `GetNextMechanicTimeToImpact()`, `GetPredictedMechanicSpikeFraction()`.
- No flag re-check inside the getters: `MechanicCastTracker.Update()` already resets `_last` to `default` when the
  feature is off, so they return `false`/`"None"`/`0f` naturally. `MechanicCastKind` stays `internal`; the IPC
  surface is `bool`/`string`/`float` only.
- `IPC_NextMechanicGettersAreReadOnly` asserts all four signatures exist and none takes a `Guid` — a lease
  parameter would turn a read into a control surface.
- No new config surface: this reuses `ParseLord5Experiments.PredictiveMechanics` verbatim, so the tank spike feed
  and the healer count boost share one kill-switch.
- 215 insertions, 0 deletions. Build 0/0, 67 tests (64 → 67), evals 14/14, gate PASS_WITH_WARNINGS (expected
  test-critical-file-changed notices).
