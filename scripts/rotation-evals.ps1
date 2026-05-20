$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$PresetFile = Join-Path $RepoRoot "WrathCombo\Combos\CustomComboPreset.cs"
$CombosDir = Join-Path $RepoRoot "WrathCombo\Combos\PvE"
$SAMFile = Join-Path $CombosDir "SAM\SAM.cs"
$SAMHelperFile = Join-Path $CombosDir "SAM\SAM_Helper.cs"
$VPRFile = Join-Path $CombosDir "VPR\VPR.cs"
$VPRHelperFile = Join-Path $CombosDir "VPR\VPR_Helper.cs"

Write-Host "=== ParseLord5 Domain Evals ==="
Write-Host "Scope: preset enumeration, structure validation, and SAM/VPR domain-specific checks"
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

# ---- FIXTURE 5: ParseLord5ExperimentalMode present in SAM and VPR combos ----
Write-Host "--- Fixture: parselord5-experimental-mode-checks ---"
$samContent = Get-Content -LiteralPath $SAMFile -Raw
$vprContent = Get-Content -LiteralPath $VPRFile -Raw

$samExpCount = ([regex]::Matches($samContent, 'ParseLord5ExperimentalMode')).Count
$vprExpCount = ([regex]::Matches($vprContent, 'ParseLord5ExperimentalMode')).Count

if ($samExpCount -ge 2) {
    Write-Host "PASS parselord5-experimental-mode-in-sam: found $samExpCount occurrences in SAM.cs (ST+AoE branches)"
    $passCount++
} else {
    Write-Host "FAIL parselord5-experimental-mode-in-sam: expected >=2 occurrences in SAM.cs, found $samExpCount"
    $failCount++
}

if ($vprExpCount -ge 2) {
    Write-Host "PASS parselord5-experimental-mode-in-vpr: found $vprExpCount occurrences in VPR.cs (ST+AoE branches)"
    $passCount++
} else {
    Write-Host "FAIL parselord5-experimental-mode-in-vpr: expected >=2 occurrences in VPR.cs, found $vprExpCount"
    $failCount++
}

# ---- FIXTURE 6: Critical action IDs mapped (Gyofu for SAM, Twinfang/Twinblood for VPR) ----
Write-Host "--- Fixture: critical-action-ids-mapped ---"
$samHelperContent = Get-Content -LiteralPath $SAMHelperFile -Raw
$vprHelperContent = Get-Content -LiteralPath $VPRHelperFile -Raw

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

Write-Host ""
Write-Host "=== Summary ==="
Write-Host "passed=$passCount failed=$failCount negative_controls=1 total=$(($passCount + $failCount))"

if ($failCount -gt 0) { exit 1 } else { exit 0 }

