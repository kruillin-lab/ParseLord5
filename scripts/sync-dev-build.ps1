<#
.SYNOPSIS
  Pull ParseLord5 from GitHub, build to Dalamud devPlugins, optionally run domain evals.

.DESCRIPTION
  Automates the Windows leg after a Cursor Cloud Agent (or any) push:
    fetch -> pull (if behind) -> dotnet build -> rotation-evals

  Output DLL (Debug or Release) lands in:
    %AppData%\XIVLauncher\devPlugins\ParseLord5\

  In-game reload still requires FFXIV + Dalamud running with Dev Plugin + Auto Reload.
  This script does not start the game or toggle plugins.

.PARAMETER Remote
  Git remote name (default: origin).

.PARAMETER Branch
  Branch to track (default: main).

.PARAMETER Configuration
  dotnet build configuration (default: Debug for dev loop).

.PARAMETER SkipPull
  Only build + evals; do not fetch/pull.

.PARAMETER SkipEvals
  Skip scripts/rotation-evals.ps1.

.PARAMETER SubmoduleUpdate
  Run git submodule update --init --recursive before build.

.PARAMETER Notify
  Windows toast on success/failure (uses BurntToast if installed).

.EXAMPLE
  .\scripts\sync-dev-build.ps1

.EXAMPLE
  .\scripts\sync-dev-build.ps1 -Notify

.EXAMPLE
  .\scripts\sync-dev-build.ps1 -Branch cursor/my-agent-branch -Remote origin
#>
[CmdletBinding()]
param(
    [string] $Remote = 'origin',
    [string] $Branch = 'main',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $SkipPull,
    [switch] $SkipEvals,
    [switch] $SubmoduleUpdate,
    [switch] $ForceDevPluginOverwrite,
    [switch] $Notify
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ProjectFile = Join-Path $RepoRoot 'WrathCombo\WrathCombo.csproj'
$EvalScript = Join-Path $RepoRoot 'scripts\rotation-evals.ps1'
$DevPluginsDir = Join-Path $env:APPDATA 'XIVLauncher\devPlugins\ParseLord5'
$DllPath = Join-Path $DevPluginsDir 'ParseLord5.dll'

# Refuse to overwrite a devPlugins build made by a different checkout.
$StampPath = Join-Path $DevPluginsDir 'devplugin-source.txt'
if (Test-Path -LiteralPath $StampPath) {
    $stampedRoot = (Get-Content -LiteralPath $StampPath -Raw).Trim()
    if ($stampedRoot -and
        (-not $stampedRoot.Equals($RepoRoot.Path, [System.StringComparison]::OrdinalIgnoreCase)) -and
        -not $ForceDevPluginOverwrite) {
        throw "devPlugins ParseLord5 was built from '$stampedRoot', not '$($RepoRoot.Path)'. Build the stamped checkout, or pass -ForceDevPluginOverwrite."
    }
}

function Send-SyncNotification {
    param([string] $Title, [string] $Message, [bool] $Success)
    if (-not $Notify) { return }
    if (Get-Module -ListAvailable -Name BurntToast) {
        Import-Module BurntToast -ErrorAction SilentlyContinue
        if ($Success) {
            New-BurntToastNotification -Text $Title, $Message | Out-Null
        } else {
            New-BurntToastNotification -Text $Title, $Message -Sound 'Alarm' | Out-Null
        }
        return
    }
    [void][System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms')
    [System.Windows.Forms.MessageBox]::Show($Message, $Title) | Out-Null
}

Push-Location $RepoRoot
try {
    Write-Host "=== ParseLord5 sync-dev-build ==="
    Write-Host "Repo:    $RepoRoot"
    Write-Host "Remote:  $Remote  Branch: $Branch  Config: $Configuration"
    Write-Host ""

    if (-not $SkipPull) {
        $currentBranch = (git rev-parse --abbrev-ref HEAD 2>$null).Trim()
        if ($currentBranch -ne $Branch) {
            Write-Warning "Current branch is '$currentBranch', not '$Branch'. Pull targets '$Branch' via refspec."
        }

        Write-Host "--- git fetch $Remote ---"
        git fetch $Remote --prune

        $remoteRef = "$Remote/$Branch"
        $localHead = (git rev-parse HEAD).Trim()
        $remoteHead = (git rev-parse $remoteRef 2>$null).Trim()
        if (-not $remoteHead) {
            throw "Remote ref '$remoteRef' not found. Check remote name and branch."
        }

        if ($localHead -eq $remoteHead) {
            Write-Host "Already up to date with $remoteRef ($($localHead.Substring(0, 7)))."
        } else {
            Write-Host "Updating: $localHead -> $remoteHead"
            git pull $Remote $Branch
        }
    }

    if ($SubmoduleUpdate) {
        Write-Host "--- git submodule update ---"
        git submodule update --init --recursive
    }

    Write-Host "--- dotnet build ($Configuration) ---"
    dotnet build $ProjectFile -c $Configuration --nologo -v minimal
    if (-not (Test-Path -LiteralPath $DllPath)) {
        throw "Build succeeded but DLL missing: $DllPath"
    }
    Set-Content -LiteralPath $StampPath -Value $RepoRoot.Path -Encoding UTF8
    $dllTime = (Get-Item -LiteralPath $DllPath).LastWriteTime
    Write-Host "Built: $DllPath ($dllTime)"

    if (-not $SkipEvals) {
        Write-Host "--- rotation-evals ---"
        & $EvalScript
    }

    Write-Host ""
    Write-Host "Done. If FFXIV is open: Dev Plugin ParseLord5 + Auto Reload should pick up the new DLL."
    Write-Host "Otherwise start XIVLauncher, enable dev plugin, then /pl5 or /wrath."
    Send-SyncNotification -Title 'ParseLord5 sync' -Message "Built $Configuration -> devPlugins ($($dllTime.ToString('HH:mm')))" -Success $true
}
catch {
    Write-Host ""
    Write-Error $_
    Send-SyncNotification -Title 'ParseLord5 sync failed' -Message $_.Exception.Message -Success $false
    exit 1
}
finally {
    Pop-Location
}