<#
.SYNOPSIS
  BugCam development checkpoint runner (EditMode / PlayMode) for Unity 6000.3.21f1.

.DESCRIPTION
  Repository-portable automation for batchmode Unity Test Runner suites used by
  BugCam Blocks 1.1–1.3. This is development tooling only — not a BugCam Core
  runtime dependency and not shipped product behavior.

  Resolves the Unity project root as the parent of Tools/ (this script lives in
  Tools/BugCam/). Writes NUnit XML + Editor logs under Library/BugCamEvidence/
  (gitignored). Does not modify ProjectSettings, Core source, or research docs.

  Contracts preserved by the tests themselves (not altered here):
    state stride 14, repeatability gate 1e-6, TowerScene 49 bodies,
    250 steps @ dt 0.02, Enhanced Determinism OFF.

.PARAMETER Suite
  Which suite(s) to run: EditMode | PlayMode | All (default All).

.PARAMETER UnityExe
  Optional full path to Unity.exe. When omitted, discovers 6000.3.21f1 via
  Unity Hub editors.json and common Hub install locations.

.PARAMETER EvidenceDir
  Relative or absolute evidence directory. Default:
  Library/BugCamEvidence/checkpoint-runner

.PARAMETER MaxSeconds
  Per-suite wall-clock timeout (default 900).

.PARAMETER StallSeconds
  Kill Unity if the log file stops growing for this many seconds (default 300).

.PARAMETER KeepUnityAlive
  Do not kill leftover Unity processes before launch (default kills 6000.3.21f1).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Tools\BugCam\run-checkpoint.ps1

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Tools\BugCam\run-checkpoint.ps1 -Suite EditMode

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Tools\BugCam\run-checkpoint.ps1 -Suite PlayMode -UnityExe "C:\Path\To\Unity.exe"

.OUTPUTS
  Exit code 0 when every requested suite passes.
  Non-zero when Unity is missing, a suite fails, stalls, or times out.
  Evidence (gitignored):
    <EvidenceDir>\EditMode.xml / EditMode.unity.log
    <EvidenceDir>\PlayMode.xml / PlayMode.unity.log
#>
[CmdletBinding()]
param(
    [ValidateSet("EditMode", "PlayMode", "All")]
    [string]$Suite = "All",

    [string]$UnityExe = "",

    [string]$EvidenceDir = "",

    [int]$MaxSeconds = 900,

    [int]$StallSeconds = 300,

    [switch]$KeepUnityAlive
)

$ErrorActionPreference = "Stop"
$RequiredUnityVersion = "6000.3.21f1"

function Get-ProjectRoot {
    # Tools/BugCam/run-checkpoint.ps1 → repo/Unity project root
    return (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

function Find-UnityExe {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "UnityExe not found: $ExplicitPath"
        }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $candidates = New-Object System.Collections.Generic.List[string]

    foreach ($envName in @("UNITY_EDITOR", "UNITY_PATH", "BUGCAM_UNITY_EXE")) {
        $envVal = [Environment]::GetEnvironmentVariable($envName)
        if (-not [string]::IsNullOrWhiteSpace($envVal) -and (Test-Path -LiteralPath $envVal)) {
            [void]$candidates.Add($envVal)
        }
    }

    $editorsJson = Join-Path $env:APPDATA "UnityHub\editors.json"
    if (Test-Path -LiteralPath $editorsJson) {
        try {
            $json = Get-Content -LiteralPath $editorsJson -Raw -ErrorAction Stop | ConvertFrom-Json
            foreach ($prop in $json.PSObject.Properties) {
                $loc = $null
                if ($null -ne $prop.Value.location) { $loc = [string]$prop.Value.location }
                elseif ($null -ne $prop.Value.path) { $loc = [string]$prop.Value.path }
                elseif ($prop.Value -is [string]) { $loc = [string]$prop.Value }
                if ([string]::IsNullOrWhiteSpace($loc)) { continue }
                if ($loc -like "*$RequiredUnityVersion*") {
                    $exe = if ($loc -like "*Unity.exe") { $loc } else { Join-Path $loc "Editor\Unity.exe" }
                    if (Test-Path -LiteralPath $exe) { [void]$candidates.Add($exe) }
                }
            }
        }
        catch {
            Write-Host "WARN: could not parse Unity Hub editors.json ($($_.Exception.Message))"
        }
    }

    $programFiles = @(
        ${env:ProgramFiles},
        ${env:ProgramFiles(x86)},
        $env:LOCALAPPDATA
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($root in $programFiles) {
        $hub = Join-Path $root "Unity\Hub\Editor\$RequiredUnityVersion\Editor\Unity.exe"
        if (Test-Path -LiteralPath $hub) { [void]$candidates.Add($hub) }
        $plain = Join-Path $root "Unity\$RequiredUnityVersion\Editor\Unity.exe"
        if (Test-Path -LiteralPath $plain) { [void]$candidates.Add($plain) }
    }

    # Also scan PATH for Unity.exe that sits under a 6000.3.21f1 folder.
    $fromPath = Get-Command "Unity.exe" -ErrorAction SilentlyContinue
    if ($null -ne $fromPath -and $fromPath.Source -like "*$RequiredUnityVersion*") {
        [void]$candidates.Add($fromPath.Source)
    }

    # Shallow, portable drive scan (no hard-coded drive letters or usernames).
    $relativeHints = @(
        "Program Files\Unity\Hub\Editor\$RequiredUnityVersion\Editor\Unity.exe",
        "Program Files\Unity\$RequiredUnityVersion\Editor\Unity.exe",
        "Programs\Unity\Hub\Editor\$RequiredUnityVersion\Editor\Unity.exe",
        "Programs\Unity\$RequiredUnityVersion\Editor\Unity.exe",
        "Unity\Hub\Editor\$RequiredUnityVersion\Editor\Unity.exe",
        "Unity\$RequiredUnityVersion\Editor\Unity.exe"
    )
    foreach ($drive in [System.IO.DriveInfo]::GetDrives()) {
        if (-not $drive.IsReady) { continue }
        if ($drive.DriveType -ne [System.IO.DriveType]::Fixed) { continue }
        foreach ($rel in $relativeHints) {
            $candidate = Join-Path $drive.RootDirectory.FullName $rel
            if (Test-Path -LiteralPath $candidate) {
                [void]$candidates.Add($candidate)
            }
        }
    }

    $unique = $candidates | Select-Object -Unique
    foreach ($exe in $unique) {
        if (Test-Path -LiteralPath $exe) {
            return (Resolve-Path -LiteralPath $exe).Path
        }
    }

    throw @"
Unity $RequiredUnityVersion not found.
Pass -UnityExe with the full path to Editor\Unity.exe, set UNITY_EDITOR, or install $RequiredUnityVersion via Unity Hub.
Searched Hub editors.json, common Hub paths, and shallow fixed-drive Unity install hints.
"@
}

function Stop-UnityTree {
    Get-Process -Name "Unity", "bee_backend", "Unity.ILPP.Runner" -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                $_.Path -like "*$RequiredUnityVersion*"
            }
            catch {
                $false
            }
        } |
        ForEach-Object {
            try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch {}
        }
    Start-Sleep -Seconds 2
}

function Invoke-UnitySuite {
    param(
        [Parameter(Mandatory = $true)][string]$UnityPath,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][ValidateSet("EditMode", "PlayMode")][string]$Platform,
        [Parameter(Mandatory = $true)][string]$ResultsXml,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds,
        [Parameter(Mandatory = $true)][int]$StallTimeoutSeconds
    )

    $assembly = if ($Platform -eq "EditMode") { "BugCam.Tests" } else { "BugCam.Tests.PlayMode" }

    $resultsParent = Split-Path -Parent $ResultsXml
    $logParent = Split-Path -Parent $LogPath
    if (-not [string]::IsNullOrEmpty($resultsParent)) {
        New-Item -ItemType Directory -Force -Path $resultsParent | Out-Null
    }
    if (-not [string]::IsNullOrEmpty($logParent)) {
        New-Item -ItemType Directory -Force -Path $logParent | Out-Null
    }
    if (Test-Path -LiteralPath $ResultsXml) { Remove-Item -LiteralPath $ResultsXml -Force }
    if (Test-Path -LiteralPath $LogPath) { Remove-Item -LiteralPath $LogPath -Force }

    # Quote path-bearing args so spaces (e.g. "AI CAM") survive Start-Process.
    # Do NOT pass -quit with -runTests: Unity can exit before the test runner starts.
    function Quote-Arg([string]$value) {
        if ($value -match '[\s"]') {
            return '"' + ($value -replace '"', '\"') + '"'
        }
        return $value
    }

    $argList = @(
        "-batchmode",
        "-nographics",
        "-projectPath", (Quote-Arg $ProjectPath),
        "-runTests",
        "-testPlatform", $Platform,
        "-assemblyNames", $assembly,
        "-testResults", (Quote-Arg $ResultsXml),
        "-logFile", (Quote-Arg $LogPath)
    )

    Write-Host "LAUNCH: `"$UnityPath`" $($argList -join ' ')"
    Write-Host "SUITE=$Platform ASSEMBLY=$assembly RESULTS=$ResultsXml LOG=$LogPath"

    $proc = Start-Process -FilePath $UnityPath -ArgumentList $argList -PassThru -WindowStyle Hidden
    $started = Get-Date
    $lastSize = -1
    $lastProgress = Get-Date

    while (-not $proc.HasExited) {
        Start-Sleep -Seconds 10
        $elapsed = [int]((Get-Date) - $started).TotalSeconds
        $size = 0
        if (Test-Path -LiteralPath $LogPath) {
            $size = (Get-Item -LiteralPath $LogPath).Length
        }
        Write-Host ("POLL suite={0} t={1}s size={2}" -f $Platform, $elapsed, $size)

        if ($size -gt $lastSize) {
            $lastSize = $size
            $lastProgress = Get-Date
        }

        $stall = [int]((Get-Date) - $lastProgress).TotalSeconds
        if ($stall -ge $StallTimeoutSeconds) {
            Write-Host "STALL: no log growth for ${StallTimeoutSeconds}s — killing Unity"
            try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {}
            Stop-UnityTree
            return 98
        }

        if ($elapsed -ge $TimeoutSeconds) {
            Write-Host "TIMEOUT: exceeded ${TimeoutSeconds}s — killing Unity"
            try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {}
            Stop-UnityTree
            return 99
        }
    }

    $code = $proc.ExitCode
    if ($null -eq $code) { $code = 1 }
    Write-Host ("EXIT suite={0} code={1} elapsed={2}s" -f $Platform, $code, [int]((Get-Date) - $started).TotalSeconds)

    if (-not (Test-Path -LiteralPath $ResultsXml)) {
        Write-Host "ERROR: missing results XML: $ResultsXml"
        if (Test-Path -LiteralPath $LogPath) {
            Select-String -Path $LogPath -Pattern "error CS|Scripts have compiler errors|Test run completed" |
                Select-Object -Last 20 |
                ForEach-Object { Write-Host $_.Line }
        }
        return 2
    }

    try {
        [xml]$xml = Get-Content -LiteralPath $ResultsXml
        $run = $xml.'test-run'
        $total = [int]$run.total
        $passed = [int]$run.passed
        $failed = [int]$run.failed
        $result = [string]$run.result
        Write-Host ("RESULT suite={0} total={1} passed={2} failed={3} result={4}" -f $Platform, $total, $passed, $failed, $result)

        # Suite passes only when: total>=1, failed==0, passed==total,
        # result begins with Passed, and Unity exit code is 0. Empty suite is failure.
        $suiteOk =
            ($total -ge 1) -and
            ($failed -eq 0) -and
            ($passed -eq $total) -and
            ($result.StartsWith("Passed")) -and
            ($code -eq 0)

        if (-not $suiteOk) {
            if ($total -lt 1) {
                Write-Host "ERROR: empty suite (total=$total) is not a pass"
            }
            elseif ($code -ne 0) {
                Write-Host "ERROR: Unity exit code $code with result=$result"
            }
            return 1
        }
    }
    catch {
        Write-Host "ERROR: failed to parse results XML: $($_.Exception.Message)"
        return 3
    }

    return 0
}

# --- main ---
$projectRoot = Get-ProjectRoot
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot "Assets"))) {
    throw "Project root does not look like a Unity project (Assets/ missing): $projectRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot "ProjectSettings"))) {
    throw "Project root does not look like a Unity project (ProjectSettings/ missing): $projectRoot"
}

$unityPath = Find-UnityExe -ExplicitPath $UnityExe
Write-Host "PROJECT=$projectRoot"
Write-Host "UNITY=$unityPath"
Write-Host "SUITE=$Suite"

if ([string]::IsNullOrWhiteSpace($EvidenceDir)) {
    $EvidenceDir = Join-Path $projectRoot "Library\BugCamEvidence\checkpoint-runner"
}
elseif (-not [System.IO.Path]::IsPathRooted($EvidenceDir)) {
    $EvidenceDir = Join-Path $projectRoot $EvidenceDir
}
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
Write-Host "EVIDENCE=$EvidenceDir"

if (-not $KeepUnityAlive) {
    $leftover = Get-Process -Name "Unity" -ErrorAction SilentlyContinue |
        Where-Object {
            try { $_.Path -like "*$RequiredUnityVersion*" } catch { $false }
        }
    if ($leftover) {
        Write-Host "Killing leftover Unity $RequiredUnityVersion processes before launch..."
        Stop-UnityTree
    }
}

$suites = @()
if ($Suite -eq "All" -or $Suite -eq "EditMode") { $suites += "EditMode" }
if ($Suite -eq "All" -or $Suite -eq "PlayMode") { $suites += "PlayMode" }

$overall = 0
foreach ($name in $suites) {
    $xml = Join-Path $EvidenceDir "$name.xml"
    $log = Join-Path $EvidenceDir "$name.unity.log"
    $code = Invoke-UnitySuite `
        -UnityPath $unityPath `
        -ProjectPath $projectRoot `
        -Platform $name `
        -ResultsXml $xml `
        -LogPath $log `
        -TimeoutSeconds $MaxSeconds `
        -StallTimeoutSeconds $StallSeconds
    if ($code -ne 0) {
        $overall = $code
        Write-Host "SUITE_FAILED name=$name exit=$code"
        # Continue so All still attempts the second suite, but overall stays non-zero.
    }
    else {
        Write-Host "SUITE_PASSED name=$name"
    }
}

if ($overall -ne 0) {
    Write-Host "CHECKPOINT_FAILED exit=$overall"
    exit $overall
}

Write-Host "CHECKPOINT_PASSED suites=$($suites -join ',')"
exit 0
