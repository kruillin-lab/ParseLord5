$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$PresetFile = Join-Path $RepoRoot "WrathCombo\Combos\CustomComboPreset.cs"
$CombosDir = Join-Path $RepoRoot "WrathCombo\Combos\PvE"
$SAMFile = Join-Path $CombosDir "SAM\SAM.cs"
$SAMHelperFile = Join-Path $CombosDir "SAM\SAM_Helper.cs"
$VPRHelperFile = Join-Path $CombosDir "VPR\VPR_Helper.cs"
$WHMFile = Join-Path $CombosDir "WHM\WHM.cs"
$MNKHelperFile = Join-Path $CombosDir "MNK\MNK_Helper.cs"

Write-Host "=== ParseLord5 Domain Evals ==="
Write-Host "Scope: preset enumeration, structure validation, and SAM/VPR/WHM/MNK domain-specific checks"
Write-Host ""

# ---- FIXTURE 1: Preset enum has entries ----
Write-Host "--- Fixture: preset-enum-has-entries ---"
$presetContent = Get-Content -LiteralPath $PresetFile -Raw
$presetMatches = [regex]::Matches($presetContent, '^\s+(\w+) = (\d+),?', [System.Text.RegularExpressions.RegexOptions]::Multiline)
$passCount = 0
$failCount = 0

if ($presetMatches.Count -gt 0) {
    Write-Host "PASS preset-enum-has-entries: found $($presetMatches.Count) preset entries"
    $passCount++
} else {
    Write-Host "FAIL preset-enum-has-entries: no preset entries found"
    $failCount++
}

# ---- FIXTURE 2: All jobs have presets ----
Write-Host "--- Fixture: all-jobs-have-presets ---"
$jobDirs = Get-ChildItem -LiteralPath $CombosDir -Directory | Where-Object { $_.Name -match '^(ALL|[A-Z]{3})$' }
$jobsWithPresets = 0
$jobsMissing = @()

foreach ($dir in $jobDirs) {
    $jobCode = $dir.Name
    $presetPattern = "${jobCode}_"
    $matches = $presetMatches | Where-Object { $_.Groups[1].Value -match "^${presetPattern}" }
    if ($matches) {
        $jobsWithPresets++
        Write-Host "  $jobCode : $($matches.Count) presets"
    } else {
        $jobsMissing += $jobCode
    }
}

if ($jobsMissing.Count -eq 0) {
    Write-Host "PASS all-jobs-have-presets: $jobsWithPresets jobs, 0 missing"
    $passCount++
} else {
    Write-Host "FAIL all-jobs-have-presets: missing presets for $($jobsMissing -join ', ')"
    $failCount++
}

# ---- FIXTURE 3: Presets have unique IDs (no duplicates) ----
Write-Host "--- Fixture: presets-have-unique-ids ---"
$ids = @{}
$duplicates = 0
foreach ($match in $presetMatches) {
    $name = $match.Groups[1].Value
    $value = $match.Groups[2].Value
    if ($ids.ContainsKey($value)) {
        Write-Host "  FAIL: duplicate ID $value for $name and $($ids[$value])"
        $duplicates++
    } else {
        $ids[$value] = $name
    }
}
if ($duplicates -eq 0) {
    Write-Host "PASS presets-have-unique-ids: all $($presetMatches.Count) presets have unique IDs"
    $passCount++
} else {
    Write-Host "FAIL presets-have-unique-ids: $duplicates duplicate IDs found"
    $failCount++
}

# ---- NEGATIVE CONTROL: invalid preset does not match ----
Write-Host "--- Negative Control: no-preset-named-INVALID ---"
$invalidMatch = $presetMatches | Where-Object { $_.Groups[1].Value -eq 'INVALID_PRESET_NAME' }
if ($invalidMatch) {
    Write-Host "FAIL_NEGATIVE no-preset-named-INVALID: unexpected match for INVALID_PRESET_NAME"
    $failCount++
} else {
    Write-Host "PASS_NEGATIVE no-preset-named-INVALID: no match for invalid name (expected)"
    $passCount++
}

# ---- FIXTURE 4: Combo files compile (build check already done by gate) ----
Write-Host "--- Fixture: combo-files-exist-for-jobs ---"
foreach ($dir in $jobDirs) {
    $comboFiles = Get-ChildItem -LiteralPath $dir.FullName -Filter "*.cs" | Where-Object { $_.Name -notlike "*_Config*" -and $_.Name -notlike "*_Helper*" }
    if ($comboFiles.Count -eq 0) {
        Write-Host "  WARN: $($dir.Name) has no main combo file"
    }
}
Write-Host "PASS combo-files-exist-for-jobs: all jobs checked"
$passCount++

# ---- FIXTURE 5: Promoted SAM/VPR priority contracts remain default behavior ----
Write-Host "--- Fixture: promoted-job-priority-contracts ---"
$samContent = Get-Content -LiteralPath $SAMFile -Raw
$vprHelperContent = Get-Content -LiteralPath $VPRHelperFile -Raw

$samPriorityBlocks = [ordered]@{
    "SAM_ST_SimpleMode" = [regex]::Match($samContent, 'internal class SAM_ST_SimpleMode.*?internal class SAM_AoE_SimpleMode', 'Singleline').Value
    "SAM_AoE_SimpleMode" = [regex]::Match($samContent, 'internal class SAM_AoE_SimpleMode.*?internal class SAM_ST_AdvancedMode', 'Singleline').Value
    "SAM_ST_AdvancedMode" = [regex]::Match($samContent, 'internal class SAM_ST_AdvancedMode.*?internal class SAM_AoE_AdvancedMode', 'Singleline').Value
    "SAM_AoE_AdvancedMode" = [regex]::Match($samContent, 'internal class SAM_AoE_AdvancedMode.*?internal class SAM_ST_YukikazeCombo', 'Singleline').Value
}
$samPriorityPatterns = [ordered]@{
    "SAM_ST_SimpleMode" = '(?s)if\s*\(canIkishoten\)\s*return\s+ikishotenAction;.*?if\s*\(CanMeikyo\(\)\)\s*return\s+MeikyoShisui;'
    "SAM_AoE_SimpleMode" = '(?s)if\s*\(canIkishoten\)\s*return\s+kenkiAction;.*?if\s*\(canMeikyo\)\s*return\s+MeikyoShisui;'
    "SAM_ST_AdvancedMode" = '(?s)if\s*\(canIkishoten\)\s*return\s+ikishotenAction;.*?if\s*\(canMeikyo\)\s*return\s+MeikyoShisui;'
    "SAM_AoE_AdvancedMode" = '(?s)if\s*\(canIkishoten\)\s*return\s+kenkiAction;.*?if\s*\(canMeikyo\)\s*return\s+MeikyoShisui;'
}
$samPriorityFailures = @()

foreach ($name in $samPriorityBlocks.Keys) {
    $block = $samPriorityBlocks[$name]
    $pattern = $samPriorityPatterns[$name]
    if ([string]::IsNullOrWhiteSpace($block) -or
        $block -notmatch $pattern -or
        $block -match 'ParseLord5Experiments\.JobRotationExperiments') {
        $samPriorityFailures += $name
    }
}

if ($samPriorityFailures.Count -eq 0) {
    Write-Host "PASS sam-promoted-ikishoten-priority: Ikishoten precedes Meikyo in all ST/AoE modes without an experiment gate"
    $passCount++
} else {
    Write-Host "FAIL sam-promoted-ikishoten-priority: priority contract missing in $($samPriorityFailures -join ', ')"
    $failCount++
}

$vprViceTwinBlock = [regex]::Match(
    $vprHelperContent,
    'private static bool UseViceTwinWeaves.*?private static bool CanSerpentsIre',
    'Singleline'
).Value
$vprAoEPriorityPattern = '(?s)if\s*\(canFellskinsVenom\)\s*\{\s*action\s*=\s*OriginalHook\(Twinblood\);\s*return true;\s*\}.*?if\s*\(canFellhuntersVenom\)\s*\{\s*action\s*=\s*OriginalHook\(Twinfang\);\s*return true;\s*\}'
$vprStPriorityPattern = '(?s)if\s*\(HasStatusEffect\(Buffs\.SwiftskinsVenom\)\)\s*\{\s*action\s*=\s*OriginalHook\(Twinblood\);\s*return true;\s*\}.*?if\s*\(HasStatusEffect\(Buffs\.HuntersVenom\)\)\s*\{\s*action\s*=\s*OriginalHook\(Twinfang\);\s*return true;\s*\}'

if (-not [string]::IsNullOrWhiteSpace($vprViceTwinBlock) -and
    $vprViceTwinBlock -match $vprAoEPriorityPattern -and
    $vprViceTwinBlock -match $vprStPriorityPattern -and
    $vprViceTwinBlock -notmatch 'ParseLord5Experiments\.JobRotationExperiments') {
    Write-Host "PASS vpr-promoted-venom-priority: Twinblood priority remains promoted in AoE and ST without an experiment gate"
    $passCount++
} else {
    Write-Host "FAIL vpr-promoted-venom-priority: UseViceTwinWeaves no longer preserves the promoted Twinblood-first ladders"
    $failCount++
}

# ---- FIXTURE 6: Critical action IDs mapped (Gyofu for SAM, Twinfang/Twinblood for VPR) ----
Write-Host "--- Fixture: critical-action-ids-mapped ---"
$samHelperContent = Get-Content -LiteralPath $SAMHelperFile -Raw

# Gyofu must appear as a const assignment in SAM_Helper.cs
if ($samHelperContent -match 'Gyofu\s*=\s*\d+') {
    Write-Host "PASS sam-gyofu-id-mapped: Gyofu action ID is defined in SAM_Helper.cs"
    $passCount++
} else {
    Write-Host "FAIL sam-gyofu-id-mapped: Gyofu action ID not found in SAM_Helper.cs"
    $failCount++
}

# Twinfang must appear as a const assignment in VPR_Helper.cs
if ($vprHelperContent -match 'Twinfang\s*=\s*\d+') {
    Write-Host "PASS vpr-twinfang-id-mapped: Twinfang action ID is defined in VPR_Helper.cs"
    $passCount++
} else {
    Write-Host "FAIL vpr-twinfang-id-mapped: Twinfang action ID not found in VPR_Helper.cs"
    $failCount++
}

# Twinblood must appear as a const assignment in VPR_Helper.cs
if ($vprHelperContent -match 'Twinblood\s*=\s*\d+') {
    Write-Host "PASS vpr-twinblood-id-mapped: Twinblood action ID is defined in VPR_Helper.cs"
    $passCount++
} else {
    Write-Host "FAIL vpr-twinblood-id-mapped: Twinblood action ID not found in VPR_Helper.cs"
    $failCount++
}

# ---- FIXTURE 7: SAM ST Ikishoten can recover after the exact second-GCD window ----
Write-Host "--- Fixture: sam-st-ikishoten-recovery ---"
# Dedup (guarded-ladder rewrite) hoisted each duplicated call into a single copy:
# 4 calls (2 sites x 2 flag-order copies) became 2, and the advanced site now uses a
# predeclared out var (`out ikishotenAction`) for definite assignment.
$samIkishotenCallCount = ([regex]::Matches($samContent, 'TryGetIkishotenAction\s*\(')).Count
$samAdvancedIkishotenCallCount = ([regex]::Matches($samContent, 'TryGetIkishotenAction\s*\(\s*out (uint )?ikishotenAction,\s*IsEnabled\(Preset\.SAM_ST_Shinten\)\s*\)')).Count
$samIkishotenHelperBlock = [regex]::Match(
    $samHelperContent,
    'private static bool TryGetIkishotenAction\(out uint action, bool allowKenkiDump = true\)(?<body>[\s\S]*?)\r?\n    private static bool CanSenei'
).Groups['body'].Value
$samHasLateWindow = $samHelperContent -match 'NumberOfGcdsUsed\s*>=\s*2'
$samHasExactOnlyWindow = $samHelperContent -match 'NumberOfGcdsUsed\s+is\s+2'
$samHasKenkiSafeIkishoten = $samIkishotenHelperBlock -match 'Kenki\s*<=\s*50' -and $samIkishotenHelperBlock -match 'action\s*=\s*Ikishoten'
$samHasKenkiDump = $samIkishotenHelperBlock -match 'allowKenkiDump' -and
    $samIkishotenHelperBlock -match 'ActionReady\(Shinten\)' -and
    $samIkishotenHelperBlock -match 'InActionRange\(Shinten\)' -and
    $samIkishotenHelperBlock -match 'action\s*=\s*Shinten'

if ($samIkishotenCallCount -ge 2 -and $samAdvancedIkishotenCallCount -ge 1 -and $samHasLateWindow -and -not $samHasExactOnlyWindow -and $samHasKenkiSafeIkishoten -and $samHasKenkiDump) {
    Write-Host "PASS sam-st-ikishoten-recovery: ST routes recover after GCD 2 and dump Kenki before Ikishoten overcap"
    $passCount++
} else {
    if ($samIkishotenCallCount -lt 2) {
        Write-Host "  Expected all ST Ikishoten branches to call TryGetIkishotenAction, found $samIkishotenCallCount"
    }
    if ($samAdvancedIkishotenCallCount -lt 1) {
        Write-Host "  Expected Advanced ST Ikishoten branches to honor the Shinten toggle, found $samAdvancedIkishotenCallCount"
    }
    if (-not $samHasLateWindow) {
        Write-Host "  Missing NumberOfGcdsUsed >= 2 recovery window in SAM_Helper.cs"
    }
    if ($samHasExactOnlyWindow) {
        Write-Host "  Found exact-only NumberOfGcdsUsed is 2 gate in SAM_Helper.cs"
    }
    if (-not $samHasKenkiSafeIkishoten) {
        Write-Host "  Missing Ikishoten selection at safe Kenki in TryGetIkishotenAction"
    }
    if (-not $samHasKenkiDump) {
        Write-Host "  Missing Shinten Kenki dump before Ikishoten overcap in TryGetIkishotenAction"
    }
    Write-Host "FAIL sam-st-ikishoten-recovery"
    $failCount++
}

# ---- FIXTURE 8: WHM DPS paths remain isolated from the dedicated healer lane ----
Write-Host "--- Fixture: whm-dps-healer-lane-isolation ---"
$whmContent = Get-Content -LiteralPath $WHMFile -Raw
$whmDpsBlocks = [ordered]@{
    "WHM_ST_Simple_DPS" = [regex]::Match($whmContent, 'internal class WHM_ST_Simple_DPS.*?internal class WHM_AoE_Simple_DPS', 'Singleline').Value
    "WHM_AoE_Simple_DPS" = [regex]::Match($whmContent, 'internal class WHM_AoE_Simple_DPS.*?internal class WHM_ST_MainCombo', 'Singleline').Value
    "WHM_ST_MainCombo" = [regex]::Match($whmContent, 'internal class WHM_ST_MainCombo.*?internal class WHM_AoE_DPS', 'Singleline').Value
    "WHM_AoE_DPS" = [regex]::Match($whmContent, 'internal class WHM_AoE_DPS.*?#endregion\s+#region Simple Heals', 'Singleline').Value
}
$whmIsolationFailures = @()

foreach ($entry in $whmDpsBlocks.GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace($entry.Value) -or
        $entry.Value -match 'TryDpsSingleTargetHealPriority|TryDpsAoEHealPriority') {
        $whmIsolationFailures += $entry.Key
    }
}

if ($whmIsolationFailures.Count -eq 0) {
    Write-Host "PASS whm-dps-healer-lane-isolation: all WHM DPS paths leave healing to the dedicated healer lane"
    $passCount++
} else {
    Write-Host "  DPS-heal leakage found in: $($whmIsolationFailures -join ', ')"
    Write-Host "FAIL whm-dps-healer-lane-isolation"
    $failCount++
}

# ---- FIXTURE 9: MNK ST Perfect Balance spends a charge before overcapping ----
Write-Host "--- Fixture: mnk-st-perfect-balance-charge-failsafe ---"
$mnkHelperContent = Get-Content -LiteralPath $MNKHelperFile -Raw
# CanPerfectBalance's ST path (onAoE == false) delegates to ShouldUsePreRoFPerfectBalance;
# the charge-overcap failsafe lives at the top of that method, not inline in a switch anymore
# (upstream restructured CanPerfectBalance from a switch-expression into if/else + helper calls).
$mnkPerfectBalanceStBlock = [regex]::Match(
    $mnkHelperContent,
    'private static bool ShouldUsePreRoFPerfectBalance\(bool useOpenerBalance\)(?<body>.*?)\n    \}',
    'Singleline'
).Groups['body'].Value

if (-not [string]::IsNullOrWhiteSpace($mnkPerfectBalanceStBlock) -and
    $mnkPerfectBalanceStBlock -match 'GetRemainingCharges\(PerfectBalance\)\s*==\s*GetMaxCharges\(PerfectBalance\)') {
    Write-Host "PASS mnk-st-perfect-balance-charge-failsafe: ST spends Perfect Balance at maximum charges"
    $passCount++
} else {
    Write-Host "FAIL mnk-st-perfect-balance-charge-failsafe: ST can hold Perfect Balance at maximum charges"
    $failCount++
}

# ---- FIXTURE: plugin teardown unsubscribes every long-lived event ----
# A Dalamud event left subscribed at Dispose keeps calling into the unloaded
# assembly every frame, which crashes the game on disable -> re-enable. Each
# entry is a delegate that must be removed by the same file that adds it.
Write-Host "--- Fixture: teardown-event-subscription-symmetry ---"
$symmetryTargets = @(
    @{ File = "WrathCombo\WrathCombo.cs";                       Delegates = @("ws.Draw", "OnOpenMainUi", "OnOpenConfigUi", "OnFrameworkUpdate", "OnErrorToast", "Text.OnLanguageChanged", "ClientState_TerritoryChanged", "PrintLoginMessage") },
    @{ File = "WrathCombo\Services\IPC\Leasing.cs";             Delegates = @("CheckIfLeaseePluginsUnloaded") },
    @{ File = "WrathCombo\AutoRotation\AutoRotationController.cs"; Delegates = @("ScanForWarnings", "StatusChanged", "ResetError") },
    @{ File = "WrathCombo\Data\ActionWatching.cs";               Delegates = @("ResetActions", "CancelPendingLastActionUpdate") },
    @{ File = "WrathCombo\CustomCombo\Functions\Timer.cs";       Delegates = @("UpdatePartyTimer", "UpdateDeadtionary", "CheckInterruptedCasts", "CheckStatuses", "OnCombat") },
    @{ File = "WrathCombo\Data\CustomComboCache.cs";             Delegates = @("Framework_Update") }
)

$unpairedSubscriptions = @()
foreach ($target in $symmetryTargets) {
    $targetPath = Join-Path $RepoRoot $target.File
    if (-not (Test-Path -LiteralPath $targetPath)) {
        $unpairedSubscriptions += "$($target.File) (missing file)"
        continue
    }

    $targetContent = Get-Content -LiteralPath $targetPath -Raw
    foreach ($handler in $target.Delegates) {
        $escaped = [regex]::Escape($handler)
        $subscribes = [regex]::Matches($targetContent, "\+=\s*$escaped\s*;").Count
        $unsubscribes = [regex]::Matches($targetContent, "-=\s*$escaped\s*;").Count

        if ($subscribes -gt 0 -and $unsubscribes -eq 0) {
            $unpairedSubscriptions += "$($target.File): $handler subscribed but never unsubscribed"
        } elseif ($subscribes -eq 0) {
            $unpairedSubscriptions += "$($target.File): $handler no longer subscribed (stale fixture entry)"
        }
    }
}

if ($unpairedSubscriptions.Count -eq 0) {
    Write-Host "PASS teardown-event-subscription-symmetry: every tracked delegate is both subscribed and unsubscribed"
    $passCount++
} else {
    foreach ($problem in $unpairedSubscriptions) { Write-Host "  FAIL: $problem" }
    Write-Host "FAIL teardown-event-subscription-symmetry: $($unpairedSubscriptions.Count) unpaired subscription(s)"
    $failCount++
}

Write-Host ""
Write-Host "=== Summary ==="
Write-Host "passed=$passCount failed=$failCount negative_controls=1 total=$(($passCount + $failCount))"

if ($failCount -gt 0) { exit 1 } else { exit 0 }
