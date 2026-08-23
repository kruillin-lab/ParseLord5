---
tags:
  - type/plan
  - project/parselord5
  - status/active
type: plan
project: parselord5
status: active
aliases: []
---
# ParseLord5 Stability Wargame Prompt

WARGAME ORDER. You are not executing this mission. You are wargaming it. A cheaper executor runs the brief below later. Your job is to produce the route it will follow.

Recon first, read-only:

- Read `/home/kruillin/Projects/Projects/ParseLord5/AGENTS.md`.
- Read `docs/AGENTS_FULL.md` if it exists and is relevant to build or repo workflow.
- Read `README.md`, `advisor-plan-prompt.md`, `WrathCombo/WrathCombo.csproj`, and the code paths needed to trace combat combo execution.
- Pay special attention to recently modified tank helper files if present: `WrathCombo/Combos/PvE/GNB/GNB_Helper.cs` and `WrathCombo/Combos/PvE/WAR/WAR_Helper.cs`.
- Do not edit files during recon.

Then fight the mission on paper, move by move, and write the battle plan to `wargames/parselord5-stability-battle-plan.md`:

- Every move states its expected observation, exactly what the executor should see if it worked.
- Every move carries its most likely failure, the cause it signals, and the counter-move.
- Every fork gets a trigger: if the executor observes X, take route B.
- Assumptions recon could not settle get marked `RECON NEEDED` with the exact check that settles it.
- End with abort conditions and verification runs, including what pass looks like for each.
- Keep the plan executable by a mid-tier coding model without asking follow-up questions.

=== THE MISSION BRIEF (the executor's orders, not yours) ===

Repository: `/home/kruillin/Projects/Projects/ParseLord5`.

Goal: hunt and fix the top 3 real stability or combat-logic defects in ParseLord5, a Dalamud plugin built on WrathCombo targeting .NET 10 and Dalamud API 15.

Before touching anything, trace the core flow from Dalamud plugin load through combo selection, player state inspection, and action decision output. Focus on defects that can plausibly break in-game behavior, crash the plugin, spam invalid actions, miss expected oGCD/song/tank actions, or desynchronize UI/config state from runtime behavior.

Rules:

- No style nits.
- No broad refactors.
- No speculative hardening for scenarios that cannot happen.
- Do not overwrite user changes.
- Fix only the top 3 evidence-backed findings.
- Each finding must cite file and line, explain the failure scenario in one sentence, rate severity, and include proof from a failing test, reproduction command, build diagnostic, or concrete trace through the code.
- If the issue requires a live FFXIV/Dalamud environment to confirm, mark the live step as `MANUAL VERIFY` and provide the exact in-game check.

Required verification:

- Run `dotnet build ./WrathCombo/WrathCombo.csproj -c Release` if the local environment supports it.
- Run any focused test project that exists and applies to changed code.
- If build/test cannot run on this machine, capture the exact error and provide the smallest next command for the user to run in the right environment.
- Final report must separate: fixed findings, unfixed evidence-backed findings, verification results, manual game checks, and residual risk.
