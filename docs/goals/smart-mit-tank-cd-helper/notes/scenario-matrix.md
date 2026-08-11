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
# Smart mit scenario matrix (WAR, experimental mode)

Operator verifies in-game with ParseLord5 experimental mode on.

| Scenario | Expect |
|----------|--------|
| Trash, 2 enemies, mit already up | No extra Rampart/Vengeance weave |
| Trash, pack 5+, sustained damage | One personal CD via coverage, not full bar |
| Boss TB telegraph | Single appropriate CD (Rampart/Vengeance/Raw), not stack + Shake unless raidwide |
| Raidwide + party mit enabled | Reprisal **or** Shake via coverage, not both same window |
| Low trash HP% (above threshold) | No mit spam |

Build verify: `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`
