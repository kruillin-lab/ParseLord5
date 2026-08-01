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
# Goal-workflow smoke test (ParseLord5)

## Objective

Prove the Cursor goal-workflow skill and GoalBuddy visual board work in this repo: durable board files, live browser board, and a read-only Scout pass that documents how to verify the project.

## Original Request

Run goal prep + visual board in ParseLord5 as a test.

## Intake Summary

- Input shape: `specific`
- Audience: operator (kruil)
- Authority: `requested`
- Proof type: `artifact`
- Completion proof: Board URL loads; `state.yaml` drives live columns; Scout receipt lists repo map + `dotnet build` verification path; final audit records `full_outcome_complete: true`
- Goal oracle: `http://goalbuddy.localhost:41737/workflow-smoke-test/` shows tasks moving through columns as receipts are written, and Scout documents a successful or documented build command for `WrathCombo.csproj`
- Likely misfire: Creating board files but never starting the server, or marking done without opening the board
- Blind spots considered: XIV/Dalamud plugin cannot be fully runtime-tested in CI; smoke test is repo + build verification only
- Existing plan facts: Build per AGENTS.md — `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`

## Goal Oracle

1. Visual board at `http://goalbuddy.localhost:41737/workflow-smoke-test/` is reachable and reflects `state.yaml`.
2. Scout receipt includes: repo purpose, key paths, verification command, and 1–3 improvement candidates (no implementation required for this smoke test).

## Goal Kind

`audit` (read-only tranche; Worker optional only if operator approves follow-up)

## Current Tranche

Validate workflow plumbing in ParseLord5 only — no product feature work unless explicitly approved after prep.

## Non-Negotiable Constraints

- Do not change WrathCombo logic during prep or Scout.
- Dalamud/XIV runtime testing out of scope for this smoke test.

## Stop Rule

Stop when T999 audit confirms oracle satisfied for this smoke test.

## Canonical Board

`docs/goals/workflow-smoke-test/state.yaml`

## Execute Command

```text
Goal execute: Follow docs/goals/workflow-smoke-test/goal.md
```
