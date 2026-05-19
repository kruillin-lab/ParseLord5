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
# Advisor Plan Mode Prompt — ParseLord5: What Next?

Paste everything below into the advisor chat.

---

You are the bound advisor in Plan Mode for ParseLord5.

## Complete Milestone Inventory

### 1. /pl5 Command Alias
- `/pl5` added alongside `/wrath` and `/scombo`. All three route to `OnCommand`.

### 2. Config Isolation Hardening
- `InternalName = "ParseLord5"` → config file is `ParseLord5.json`.
- `GetPluginConfig()` / `SavePluginConfig()` use Dalamud-native paths.
- `Search.cs` uses `ConfigFile.FullName` (no hardcoded filename).
- `RepoCheck.cs` reads `ParseLord5.json`.
- `WrathCombo.json` reference removed from `RepoCheck.cs`.
- Zero runtime `.cs` references to `WrathCombo.json`.

### 3. Visible Branding Shell
- Window title, About tab, Major Changes popup, DTR labels, DTR tooltip, Plugin Name (`P.Name`) — all rebranded to `ParseLord5`.
- ImGui window IDs updated (ConfigWindow, TargetHelper).

### 4. Live Smoke Test
- All commands work, UI opens, config writes only to `ParseLord5.json`, `WrathCombo.json` untouched. Basic side-by-side behavior acceptable.

### 5. MOTD / PunishLib / IPC Naming Cleanup
- MOTD welcome text, MOTD prefix, PunishLib init, IPC log prefix — all rebranded to `ParseLord5`.
- Policy doc: `docs/ParseLord5_Remaining_Identity_Policy_20260517.md`.

### 6. IPC / API Compatibility Evaluation
- Full provider method inventory (20 methods). Subscriber inventory (PingPlugin, Orbwalker, BossMod, MOAction, ReAction, Redirect, RotationSolver).
- Found InternalName/prefix mismatch was blocking external IPC consumers.
- Evaluation doc: `docs/ParseLord5_IPC_API_Compatibility_Evaluation_20260517.md`.

### 7. IPC Prefix Implementation
- Provider IPC prefix changed from `"WrathCombo"` to `"ParseLord5"` at `Provider.cs:84`.
- Consumer pattern: `TryGetDalamudPlugin("ParseLord5")` + `EzIPC.Init(..., "ParseLord5")`.
- Side-by-side collision eliminated (separate prefixes for separate plugins).

### 8. Public Distribution Identity Cleanup
- `"parselord5"` tag added to manifest tags.
- `"WrathCombo Icon"` → `"ParseLord5 Icon"` in cosmetic log strings.
- RepoUrl, IconUrl, PackageProjectUrl, and logo asset documented as deferred (no ParseLord5-owned replacements exist).
- Doc: `docs/ParseLord5_Public_Distribution_Identity_20260517.md`.

## Current State

**All identity/config/branding/IPC/distribution work is complete.** Everything that can be rebranded without owning a separate repo or breaking upstream mergeability has been done.

### Done ✅
- Config isolation (InternalName → separate config files)
- Command aliases (`/pl5`, `/wrath`, `/scombo`)
- Window/About/DTR visible branding
- MOTD local text branding
- PunishLib display name
- IPC log prefix
- IPC provider prefix now matches InternalName
- Manifest tags include `parselord5`
- Cosmetic icon log strings rebranded
- Docs: runtime identity audit, smoke/collision audit, identity policy, IPC/API evaluation, distribution identity

### Still Wrath-branded (intentionally deferred)
- **IPC callback name**: `WrathComboCallback` — external consumers implement this signature.
- **IPC status endpoint**: `PunishXIV/WrathCombo/…/ipc_status.txt` — upstream dependency.
- **MOTD fetch URL**: `PunishXIV/WrathCombo/…/motd.txt` — upstream dependency.
- **`WrathCombo.API` project/namespace** — public API surface for external consumers.
- **C# namespaces/classes**: `WrathCombo.*` — mergeability-preserving defer.
- **RepoUrl / IconUrl / PackageProjectUrl / logo asset** — require a ParseLord5-owned repo to replace.
- **Cosmetic doc comments and debug UI labels** — lowest priority.

### Key Docs
- `docs/ParseLord5_Runtime_Identity_Audit_20260517.md`
- `docs/ParseLord5_Runtime_Smoke_And_Collision_Audit_20260517.md`
- `docs/ParseLord5_Remaining_Identity_Policy_20260517.md`
- `docs/ParseLord5_IPC_API_Compatibility_Evaluation_20260517.md`
- `docs/ParseLord5_Public_Distribution_Identity_20260517.md`
- `docs/ParseLord5_WrathCombo_Architecture_Map.md`
- `docs/ParseLord5_Config_Migration_Plan.md`
- `docs/IPC.md` (updated)

### Experimental Mode
`ParseLord5ExperimentalMode` flag already exists in `Configuration.cs:50` (default `false`). WAR tracing code already gated behind it. The flag is wired into the UI and config save/load but no other gameplay code uses it yet.

## Questions

1. **Is the identity phase complete enough to close?** Eight milestones have been completed. All surfaces that can be rebranded without owning a separate repo or breaking upstream mergeability have been addressed. Should we consider the fork identity-stable and move to gameplay work?

2. **Should we begin gameplay experimentation behind `ParseLord5ExperimentalMode`?** The flag exists, is gated, and is wired. The architecture map recommends starting with one job/preset (Warrior is already partially traced). Is this the right next move, or should other prerequisites come first?

3. **What is the correct next milestone?**
   - Gameplay/rotation experimentation behind `ParseLord5ExperimentalMode`?
   - Config migration/import feature?
   - WrathComboCallback rename + consumer coordination?
   - IPC status endpoint ownership?
   - Namespace/class rename?
   - Something else?

4. **Are there any remaining risks** that should block beginning gameplay changes? Specifically:
   - Does the IPC callback name (`WrathComboCallback`) create any risk for gameplay consumers?
   - Does the MOTD fetch URL create any runtime risk?
   - Is the `ParseLord5ExperimentalMode` gating mechanism sufficient for safe experimentation?

5. **Sequence:** If gameplay work starts now, what 2-3 milestones should follow in order?

## Format

Return:
- **DIAGNOSIS** — assessment of whether the identity phase is complete and whether gameplay work can begin
- **CORRECTION** — recommended next concrete action
- **RISKS** — remaining concerns that could block gameplay work
- **RECOMMENDED NEXT MILESTONE** — one concrete milestone name
- **SEQUENCE** — next 2-3 milestones in recommended order
