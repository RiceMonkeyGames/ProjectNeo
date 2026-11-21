param(
    [string]$BaseRef = 'origin/develop',
    [switch]$NoFetch,
    [int]$MaxLines = 35
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    if (-not $NoFetch) {
        $null = & git fetch origin 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Output "ERROR=git fetch origin failed"
            exit 2
        }
    }

    $behind = [int]((& git rev-list --count "HEAD..$BaseRef" 2>$null).Trim())
    $ahead = [int]((& git rev-list --count "$BaseRef..HEAD" 2>$null).Trim())
    Write-Output "BASE_REF=$BaseRef"
    Write-Output "COMMITS_BEHIND=$behind"
    Write-Output "COMMITS_AHEAD=$ahead"

    if ($behind -eq 0) {
        Write-Output "UP_TO_DATE=True"
        exit 0
    }

    Write-Output "--- CHANGED FILES ($BaseRef vs HEAD) ---"
    $files = @(& git diff --name-only "HEAD..$BaseRef")
    Write-BoundedLines -Lines $files -MaxLines 20 -TruncateHint "run git diff --name-only HEAD..$BaseRef"

    Write-Output "--- BACKLOG DIFF ---"
    $backlogChanges = @(& git diff --name-status "HEAD..$BaseRef" -- Docs/backlog/)
    $backlogSummary = @()
    foreach ($entry in $backlogChanges) {
        if (-not $entry.Trim()) { continue }
        $cols = $entry -split "`t"
        if ($cols.Count -lt 2) { continue }
        $code = $cols[0].Trim()
        $relPath = $cols[$cols.Count - 1].Trim()
        $taskId = [System.IO.Path]::GetFileNameWithoutExtension($relPath)
        if ($code -eq 'D') {
            $backlogSummary += "  - [D] $taskId (removed/completed)"
        } else {
            $showRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('show', "${BaseRef}:$relPath")
            if ($showRes.ExitCode -eq 0 -and $showRes.StdOut) {
                $top = @($showRes.StdOut -split "`r?`n" | Select-Object -First 16)
                $meta = Read-TaskFrontmatterFromLines -Lines $top -FilePath $relPath -RelativePath $relPath
                $branchPart = if ($meta.Branch) { " | $($meta.Branch)" } else { '' }
                $backlogSummary += "  - [$code] $($meta.Id) | $($meta.Status) | $($meta.Assignee)$branchPart | $($meta.Title)"
            } else {
                $backlogSummary += "  - [$code] $taskId"
            }
        }
    }
    Write-BoundedLines -Lines $backlogSummary -MaxLines 15

    Write-Output "--- RUNTIME PUBLIC API CHANGES ---"
    $apiDiff = @(& git diff -U0 "HEAD..$BaseRef" -- 'Assets/_Project/Scripts/Runtime/*.cs' 'Assets/_Project/Scripts/Runtime/**/*.cs' |
        Where-Object { $_ -match '^[+-]\s*(public|internal)\s+(class|struct|interface|enum|event|delegate|static|virtual|override|readonly|void|bool|int|float|string|[A-Z]\w*)' })
    Write-BoundedLines -Lines $apiDiff -MaxLines $MaxLines -TruncateHint "inspect specific Runtime files"
} finally {
    Pop-Location
}
