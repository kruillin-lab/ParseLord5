# Dynamic Healing Responsiveness Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement dynamic linear healing responsiveness and optimize the party update polling rate when ParseLord5ExperimentalMode is active.

**Architecture:** Bypasses or dynamically reduces healing delays and party updating cache refresh times based on player combat state and real-time HP percentage of target(s).

**Tech Stack:** C# / .NET 10 / Dalamud API 14+ / ECommons

---

### Task 1: Optimize Party Cache Refresh Throttle

**Files:**
- Modify: `WrathCombo/CustomCombo/Functions/Party.cs:31-40`

- [ ] **Step 1: Open Party.cs and locate the party list cache throttling logic**
  Find the following block around lines 31-36:
  ```csharp
      public static unsafe List<WrathPartyMember> GetPartyMembers(bool allowCache = true)
      {
          if (!Player.Available) return [];
          _partyList.RemoveAll(x => x.BattleChara is null);
          if (allowCache && !EzThrottler.Throttle("PartyUpdateThrottle", 2000))
              return _partyList;
  ```

- [ ] **Step 2: Update the throttling logic to scale based on experimental mode and combat status**
  Replace it with:
  ```csharp
      public static unsafe List<WrathPartyMember> GetPartyMembers(bool allowCache = true)
      {
          if (!Player.Available) return [];
          _partyList.RemoveAll(x => x.BattleChara is null);

          int throttleTime = 2000;
          if (Service.Configuration.ParseLord5ExperimentalMode && (InCombat() || PartyInCombat()))
          {
              throttleTime = 100; // Fast 100ms updates in combat
          }

          if (allowCache && !EzThrottler.Throttle("PartyUpdateThrottle", throttleTime))
              return _partyList;
  ```

- [ ] **Step 3: Build the project to verify compilation**
  Run: `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`
  Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Commit the changes**
  Run:
  ```powershell
  git add WrathCombo/CustomCombo/Functions/Party.cs
  git commit -m "feat(party): dynamically scale party cache refresh throttle to 100ms in combat"
  ```

---

### Task 2: Implement Dynamic Linear Healing Delay

**Files:**
- Modify: `WrathCombo/AutoRotation/AutoRotationController.cs:230-242`

- [ ] **Step 1: Open AutoRotationController.cs and locate the canHeal check**
  Find the following lines around 230-240:
  ```csharp
          // Check if any healing action is ready
          bool actCheck = autoActions.Any(x =>
          {
              var attr = x.Key.Attributes();
              return attr.AutoAction?.IsHeal == true && ActionReady(AutoRotationHelper.InvokeCombo(x.Key, attr, ref _));
          });

          bool canHeal = TimeToHeal is not null
                         && (DateTime.Now - TimeToHeal.Value).TotalSeconds >= cfg.HealerSettings.HealDelay
                         && actCheck;
  ```

- [ ] **Step 2: Update the block to compute lowestHp and effectiveHealDelay dynamically**
  Replace the `canHeal` check and preceding check with:
  ```csharp
          // Check if any healing action is ready
          bool actCheck = autoActions.Any(x =>
          {
              var attr = x.Key.Attributes();
              return attr.AutoAction?.IsHeal == true && ActionReady(AutoRotationHelper.InvokeCombo(x.Key, attr, ref _));
          });

          float lowestHp = 100f;
          if (healTarget != null)
          {
              lowestHp = GetTargetHPPercent(healTarget);
          }
          else if (aoeheal)
          {
              foreach (var member in GetPartyMembers())
              {
                  if (member.BattleChara is not null && !member.BattleChara.IsDead)
                  {
                      float hp = GetTargetHPPercent(member.BattleChara);
                      if (hp < lowestHp)
                          lowestHp = hp;
                  }
              }
          }

          double effectiveHealDelay = cfg.HealerSettings.HealDelay;
          if (Service.Configuration.ParseLord5ExperimentalMode)
          {
              if (lowestHp <= 35f)
              {
                  effectiveHealDelay = 0.0;
              }
              else if (lowestHp >= 75f)
              {
                  effectiveHealDelay = cfg.HealerSettings.HealDelay;
              }
              else
              {
                  double t = (lowestHp - 35f) / (75f - 35f);
                  effectiveHealDelay = t * cfg.HealerSettings.HealDelay;
              }
          }

          bool canHeal = TimeToHeal is not null
                         && (DateTime.Now - TimeToHeal.Value).TotalSeconds >= effectiveHealDelay
                         && actCheck;
  ```

- [ ] **Step 3: Build the project to verify compilation**
  Run: `dotnet build .\WrathCombo\WrathCombo.csproj -c Release`
  Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Commit the changes**
  Run:
  ```powershell
  git add WrathCombo/AutoRotation/AutoRotationController.cs
  git commit -m "feat(healer): implement dynamic linear reaction delay scaled by lowest HP %"
  ```
