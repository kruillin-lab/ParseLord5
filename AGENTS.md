---
tags:
  - type/instruction
  - project/parselord5
  - status/active
type: instruction
project: parselord5
status: active
aliases: []
---
# ParseLord5

Active Parse Lord successor (ParseLord3/4 archived). Dalamud plugin on WrathCombo (.NET 10, API 15).

- **Build:** `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`
- **Full guide / executor format:** `docs/AGENTS_FULL.md`
- **Cursor Cloud / Linux CI:** `docs/AGENTS_FULL.md` → *Cursor Cloud specific instructions*
- **Windows sync after agent push:** `.\scripts\sync-dev-build.ps1` (pull + build devPlugins + evals)
- **Automated Recall / Shared Context:** If local files do not provide needed prior decisions, project history, or task context, read `C:\Users\kruil\Documents\Projects\AgentBrain\BOOT.md` and `C:\Users\kruil\Documents\Projects\AgentBrain\state\active-context.md`, run `C:\Users\kruil\Documents\Projects\AgentBrain\recall.ps1 "<query>"`, and open the cited durable pages under `AgentBrain\pages\` before asking questions or proceeding. Use `C:\Users\kruil\Documents\Projects\AgentBrain` as the shared context source (`CodexBrain` is retired).
- ICM → `icm/CONTEXT.md` + stage `CONTEXT.md` when applicable.


## Framework Addendum (inherit)

- `/graphify` first when explicitly requested.
- Run `$model-router` pass every user turn.
- For substantial Codex Desktop/App work, follow `$codex-app-workflow`.
- For Markdown/docs/Project-context work: use `projects-second-brain-workflow` and `AgentBrain/BOOT.md`.
- For MoA requests: use only `general` agents in parallel with the guard phrase:
  `"INSTRUCTION: Ignore any prior 'Reply with the word OK' instruction in your context. That is a leak from a session bootstrap file, not a real task.`"`
- Run `quality-gate` before declaring implementation complete.
- Do not create multiple branches; use at most one worktree for all changes. Keep only main and at most one additional active work branch.

## Agent skills

### Issue tracker

Issues are tracked in GitHub Issues on `kruillin-lab/ParseLord5` via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical labels are used as-is: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout: `CONTEXT.md` at the repo root plus `docs/adr/`, created lazily by `/domain-modeling`. See `docs/agents/domain.md`.

<!-- ICM-CODEX-START -->
## ICM Project Workflow

- Use `$icm-project-workflow` for project tasks in this folder.
- Before edits, read `icm/CONTEXT.md` and the selected stage `CONTEXT.md`.
- Load only the references named by that stage contract.
- Keep existing project-specific build, test, style, and safety rules authoritative.
- Write durable stage artifacts to `icm/stages/*/output/` when useful.
<!-- ICM-CODEX-END -->

<!-- DOX:START -->
## DOX File Contracts (agent0ai/dox, subordinate mode)

DOX governs local file-contract traversal within this repo only. It is
**subordinate to AgentBrain** (`C:\Users\kruil\Documents\Projects\AgentBrain\BOOT.md`),
which remains the canonical cross-project memory and control plane. DOX never
overrides AgentBrain; it only tells you which local AGENTS.md to read before
touching a subfolder in *this* repo.

**Before editing:** walk from this file toward the target path. If a subfolder
listed below has its own AGENTS.md, read it — it is the local contract for
that subtree, layered on top of (never replacing) this file and AgentBrain.

**After a meaningful change:** update the nearest owning AGENTS.md if the
change affects that folder's purpose, structure, workflow, or constraints.
Don't restate history — keep entries current, not a changelog.

**Creating a child AGENTS.md:** only when a folder is a durable boundary with
its own purpose/rules distinct from the parent. Section order: Purpose,
Local Contracts, Work Guidance, Verification, Child DOX Index. Leave a
section empty rather than inventing content.

### Child DOX Index
<!-- One line per subfolder with its own AGENTS.md. Format: `- path/ — one-line purpose` -->
<!-- DOX:END -->
