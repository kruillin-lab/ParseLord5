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
# ParseLord5 Public Distribution Identity - 2026-05-17

## Purpose

Audit and clean up ParseLord5's public-facing distribution metadata after the identity/config/branding/IPC phase. Runtime identity is coherent (InternalName, config, shell branding, IPC prefix all say `ParseLord5`). This milestone addresses public/package metadata and cosmetic public identity strings.

## Metadata changed

| Item | File | Old | New |
|---|---|---|---|
| Tags | `ParseLord5.json` | (no `parselord5` tag) | Added `"parselord5"` |
| Icon log (local) | `ConfigWindow.cs:205` | `"Using Local WrathCombo Icon"` | `"Using Local ParseLord5 Icon"` |
| Icon log (remote) | `ConfigWindow.cs:213` | `"Using Remote WrathCombo Icon"` | `"Using Remote ParseLord5 Icon"` |

## Metadata intentionally left unchanged

| Item | File | Current value | Why deferred |
|---|---|---|---|
| RepoUrl | `ParseLord5.json:8` | `https://github.com/PunishXIV/WrathCombo` | No ParseLord5-owned repo exists (`git remote -v` returns PunishXIV/WrathCombo). Must not invent a fake URL. |
| IconUrl | `ParseLord5.json:9` | `https://s3.puni.sh/media/plugin/60/icon-2bwhkn3zf1f.png` | No ParseLord5-owned hosted icon exists. Must not invent a fake URL. |
| PackageProjectUrl | `WrathCombo.csproj:12` | `https://github.com/PunishXIV/WrathCombo` | Same as RepoUrl — no ParseLord5 repo exists. |
| Logo asset | `WrathCombo.csproj:69` | `res/plugin/wrathcombo.png` | No ParseLord5 replacement asset exists. Renaming without a replacement file would break the build. |
| Punchline | `ParseLord5.json:6` | `"WrathCombo-based fork and iteration."` | Accurate description. Changing it is cosmetic and low-priority. |
| Description | `ParseLord5.json:7` | `"ParseLord5 is a WrathCombo-based fork..."` | Already mentions ParseLord5. Accurate. |
| Author | `ParseLord5.json:2` | `"Team Wrath"` | Upstream team attribution. Changing requires coordination. |
| Changelog | `ParseLord5.json:10` | Points to upstream GitHub/Discord | No ParseLord5-owned channels exist. |
| Tags: `wrath`, `wrathcombo` | `ParseLord5.json:13-14` | Preserved | Removes upstream discoverability. `parselord5` tag added alongside. |
| Tags: `auto`, `autorotation`, `rotation`, `pve`, `pvp`, `combo` | `ParseLord5.json` | Preserved | Functional tags, not branding. Shared by both plugins. |

## Remaining upstream dependencies (not just branding)

| Item | File | Value | Why deferred |
|---|---|---|---|
| MOTD fetch URL | `WrathCombo.cs:429` | `PunishXIV/WrathCombo/main/res/motd.txt` | Requires ParseLord5-owned MOTD endpoint. Local fallback text already rebranded. |
| IPC status endpoint | `Helper.cs:390` | `PunishXIV/WrathCombo/main/res/ipc_status.txt` | Remote kill-switch. Requires ParseLord5-owned endpoint or removal. |
| WrathCombo.API | `WrathCombo.csproj:82` | Project reference + namespace | Public API surface for external consumers. Requires coordinated fork/rename. |
| C# namespaces/classes | Throughout | `WrathCombo.*` | Large mergeability hit. No user-visible benefit. |

## Cosmetic log strings (unchanged, lowest priority)

| Item | File | Value |
|---|---|---|
| Icon loading code | `ConfigWindow.cs:203,210,215` | `wrathcombo.png`, `IconUrl` |
| IPC doc comment | `Provider.cs:165` | `"Checks the state of the Wrath IPC."` |
| IPC debug label | `Debug.cs:1020` | `"Wrath IPC"` |
| IPC debug text | `Debug.cs:1022` | `"Wrath Leased:"` |
| IPC debug registration | `Debug.cs:1028` | `RegisterForLease("WrathCombo", …)` |

These are debug/log/dev-only strings with no public distribution impact. They can be cleaned up in a future cosmetic pass.

## Prerequisites for future public release

To complete public distribution identity:

1. **Own a GitHub repo** — create or transfer a ParseLord5 repository so `RepoUrl`, `PackageProjectUrl`, and `Changelog` can point to it.
2. **Own an icon asset** — create a ParseLord5 icon and host it (or bundle it locally) so `IconUrl` and the logo asset can be updated.
3. **Optional: MOTD endpoint** — if ParseLord5 wants its own message-of-the-day, create an endpoint and update the fetch URL.
4. **Optional: IPC status endpoint** — if ParseLord5 wants independent IPC kill-switch control, create an endpoint and update the status URL.
5. **Optional: WrathCombo.API fork** — if ParseLord5 enums diverge from upstream, fork the API project and coordinate with consumers.
6. **Optional: Namespace rename** — only after all other ParseLord5 work is stable and upstream mergeability is no longer needed.

## Build result

Passed. 0 errors, 8 pre-existing warnings (CS8618 ×5, CS0219 ×2, CS0649 ×1).
