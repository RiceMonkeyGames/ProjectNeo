param(
    [switch]$Fetch,
    [switch]$SkipUnity,
    [string]$ProjectPath = '',
    [int]$MaxTasks = 12
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $ProjectPath -and $env:UNITY_PROJECT_PATH) { $ProjectPath = $env:UNITY_PROJECT_PATH }
$projArgs = if ($ProjectPath) { @('--project-path', $ProjectPath) } else { @() }

Push-Location $root
try {
    $kst = Get-KstTimestamp
    if ($Fetch) {
        $fetchRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('fetch', 'origin')
        if ($fetchRes.ExitCode -ne 0) {
            Write-Output "FETCH=FAILED"
        }
    }

    $branchRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('branch', '--show-current')
    $branch = if ($branchRes.ExitCode -eq 0 -and $branchRes.StdOut) { $branchRes.StdOut.Trim() } else { 'DETACHED' }

    # Submodule status per AGENTS.md session-start rule
    $subRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('submodule', 'status')
    $subRaw = if ($subRes.ExitCode -eq 0 -and $subRes.StdOut) { @($subRes.StdOut -split "`r?`n" | Where-Object { $_.Trim() }) } else { @() }
    $subState = 'NONE'
    $subDetail = ''
    if ($subRaw.Count -gt 0) {
        $needsInit = @($subRaw | Where-Object { $_ -match '^-' })
        $diverged  = @($subRaw | Where-Object { $_ -match '^\+' })
        $conflict  = @($subRaw | Where-Object { $_ -match '^U' })
        if ($conflict.Count -gt 0) {
            $subState = 'CONFLICT'
            $subDetail = ($conflict -join '; ')
        } elseif ($needsInit.Count -gt 0) {
            $subState = 'NEEDS_INIT'
            $subDetail = 'run: git submodule update --init --recursive'
        } elseif ($diverged.Count -gt 0) {
            $subState = 'DIVERGED'
            $subDetail = 'run: git submodule update --init --recursive'
        } else {
            $subState = 'CLEAN'
        }
    }

    $dirtyRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('status', '--porcelain')
    $dirty = if ($dirtyRes.ExitCode -eq 0 -and $dirtyRes.StdOut) { @($dirtyRes.StdOut -split "`r?`n" | Where-Object { $_.Trim() }) } else { @() }
    $aheadBehind = ''
    $abRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('rev-list', '--left-right', '--count', 'HEAD...origin/develop')
    if ($abRes.ExitCode -eq 0 -and $abRes.StdOut) {
        $parts = $abRes.StdOut.Trim() -split '\s+'
        if ($parts.Count -ge 2) {
            $aheadBehind = "ahead=$($parts[0]) behind=$($parts[1])"
        }
    }

    Write-Output "KST_NOW=$kst"
    Write-Output "CURRENT_BRANCH=$branch"
    if ($subDetail) {
        Write-Output "SUBMODULE_STATUS=$subState ($subDetail)"
    } else {
        Write-Output "SUBMODULE_STATUS=$subState"
    }
    Write-Output "DIRTY_FILES=$($dirty.Count)"
    if ($aheadBehind) {
        Write-Output "HEAD_VS_ORIGIN_DEVELOP=$aheadBehind"
    }

    if ($SkipUnity) {
        Write-Output "UNITY_STATUS=SKIPPED (-SkipUnity)"
    } else {
        $unityCmd = Get-Command unity -ErrorAction SilentlyContinue
        if (-not $unityCmd) {
            Write-Output "UNITY_STATUS=MISSING"
        } else {
            $st = Invoke-StdoutOnly -FilePath 'unity' -Arguments (@('status') + $projArgs)
            if ($st.ExitCode -eq 0 -and ($st.StdOut -match '(?i)ready')) {
                Write-Output "UNITY_STATUS=READY"
            } else {
                $oneLine = ($st.StdOut -replace '\r?\n', ' ').Trim()
                if ($oneLine.Length -gt 120) { $oneLine = $oneLine.Substring(0, 120) + '...' }
                Write-Output "UNITY_STATUS=NOT_READY ($oneLine)"
            }
        }
    }

    # Compact active backlog digest from Docs/backlog/*.md frontmatter (top 16 lines per file)
    $allTasks = @(Get-ActiveBacklogTasks -Root $root)
    $waitingCount = @($allTasks | Where-Object { $_.Status -ieq 'Waiting' }).Count
    $inProgressTasks = @($allTasks | Where-Object { $_.Status -ieq 'In Progress' } | Sort-Object Id)

    Write-Output "ACTIVE_BACKLOG_TOTAL=$($allTasks.Count)"
    Write-Output "ACTIVE_WAITING=$waitingCount"
    Write-Output "ACTIVE_IN_PROGRESS=$($inProgressTasks.Count)"
    if ($inProgressTasks.Count -gt 0) {
        $inProgressLines = @()
        foreach ($t in $inProgressTasks) {
            if ($t.Branch) {
                $inProgressLines += "  - $($t.Id) | $($t.Assignee) | $($t.Branch)"
            } else {
                $inProgressLines += "  - $($t.Id) | $($t.Assignee)"
            }
        }
        Write-BoundedLines -Lines $inProgressLines -MaxLines $MaxTasks -TruncateHint "run Tools/Agent/backlog-list.ps1 -Status 'In Progress'"
    }
} finally {
    Pop-Location
}
