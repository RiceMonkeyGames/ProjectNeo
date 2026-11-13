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
            $buildRes = Invoke-StdoutOnly -FilePath 'dotnet' -Arguments @('build', $csproj, '--no-restore', '-nologo', '-clp:ErrorsOnly')
            if ($buildRes.ExitCode -ne 0) {
                Write-Output "DOTNET_PRECHECK=FAILED ($csproj)"
                $errLines = @()
                if ($buildRes.StdOut) { $errLines += ($buildRes.StdOut -split "`r?`n") }
                if ($buildRes.StdErr) { $errLines += ($buildRes.StdErr -split "`r?`n") }
                Write-BoundedLines -Lines ($errLines | Where-Object { $_.Trim() }) -MaxLines $MaxErrorLines -TruncateHint "run dotnet build $csproj --no-restore for full output"
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

    # 3. Ensure editor is neither playing nor compiling mid-flight
    $guard = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', '"playing=" + UnityEditor.EditorApplication.isPlaying + " compiling=" + UnityEditor.EditorApplication.isCompiling') + $projArgs)
    if ($guard.ExitCode -ne 0 -or $guard.StdOut -match 'playing=True' -or $guard.StdOut -match 'compiling=True') {
        Write-Output "UNITY_GUARD=BLOCKED ($($guard.StdOut))"
        exit 3
    }

    # 4. Refresh & Compile
    $null = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); "requested"') + $projArgs)

    $ready = $false
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $waitRes = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('status', '--until-ready', '--timeout', '30') + $projArgs)
        if ($waitRes.ExitCode -eq 0 -and $waitRes.StdOut -match '(?i)ready') {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) {
        Write-Output "COMPILE=TIMEOUT (unity status did not return ready)"
        exit 4
    }

    $compCheck = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', '"failed=" + UnityEditor.EditorUtility.scriptCompilationFailed + " compiling=" + UnityEditor.EditorApplication.isCompiling') + $projArgs)
    if ($compCheck.StdOut -match 'failed=True') {
        Write-Output "COMPILE=FAILED"
        $editorLog = Join-Path $root 'Logs\Editor.log'
        if (Test-Path $editorLog) {
            $csErrors = @(Select-String -Path $editorLog -Pattern 'error CS' -SimpleMatch -Encoding UTF8 | Select-Object -Last $MaxErrorLines | ForEach-Object { $_.Line.Trim() })
            Write-BoundedLines -Lines $csErrors -MaxLines $MaxErrorLines -TruncateHint "search error CS in Logs/Editor.log"
        }
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
        $editLines = @($editRes.StdOut -split "`r?`n" | Where-Object { $_.Trim() })
        if ($editRes.ExitCode -ne 0) {
            Write-Output "EDITMODE=FAILED"
            $failLines = @($editLines | Where-Object { $_ -match '(?i)fail|error|exception|passed|total' })
            if ($failLines.Count -eq 0) { $failLines = $editLines }
            Write-BoundedLines -Lines $failLines -MaxLines $MaxErrorLines
            exit 5
        }
        $summary = ($editLines | Where-Object { $_ -match '(?i)passed|total|result' } | Select-Object -Last 3) -join ' | '
        if (-not $summary) { $summary = ($editLines | Select-Object -Last 1) }
        Write-Output "EDITMODE=PASSED ($summary)"
    }

    # 6. PlayMode tests
    if ($Mode -in @('all', 'play')) {
        $null = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'editor', 'set_auto_tick', '--caller', 'plugin', '--skill', 'unity-cli') + $projArgs)
        $playStart = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'test', 'run', '--caller', 'plugin', '--skill', 'unity-cli', '--mode', 'playmode', '--async-tests', '--result-only') + $commonFilter + $projArgs)
        if ($playStart.ExitCode -ne 0) {
            Write-Output "PLAYMODE=START_FAILED ($($playStart.StdOut))"
            exit 6
        }

        $playDone = $false
        $playStatusOut = ''
        $deadline = (Get-Date).AddSeconds(180)
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
            $stRes = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'test', 'status', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only') + $projArgs)
            $playStatusOut = $stRes.StdOut
            if ($playStatusOut -match '(?i)completed|finished|passed|failed') {
                if ($playStatusOut -notmatch '(?i)\brunning\b') {
                    $playDone = $true
                    break
                }
            }
        }

        # Ensure PlayMode finishes exiting before returning control
        $exitDeadline = (Get-Date).AddSeconds(15)
        while ((Get-Date) -lt $exitDeadline) {
            $isPlay = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('command', 'script', 'eval', '--caller', 'plugin', '--skill', 'unity-cli', '--result-only', 'UnityEditor.EditorApplication.isPlaying') + $projArgs)
            if ($isPlay.StdOut -match '(?i)false') { break }
            Start-Sleep -Milliseconds 500
        }

        if (-not $playDone) {
            Write-Output "PLAYMODE=TIMEOUT ($playStatusOut)"
            exit 6
        }

        $playLines = @($playStatusOut -split "`r?`n" | Where-Object { $_.Trim() })
        if ($playStatusOut -match '(?i)failed:\s*[1-9]|status:\s*failed|\bfailed\b' -and $playStatusOut -notmatch '(?i)failed:\s*0\b') {
            Write-Output "PLAYMODE=FAILED"
            $failLines = @($playLines | Where-Object { $_ -match '(?i)fail|error|exception|passed|total' })
            if ($failLines.Count -eq 0) { $failLines = $playLines }
            Write-BoundedLines -Lines $failLines -MaxLines $MaxErrorLines
            exit 6
        }
        $pSummary = ($playLines | Where-Object { $_ -match '(?i)passed|total|result' } | Select-Object -Last 3) -join ' | '
        if (-not $pSummary) { $pSummary = ($playLines | Select-Object -Last 1) }
        Write-Output "PLAYMODE=PASSED ($pSummary)"
    }
} finally {
    Pop-Location
}
