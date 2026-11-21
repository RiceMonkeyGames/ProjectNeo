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
    $backlogDir = Join-Path $root 'Docs\backlog'
    if (-not (Test-Path -LiteralPath $backlogDir -PathType Container)) {
        Write-Output "ERROR=Backlog directory not found: Docs/backlog"
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

    $idValid = ($TaskId -match '^[A-Za-z0-9-]+$')
    $prefix = Get-TaskPrefix -TaskId $TaskId
    $suggestedBranch = if ($prefix -eq 'BUG') { "fix/$($TaskId.ToLowerInvariant())" } else { "feature/$($TaskId.ToLowerInvariant())" }
    $taskRel = "Docs/backlog/$TaskId.md"
    $taskFull = Join-Path $root ($taskRel -replace '/', '\')
    $historyRel = Find-TaskHistoryPath -Root $root -TaskId $TaskId

    Write-Output "KST_NOW=$kst"
    Write-Output "CURRENT_BRANCH=$branch"
    Write-Output "DIRTY_FILES=$($dirty.Count)"
    if ($aheadBehind) { Write-Output "HEAD_VS_ORIGIN_DEVELOP=$aheadBehind" }
    Write-Output "ID_FORMAT_VALID=$idValid"
    Write-Output "SUGGESTED_BRANCH=$suggestedBranch"
    Write-Output "TASK_FILE=$taskRel"

    if (Test-Path -LiteralPath $taskFull -PathType Leaf) {
        $meta = Read-TaskFrontmatter -Path $taskFull -Root $root
        Write-Output "IN_BACKLOG=True"
        Write-Output "IN_HISTORY=$([bool]$historyRel)"
        Write-Output "TASK_STATUS=$($meta.Status)"
        Write-Output "TASK_ASSIGNEE=$($meta.Assignee)"
        if ($meta.Status -ieq 'In Progress') {
            Write-Output "CLAIM_OK=False (already In Progress by $($meta.Assignee))"
            exit 3
        }
        if ($meta.Status -ieq 'Done') {
            Write-Output "CLAIM_OK=False (already Done; remove active file $taskRel)"
            exit 3
        }
        Write-Output "CLAIM_OK=True"
    } else {
        Write-Output "IN_BACKLOG=False"
        if ($historyRel) {
            Write-Output "IN_HISTORY=True ($historyRel)"
            Write-Output "CLAIM_OK=False (already completed in $historyRel)"
            exit 3
        }
        Write-Output "IN_HISTORY=False"
        if (-not $idValid) {
            Write-Output "CLAIM_OK=False (new ID must match ^[A-Za-z0-9-]+$)"
            exit 4
        }
        Write-Output "CLAIM_OK=True (new task; create $taskRel)"
    }
} finally {
    Pop-Location
}
