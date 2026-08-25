---
tags:
  - type/audit
  - project/parselord5
  - status/active
type: audit
project: parselord5
status: archived
aliases: []
---
# ParseLord5 Runtime Smoke And Collision Audit - 2026-05-17

## Purpose

Create one handoff artifact for the next live-testing phase after the completed identity/config-isolation and branding-shell milestones.

This document is intentionally **source-based plus future smoke-test instructions**. It does **not** claim that ParseLord5 has already been exercised in a live Dalamud session.

## Current known-good source state

- Active manifest is `ParseLord5.json` with `InternalName = "ParseLord5"`.
- Build output is `ParseLord5.dll`.
- Config load/save uses Dalamud plugin APIs (`GetPluginConfig()` / `SavePluginConfig()`).
- `Search.cs` now uses `Svc.PluginInterface.ConfigFile.FullName` for cache invalidation.
- No runtime `.cs` source references to `WrathCombo.json` remain.
- `RepoCheck.cs` reads `ParseLord5.json` from the assembly directory.
- `/wrath`, `/scombo`, and `/pl5` all remain registered.
- Visible shell branding has been updated for window titles / About / DTR labels, while IPC, MOTD, PunishLib, and namespace identity remain intentionally deferred.

---

## 1. Smoke-test checklist (for future live tester)

### Preconditions

1. Build `WrathCombo/WrathCombo.csproj` in `Release`.
2. Ensure ParseLord5 is the plugin being loaded by Dalamud from the expected dev plugin path.
3. Know where Dalamud plugin config files live on the test machine.
4. If running a side-by-side test with WrathCombo, take a backup/screenshot of the plugin list and current plugin config directory first.

### A. ParseLord5-only smoke test

#### Load + manifest identity

- [ ] ParseLord5 appears in Dalamud plugin UI as `ParseLord5`.
- [ ] The loaded plugin DLL is `ParseLord5.dll`.
- [ ] No startup exception occurs.

#### Commands

- [ ] `/pl5` opens the main UI.
- [ ] `/wrath` still opens the same UI.
- [ ] `/scombo` still opens the same UI.
- [ ] `/pl5 auto` toggles the same auto-rotation state as `/wrath auto`.
- [ ] `/pl5 debug` still produces a debug file.

#### Window / branding shell

- [ ] Main config window title shows `ParseLord5 <version>`.
- [ ] About tab header shows `ParseLord5`.
- [ ] Major changes popup title shows `ParseLord5 | New Changes` if triggered.
- [ ] DTR entry label shows `ParseLord5`.
- [ ] DTR opener label shows `ParseLord5 Opener`.
- [ ] DTR tooltip says `Click to toggle ParseLord5's Auto-Rotation.`

#### Configuration isolation

- [ ] Toggle a harmless UI setting in ParseLord5 (for example, a visible settings toggle).
- [ ] Confirm a config write occurs only to `ParseLord5.json`.
- [ ] Confirm `WrathCombo.json` is not created or modified by ParseLord5-only usage.
- [ ] Restart/reload ParseLord5 and confirm the changed setting persists from `ParseLord5.json`.

#### Debug / repo identity

- [ ] Generate a ParseLord5 debug file.
- [ ] Confirm repo/install-source reporting still works or cleanly reports `!! Self-Built !!` in dev mode.
- [ ] Confirm no path lookup error occurs from `RepoCheck.cs`.

### B. Optional side-by-side smoke test with WrathCombo

> Do this only when convenient. This is an observational test, not a prerequisite for source correctness.

#### Load behavior

- [ ] Try loading both WrathCombo and ParseLord5 in the same Dalamud session.
- [ ] Record whether Dalamud allows both to initialize or whether one fails early.

#### Command collisions

- [ ] Test `/wrath` with both loaded.
- [ ] Record which plugin responds.
- [ ] Test `/pl5` with both loaded.
- [ ] Confirm `/pl5` remains unique to ParseLord5.
- [ ] Test `/scombo` with both loaded.
- [ ] Record which plugin responds.

#### DTR collisions

- [ ] Check whether both DTR entries appear.
- [ ] If only one appears, record whether the label is overwritten or the second registration is blocked.

#### IPC collisions

- [ ] If an IPC-aware companion plugin is available, test whether it binds to ParseLord5, WrathCombo, or whichever loads first.
- [ ] Record whether both expose the same `WrathCombo` prefix simultaneously.

#### Config collisions

- [ ] Change a ParseLord5 setting and confirm only `ParseLord5.json` changes.
- [ ] Change a WrathCombo setting and confirm only `WrathCombo.json` changes.
- [ ] Confirm no cross-write occurs between the two files.

### Expected outcomes

#### ParseLord5-only

- `/pl5`, `/wrath`, `/scombo` all work.
- Visible shell branding shows `ParseLord5` in window title / About / DTR.
- Config writes only to `ParseLord5.json`.

#### Side-by-side

- Config files should remain isolated.
- Command and IPC collisions are **expected risks** because both still intentionally preserve WrathCombo-facing compatibility.

---

## 2. Side-by-side collision matrix (source-based)

This matrix is based on current source analysis, not a live dual-load proof.

| Surface | ParseLord5 current behavior | Collision risk if WrathCombo is also loaded | Expected severity | Notes |
|---|---|---|---|---|
| Manifest internal name | `ParseLord5` | None | Low | Separate plugin identity in Dalamud/plugin config |
| Plugin config file | `ParseLord5.json` | None | Low | Already isolated by InternalName |
| Build output DLL | `ParseLord5.dll` | None | Low | Separate output artifact |
| `/pl5` command | Registered | None expected | Low | ParseLord5-only alias |
| `/wrath` command | Registered | **Yes** | High | Both plugins can claim `/wrath` |
| `/scombo` command | Registered | **Yes** | High | Both plugins can claim `/scombo` |
| IPC prefix | `WrathCombo` | **Yes** | High | Both plugins would expose same prefix |
| IPC callback naming | `ParseLord5Callback` | Yes | Medium | Part of same IPC compatibility surface |
| IPC status endpoint | Upstream WrathCombo URL | Shared behavior | Medium | ParseLord5 still follows WrathCombo IPC kill-switch file |
| DTR main label | `ParseLord5` | Maybe | Medium | Safer after branding shell, but registration semantics need live test |
| DTR opener label | `ParseLord5 Opener` | Maybe | Medium | Same as above |
| About/window titles | `ParseLord5` | None | Low | Purely visible branding |
| MOTD URL/content | WrathCombo upstream | No direct collision, but identity leak | Low | Cosmetic / future branding work |
| PunishLib display name | `Wrath Combo` | No config collision, visible identity leak | Low | Deferred intentionally |
| Namespace/class names | `WrathCombo` | No runtime collision by itself | Low | Mergeability-preserving defer |

### Matrix summary

#### Safe today

- Config isolation
- Manifest/build identity
- `/pl5` uniqueness
- Visible shell branding

#### Known intentional compatibility leaks

- `/wrath`
- `/scombo`
- IPC prefix `WrathCombo`
- IPC endpoint on upstream WrathCombo repo

These are **not bugs** in the completed identity/config milestone. They are deliberate compatibility choices that must be revisited only with a separate migration plan.

---

## 3. Remaining identity pre-audit (safe / defer / coordination-needed)

### 3.1 Safe to change in a future branding-only pass

These are visible identity items that should be low-risk if changed carefully:

| Item | Current state | Why safe-ish |
|---|---|---|
| MOTD fallback text | `Welcome to WrathCombo` | Visible-only fallback string |
| MOTD prefix | `[Wrath Message of the Day]` | Visible-only chat branding |
| Additional About/help strings | mixed | Mostly display text |
| Logo asset naming / image labels | `wrathcombo.png` | Cosmetic, not config identity |

### 3.2 Defer unless necessary

These should remain deferred because they affect mergeability or low-level code identity more than user-facing branding:

| Item | Current state | Why defer |
|---|---|---|
| Namespace rename | `namespace WrathCombo` | Large mergeability hit |
| Class rename | `WrathCombo` main class, etc. | Large mergeability hit |
| Folder rename | `WrathCombo/` root project folder | Large repo churn |
| `WrathCombo.API` identity | Preserved | External compatibility surface |

### 3.3 Coordination-needed

These should not be changed casually. They require a separate compatibility or rollout plan.

| Item | Current state | Why coordination is needed |
|---|---|---|
| IPC prefix | `WrathCombo` | Existing consumers expect this name |
| IPC provider names | WrathCombo-shaped | Same reason as prefix |
| IPC status endpoint | Upstream WrathCombo repo | Requires ownership decision |
| `/wrath` removal | Preserved | User muscle memory + collision rollout |
| `/scombo` removal | Preserved | Legacy compatibility |
| PunishLib display name | `Wrath Combo` | Probably low-risk technically, but touches external UI/logging expectations |

### 3.4 Explicitly not implemented

- No config migration/import
- No automatic WrathCombo config detection
- No deletion or mutation of WrathCombo config
- No attempt to make ParseLord5 “pretend” to be WrathCombo from a manifest/config standpoint

---

## 4. Recommended next steps

### Recommended next milestone

**Live runtime smoke + collision observation** in Dalamud, using section 1 as the checklist.

### If that smoke test passes

Then the next sensible decision gate is:

1. Keep compatibility surfaces (`/wrath`, IPC prefix) for longer, or
2. Start a carefully planned user-facing migration away from WrathCombo identity leaks.

### Not recommended as default next step

- Namespace/class rename
- Broad API rename
- IPC rename without consumer coordination

Those actions reduce upstream mergeability too early and do not improve the already-complete config isolation story.

---

## 5. Practical tester notes

- Changing ImGui window IDs in the branding-shell milestone may reset saved window positions. This is acceptable and should be treated as a minor user-facing side effect, not a regression.
- If a side-by-side test shows command or IPC collisions, that does **not** invalidate the completed config-isolation milestone. It only confirms the known intentional compatibility overlap.
- `RepoCheck.cs` reading `ParseLord5.json` is correct and should remain.
