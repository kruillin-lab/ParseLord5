# Plan 005: Correct stale agent/handoff docs so agents build the right tree

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat e9b9d1789..HEAD -- docs/AGENTS_FULL.md CLAUDE.md HANDOFF.md advisor-plan-prompt.md AGENTS.md`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: docs
- **Planned at**: commit `e9b9d1789`, 2026-08-25

## Why this matters

Agents following `docs/AGENTS_FULL.md` `cd` to `C:\\Users\\kruil\\orca\\ParseLord5` and build it. That tree is two commits behind this one and dirty. Both trees write the same devPlugins folder. `HANDOFF.md` and `advisor-plan-prompt.md` are `status: active` and describe work that is already in history on a deleted path. `CLAUDE.md` points at a relative `Second Brain/wiki/ParseLord5.md` that is not in this repo, and it claims SCH/AST healer DPS-lane gates exist; they do not. Wrong docs cause the exact binary collision plan 004 guards against.

Cross-project wiki (do not edit in this plan): `C:\\Users\\kruil\\Documents\\Projects\\SecondBrain\\Second Brain\\wiki\\ParseLord5.md`. Point `CLAUDE.md` at that path. Do not rewrite the wiki here.

## Current state

`docs/AGENTS_FULL.md:33-44` (wrong):

```
Repo root is `C:\\Users\\kruil\\orca\\ParseLord5`.
...
pwsh ... --repo C:\\Users\\kruil\\orca\\ParseLord5 --task "<task>"
...
Baselines at `aafddadd5` + teardown fix: **0 errors / 0 warnings**, **55 tests passed**, ...
> **Two checkouts exist on this machine.** `C:\\Users\\kruil\\Documents\\Projects\\ParseLord5` is a *stale* second clone: branch `merge-rehearsal` ... Always build from `orca\\ParseLord5`.
```

Facts at plan time:

- Canonical checkout: `C:\\Users\\kruil\\Documents\\Projects\\ffxiv-tools\\ParseLord5` on `main` at `e9b9d1789`, origin `kruillin-lab/ParseLord5`.
- Second checkout: `C:\\Users\\kruil\\orca\\ParseLord5` on `main` at `e367c8220`, dirty. Do not name it canonical.
- `C:\\Users\\kruil\\Documents\\Projects\\ParseLord5` does not exist (the merge-rehearsal warning is inverted).
- Test baseline is a command, not a count. At `e9b9d1789` the suite was Failed 2 / Passed 54; after plan 002 it should be Failed 0 / Passed 56. Do not hardcode either number.

`HANDOFF.md` frontmatter `status: active`, dated 2026-07-02, repo path `C:\\Users\\kruil\\Documents\\Projects\\ParseLord5`, branch `parselord5-wc-base`, claims uncommitted dedupe work. That work is committed. Mark archived.

`advisor-plan-prompt.md` frontmatter `status: active`, last useful in May 2026, identity-phase questions already shipped. Mark archived.

`CLAUDE.md:36` says healers WHM/SGE/SCH/AST are already fixed. WHM/SGE have `IsSelectingAutorotAction` gates. SCH/AST do not (`git log -S IsSelectingAutorotAction` on those files is empty). Change the bullet to: WHM is the reference; SGE is gated; SCH/AST raidwide helpers still run during the DPS probe when `GroupDamageIncoming()` is true — do not treat them as gated.

`CLAUDE.md:48` `Second Brain/wiki/ParseLord5.md` — replace with the absolute SecondBrain path above.

`CLAUDE.md:40` Manual targeting footgun is still true (`HealerTargeting.ManualTarget()` has no `GetLowestCurrent()` fallback). Keep it.

`CLAUDE.md:37` tank/DPS do-not-gate rule stays.

`AGENTS.md` AgentBrain pointer is still a live file on this machine. Do not retarget it in this plan.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Drift check | `git diff --stat e9b9d1789..HEAD -- docs/AGENTS_FULL.md CLAUDE.md HANDOFF.md advisor-plan-prompt.md AGENTS.md` | empty until this plan |
| Confirm orca still exists (do not build it) | `Test-Path C:\\Users\\kruil\\orca\\ParseLord5` | True |
| Confirm deleted path | `Test-Path C:\\Users\\kruil\\Documents\\Projects\\ParseLord5` | False |
| Confirm wiki | `Test-Path "C:\\Users\\kruil\\Documents\\Projects\\SecondBrain\\Second Brain\\wiki\\ParseLord5.md"` | True |

No `dotnet test` required. Docs only.

Working directory: `C:\\Users\\kruil\\Documents\\Projects\\ffxiv-tools\\ParseLord5`.

## Scope

**In scope**:
- `docs/AGENTS_FULL.md` — repo root, gate --repo path, checkout warning, drop hardcoded test counts
- `CLAUDE.md` — wiki path; SCH/AST gating sentence
- `HANDOFF.md` — frontmatter status archived; one-line banner that it is not the work queue
- `advisor-plan-prompt.md` — frontmatter status archived; one-line banner

**Out of scope**:
- `AGENTS.md`.
- SecondBrain wiki page body (SCH/AST claim lives there too; do not edit the vault in this plan).
- Creating `CONTEXT.md` or `docs/adr/`.
- Deleting HANDOFF.md or advisor-plan-prompt.md.
- `quality-gate.json`.
- Source code.

## Git workflow

- Branch: `advisor/improve-f1-f5`.
- Commit message: `docs: point agents at ffxiv-tools/ParseLord5 and archive stale handoffs`
- Do NOT push or open a PR unless the operator instructed it.
- Do not add a co-author trailer.

## Steps

### Step 1: Fix docs/AGENTS_FULL.md Windows section

Replace the sentence `Repo root is C:\\Users\\kruil\\orca\\ParseLord5.` with:

`Repo root is C:\\Users\\kruil\\Documents\\Projects\\ffxiv-tools\\ParseLord5.`

In the powershell block, change `--repo C:\\Users\\kruil\\orca\\ParseLord5` to `--repo C:\\Users\\kruil\\Documents\\Projects\\ffxiv-tools\\ParseLord5`.

Delete the hardcoded baseline sentence that names `55 tests passed` and commit `aafddadd5`. Replace with:

`Verify with the commands above. Do not treat a stored test count as a gate; run the suite.`

Replace the two-checkout blockquote with:

```
> **Two checkouts exist on this machine.** Canonical: `C:\\Users\\kruil\\Documents\\Projects\\ffxiv-tools\\ParseLord5` (this tree, origin `kruillin-lab/ParseLord5`, branch `main`). Second: `C:\\Users\\kruil\\orca\\ParseLord5` — do not build it for in-game testing; both Debug and Release write `%AppData%\\XIVLauncher\\devPlugins\\ParseLord5\\` (`WrathCombo.csproj` OutputPath). Plan 004 stamps that folder with the producing checkout. `C:\\Users\\kruil\\Documents\\Projects\\ParseLord5` does not exist.
```

If the file later mentions `Always build from orca\\ParseLord5`, delete that sentence.

**Verify**: `rg -n "orca\\\\ParseLord5" docs/AGENTS_FULL.md` still matches only as the named second checkout, never as "Repo root" or `--repo`.

### Step 2: Archive HANDOFF.md

Change frontmatter `status: active` to `status: archived`.

Immediately after the H1, insert:

`> Archived 2026-08-25. Not the work queue. Current tree is `ffxiv-tools/ParseLord5` on `main`. See `CLAUDE.md` and `plans/README.md`.`

Do not rewrite the rest. Do not delete the historical tables.

### Step 3: Archive advisor-plan-prompt.md

Same frontmatter change and a banner after the H1:

`> Archived 2026-08-25. Identity-phase questions below are shipped. Do not paste this as a live advisor prompt.`

### Step 4: Correct CLAUDE.md

Replace the healer bullet currently `Healers (WHM/SGE/SCH/AST) are already fixed.` with:

`- **WHM is the reference implementation. SGE DPS combos are gated. SCH and AST DPS combos still return raidwides (Sacred Soil / Succor / Neutral Sect / etc.) when `GroupDamageIncoming()` is true — they have no `IsSelectingAutorotAction` guard. Do not tell operators SCH/AST are gated.**`

Keep the tanks/DPS do-not-gate bullet.

Replace `Deep background...: Second Brain/wiki/ParseLord5.md` with:

`Deep background, per-job status, and dead-end fixes not to retry: C:\\Users\\kruil\\Documents\\Projects\\SecondBrain\\Second Brain\\wiki\\ParseLord5.md` (note the wiki still claims SCH/AST were gated on 2026-07-27; trust the code and this file over that table until the wiki is updated separately).

Keep the Manual targeting footgun paragraph.

### Step 5: Confirm AGENTS.md untouched

```
git diff --name-only
```

Must not list `AGENTS.md`.

## Test plan

Docs only.

```
rg -n "Repo root is" docs/AGENTS_FULL.md
rg -n "status: archived" HANDOFF.md advisor-plan-prompt.md
rg -n "IsSelectingAutorotAction" CLAUDE.md
```

Expect repo root to be the ffxiv-tools path; both handoff files archived; CLAUDE.md still mentions the flag and now mentions SCH/AST are not gated.

## Done criteria

- [ ] `docs/AGENTS_FULL.md` repo root and `--repo` point at `ffxiv-tools\\ParseLord5`
- [ ] No hardcoded `55 tests passed` in that file
- [ ] orca is described as the second checkout, not the canonical one
- [ ] `HANDOFF.md` and `advisor-plan-prompt.md` frontmatter `status: archived` with banners
- [ ] `CLAUDE.md` wiki path is the SecondBrain absolute path
- [ ] `CLAUDE.md` no longer says SCH/AST are already fixed
- [ ] `AGENTS.md` unmodified
- [ ] No source files modified
- [ ] `plans/README.md` status row for 005 is DONE

## STOP conditions

- `ffxiv-tools\\ParseLord5` is not the tree you are editing.
- Wiki path `SecondBrain\\Second Brain\\wiki\\ParseLord5.md` is missing — then point at `CLAUDE.md` only and report; do not invent a vault path.
- Operator wants HANDOFF.md deleted rather than archived — report, do not delete under this plan.

## Maintenance notes

- Reviewer: this does not update the SecondBrain wiki SCH/AST table. That remains wrong until a vault edit.
- After plan 004, agents that ignore this doc and build orca will hit the sidecar guard. Both layers stay.
- Do not revive HANDOFF.md as the work queue. New session state goes in `CLAUDE.md` or `plans/`.
