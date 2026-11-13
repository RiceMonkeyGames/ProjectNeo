param(
    [ValidateSet('all', 'edit', 'play', 'compile')]
    [string]$Mode = 'all',
    [string]$Filter = '',
    [switch]$SkipDotnet,
    [switch]$IncludeExplicit,
    [int]$MaxErrorLines = 30,
    [string]$ProjectPath = '',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

function Try-ParseJson {
    param([string]$Text)
    if (-not $Text) { return $null }
    $trim = $Text.Trim()
    if (-not ($trim.StartsWith('{') -or $trim.StartsWith('['))) { return $null }
    try { return ($trim | ConvertFrom-Json) } catch { return $null }
}

function Format-UnityErrors {
    param($Json, [string]$RawOut, [string]$RawErr, [int]$MaxLines)
    $lines = @()
    if ($Json -and $Json.errors) {
        foreach ($e in $Json.errors) {
            if ($e.message) { $lines += "$($e.code): $($e.message)" }
            else { $lines += "$e" }
        }
    }
    if ($Json -and $Json.results) {
        foreach ($r in $Json.results) {
            if ($r.Status -and $r.Status -ne 'Passed' -and $r.Status -ne 'Skipped') {
                $msg = if ($r.Message) { " -> $($r.Message -replace '\r?\n', ' ')" } else { '' }
                $lines += "FAIL $($r.FullName)$msg"
            }
        }
    }
    if ($lines.Count -eq 0) {
        if ($RawOut) { $lines += ($RawOut -split "`r?`n" | Where-Object { $_.Trim() }) }
        if ($RawErr) { $lines += ($RawErr -split "`r?`n" | Where-Object { $_.Trim() }) }
    }
    Write-BoundedLines -Lines $lines -MaxLines $MaxLines
}

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $ProjectPath -and $env:UNITY_PROJECT_PATH) { $ProjectPath = $env:UNITY_PROJECT_PATH }
$projArgs = if ($ProjectPath) { @('--project-path', $ProjectPath) } else { @() }

Push-Location $root
try {
    # 1. Fast C# pre-check via dotnet build (no Unity round-trip)
    if (-not $SkipDotnet) {
        $csproj = @('ProjectNeo.Tests.EditMode.csproj', 'ProjectNeo.Runtime.csproj') | Where-Object { Test-Path (Join-Path $root $_) } | Select-Object -First 1
        $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($csproj -and $dotnetCmd) {
            $env:DOTNET_NOLOGO = '1'
            $projBase = [System.IO.Path]::GetFileNameWithoutExtension($csproj)
            $hasAssets = (Test-Path (Join-Path $root "Temp\obj\$projBase\project.assets.json")) -or (Test-Path (Join-Path $root "obj\$projBase\project.assets.json"))
            $buildArgs = if ($hasAssets) { @('build', $csproj, '--no-restore', '-nologo', '-clp:ErrorsOnly') } else { @('build', $csproj, '-nologo', '-clp:ErrorsOnly') }
            $buildRes = Invoke-StdoutOnly -FilePath 'dotnet' -Arguments $buildArgs
            if ($buildRes.ExitCode -ne 0) {
                Write-Output "DOTNET_PRECHECK=FAILED ($csproj)"
                $errLines = @()
                if ($buildRes.StdOut) { $errLines += ($buildRes.StdOut -split "`r?`n") }
                if ($buildRes.StdErr) { $errLines += ($buildRes.StdErr -split "`r?`n") }
                Write-BoundedLines -Lines ($errLines | Where-Object { $_.Trim() }) -MaxLines $MaxErrorLines -TruncateHint "run dotnet build $csproj for full output"
                exit 2
            }
            Write-Output "DOTNET_PRECHECK=PASSED ($csproj)"
        } else {
            Write-Output "DOTNET_PRECHECK=SKIPPED (no csproj or dotnet)"
        }
    } else {
        Write-Output "DOTNET_PRECHECK=SKIPPED (-SkipDotnet)"
    }

    if ($DryRun) {
        Write-Output "DRY_RUN=True (Mode=$Mode, Filter=$Filter)"
        exit 0
    }

    $unityCmd = Get-Command unity -ErrorAction SilentlyContinue
    if (-not $unityCmd) {
        Write-Output "UNITY_STATUS=MISSING (unity CLI not found in PATH)"
        exit 3
    }

    # 2. Check Unity editor status
    $st = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('status') + $projArgs)
    if ($st.ExitCode -ne 0 -or ($st.StdOut -notmatch '(?i)ready')) {
        Write-Output "UNITY_STATUS=NOT_READY ($($st.StdOut))"
        exit 3
    }

    # 3. Guard: ensure editor is neither playing nor compiling mid-flight (zero quotes/spaces in eval arg)
    $guard = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', 'UnityEditor.EditorApplication.isPlaying||UnityEditor.EditorApplication.isCompiling') + $projArgs)
    $guardJson = Try-ParseJson $guard.StdOut
    if ($guard.ExitCode -ne 0 -or -not $guardJson -or -not $guardJson.success -or $guardJson.result -eq $true) {
        Write-Output "UNITY_GUARD=BLOCKED ($($guard.StdOut))"
        exit 3
    }

    # 4. Refresh & Compile
    $refreshRes = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();') + $projArgs)
    $refreshJson = Try-ParseJson $refreshRes.StdOut
    if ($refreshRes.ExitCode -ne 0 -or ($refreshJson -and -not $refreshJson.success)) {
        Write-Output "COMPILE=REQUEST_FAILED"
        Format-UnityErrors -Json $refreshJson -RawOut $refreshRes.StdOut -RawErr $refreshRes.StdErr -MaxLines $MaxErrorLines
        exit 4
    }

    Start-Sleep -Milliseconds 500
    $ready = $false
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        $waitRes = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('status', '--until-ready', '--timeout', '30') + $projArgs)
        if ($waitRes.ExitCode -eq 0 -and $waitRes.StdOut -match '(?i)ready') {
            $chk = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', 'UnityEditor.EditorUtility.scriptCompilationFailed||UnityEditor.EditorApplication.isCompiling') + $projArgs)
            $chkJson = Try-ParseJson $chk.StdOut
            if ($chk.ExitCode -eq 0 -and $chkJson -and $chkJson.success) {
                $compFailed = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', 'UnityEditor.EditorUtility.scriptCompilationFailed') + $projArgs)
                $compFailedJson = Try-ParseJson $compFailed.StdOut
                if ($compFailedJson -and $compFailedJson.result -eq $true) {
                    Write-Output "COMPILE=FAILED"
                    $editorLog = Join-Path $root 'Logs\Editor.log'
                    if (Test-Path $editorLog) {
                        $csErrors = @(Select-String -Path $editorLog -Pattern 'error CS' -SimpleMatch -Encoding UTF8 | Select-Object -Last $MaxErrorLines | ForEach-Object { $_.Line.Trim() })
                        Write-BoundedLines -Lines $csErrors -MaxLines $MaxErrorLines -TruncateHint "search error CS in Logs/Editor.log"
                    }
                    exit 4
                }
                if ($chkJson.result -eq $false) {
                    $ready = $true
                    break
                }
            }
        }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) {
        Write-Output "COMPILE=TIMEOUT (unity status or compile did not settle)"
        exit 4
    }
    Write-Output "COMPILE=PASSED"

    if ($Mode -eq 'compile') { exit 0 }

    $commonFilter = @()
    if ($Filter) { $commonFilter += @('--filter', $Filter) }
    if ($IncludeExplicit) { $commonFilter += '--include-explicit' }

    # 5. EditMode tests
    if ($Mode -in @('all', 'edit')) {
        $editRes = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'test', 'run', '--caller', 'plugin', '--skill', 'unity-cli', '--mode', 'editor', '--result-only') + $commonFilter + $projArgs)
        $editJson = Try-ParseJson $editRes.StdOut
        $editOk = ($editRes.ExitCode -eq 0) -and $editJson -and $editJson.success -and ($editJson.Summary.Failed -eq 0)
        if (-not $editOk) {
            Write-Output "EDITMODE=FAILED"
            Format-UnityErrors -Json $editJson -RawOut $editRes.StdOut -RawErr $editRes.StdErr -MaxLines $MaxErrorLines
            exit 5
        }
        $s = $editJson.Summary
        Write-Output "EDITMODE=PASSED (Total=$($s.Total) Passed=$($s.Passed) Failed=$($s.Failed) Skipped=$($s.Skipped) Duration=$($editJson.Duration)s)"
    }

    # 6. PlayMode tests
    if ($Mode -in @('all', 'play')) {
        $null = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'editor', 'set_auto_tick', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only') + $projArgs)
        $playStart = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'test', 'run', '--caller', 'plugin', '--skill', 'unity-cli', '--mode', 'playmode', '--async-tests', '--result-only') + $commonFilter + $projArgs)
        $playStartJson = Try-ParseJson $playStart.StdOut
        if ($playStart.ExitCode -ne 0 -or ($playStartJson -and -not $playStartJson.success)) {
            Write-Output "PLAYMODE=START_FAILED"
            Format-UnityErrors -Json $playStartJson -RawOut $playStart.StdOut -RawErr $playStart.StdErr -MaxLines $MaxErrorLines
            exit 6
        }

        $playDone = $false
        $playJson = $null
        $playRaw = ''
        $deadline = (Get-Date).AddSeconds(180)
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
            $stRes = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'test', 'status', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only') + $projArgs)
            $playRaw = $stRes.StdOut
            $playJson = Try-ParseJson $playRaw
            if ($playJson -and $playJson.status -ne 'running' -and $playJson.status -ne 'Running') {
                $playDone = $true
                break
            }
        }

        # Ensure PlayMode finishes exiting before returning control
        $exitDeadline = (Get-Date).AddSeconds(15)
        while ((Get-Date) -lt $exitDeadline) {
            $isPlay = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', 'UnityEditor.EditorApplication.isPlaying') + $projArgs)
            $isPlayJson = Try-ParseJson $isPlay.StdOut
            if ($isPlayJson -and $isPlayJson.success -and $isPlayJson.result -eq $false) { break }
            Start-Sleep -Milliseconds 500
        }

        if (-not $playDone) {
            Write-Output "PLAYMODE=TIMEOUT ($playRaw)"
            exit 6
        }

        $playOk = $playJson -and $playJson.status -eq 'completed' -and ($playJson.summary.failed -eq 0)
        if (-not $playOk) {
            Write-Output "PLAYMODE=FAILED"
            Format-UnityErrors -Json $playJson -RawOut $playRaw -RawErr '' -MaxLines $MaxErrorLines
            exit 6
        }
        $ps = $playJson.summary
        Write-Output "PLAYMODE=PASSED (Total=$($ps.total) Passed=$($ps.passed) Failed=$($ps.failed) Skipped=$($ps.skipped) Duration=$($playJson.duration)s)"
    }
} finally {
    Pop-Location
}
