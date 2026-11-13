param(
    [Parameter(Mandatory = $true)]
    [string]$TaskId,
    [switch]$NoFetch
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    $kst = Get-KstTimestamp
    $backlogPath = Join-Path $root 'Docs\Backlog.md'
    if (-not (Test-Path $backlogPath)) {
        Write-Output "ERROR=Backlog file not found: Docs/Backlog.md"
        exit 2
    }

    if (-not $NoFetch) {
        $null = & git fetch origin 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Output "ERROR=git fetch origin failed"
            exit 2
        }
    }

    $branch = (& git branch --show-current).Trim()
    $dirty = @(& git status --porcelain)
    $aheadBehind = ''
    $abRaw = (& git rev-list --left-right --count HEAD...origin/develop 2>$null)
    if ($LASTEXITCODE -eq 0 -and $abRaw) { $aheadBehind = $abRaw.Trim() }

    $rows = Get-Content $backlogPath -Encoding UTF8 | Where-Object { $_ -match '^\|' }
    $matchedLine = $null
    foreach ($r in $rows) {
        $cols = $r -split '\|'
        if ($cols.Count -ge 6 -and $cols[1].Trim() -eq $TaskId) {
            $matchedLine = $r
            break
        }
    }

    $idValid = ($TaskId -match '^[A-Za-z0-9-]+$')
    $prefix = Get-TaskPrefix -TaskId $TaskId
    $suggestedBranch = if ($prefix -eq 'BUG') { "fix/$($TaskId.ToLowerInvariant())" } else { "feature/$($TaskId.ToLowerInvariant())" }

    Write-Output "KST_NOW=$kst"
    Write-Output "CURRENT_BRANCH=$branch"
    Write-Output "DIRTY_FILES=$($dirty.Count)"
    if ($aheadBehind) { Write-Output "HEAD_VS_ORIGIN_DEVELOP=$aheadBehind" }
    Write-Output "ID_FORMAT_VALID=$idValid"
    Write-Output "SUGGESTED_BRANCH=$suggestedBranch"

    if ($matchedLine) {
        $cols = $matchedLine -split '\|'
        $status = $cols[3].Trim()
        $assignee = $cols[4].Trim()
        Write-Output "IN_BACKLOG=True"
        Write-Output "TASK_STATUS=$status"
        Write-Output "TASK_ASSIGNEE=$assignee"
        if ($status -eq 'In Progress') {
            Write-Output "CLAIM_OK=False (already In Progress by $assignee)"
            exit 3
        }
        if ($status -eq 'Done') {
            Write-Output "CLAIM_OK=False (already Done)"
            exit 3
        }
        Write-Output "CLAIM_OK=True"
    } else {
        Write-Output "IN_BACKLOG=False"
        if (-not $idValid) {
            Write-Output "CLAIM_OK=False (new ID must match ^[A-Za-z0-9-]+$)"
            exit 4
        }
        Write-Output "CLAIM_OK=True (new task; register in Docs/Backlog.md)"
    }
} finally {
    Pop-Location
}
