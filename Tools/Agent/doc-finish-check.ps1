param(
    [string]$TaskId = '',
    [string]$BaseBranch = 'develop'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    $kst = Get-KstTimestamp
    $branch = (& git branch --show-current).Trim()
    Write-Output "KST_NOW=$kst"
    Write-Output "CURRENT_BRANCH=$branch"

    if ($TaskId) {
        $prefix = Get-TaskPrefix -TaskId $TaskId
        $histRel = "Docs/history/$prefix/$TaskId.md"
        $histExists = Test-Path (Join-Path $root $histRel)
        Write-Output "HISTORY_PATH=$histRel"
        Write-Output "HISTORY_EXISTS=$histExists"
    }

    $changed = @(& git diff --name-only "$BaseBranch...HEAD" 2>$null)
    $uncommitted = @(& git status --short)
    Write-Output "CHANGED_VS_BASE=$($changed.Count)"
    if ($changed.Count -gt 0) {
        Write-BoundedLines -Lines $changed -MaxLines 20 -TruncateHint "run git diff --name-only $BaseBranch...HEAD"
    }
    Write-Output "UNCOMMITTED_COUNT=$($uncommitted.Count)"
    if ($uncommitted.Count -gt 0) {
        Write-BoundedLines -Lines $uncommitted -MaxLines 20 -TruncateHint "run git status --short"
    }

    # Check active Backlog for unarchived Done rows
    $backlogPath = Join-Path $root 'Docs\Backlog.md'
    $unarchivedDone = @(Select-String -Path $backlogPath -Pattern '\|\s*Done\s*\|' -Encoding UTF8 | ForEach-Object { $_.Line })
    Write-Output "UNARCHIVED_DONE_ROWS=$($unarchivedDone.Count)"

    # Check internal markdown links
    $mdFiles = @()
    if (Test-Path (Join-Path $root 'AGENTS.md')) { $mdFiles += (Join-Path $root 'AGENTS.md') }
    if (Test-Path (Join-Path $root 'Docs')) {
        $mdFiles += (Get-ChildItem (Join-Path $root 'Docs') -Recurse -File -Filter '*.md' | ForEach-Object { $_.FullName })
    }

    $broken = @()
    foreach ($f in $mdFiles) {
        $dir = Split-Path $f -Parent
        $raw = Get-Content $f -Raw -Encoding UTF8
        $matches = [regex]::Matches($raw, '\[[^\]]+\]\(([^)#\s]+)(?:#[^)]+)?\)')
        foreach ($m in $matches) {
            $target = $m.Groups[1].Value
            if (-not $target -or $target -match '^https?://') { continue }
            $resolved = Join-Path $dir $target
            if (-not (Test-Path $resolved)) {
                $relFile = $f.Replace($root + '\', '') -replace '\\', '/'
                $broken += "$relFile -> $target"
            }
        }
    }

    Write-Output "BROKEN_LINKS=$($broken.Count)"
    if ($broken.Count -gt 0) {
        Write-BoundedLines -Lines $broken -MaxLines 20
    }

    $null = & git diff --check
    $diffCheckOk = ($LASTEXITCODE -eq 0)
    Write-Output "GIT_DIFF_CHECK_OK=$diffCheckOk"

    if ($broken.Count -gt 0 -or -not $diffCheckOk) { exit 2 }
} finally {
    Pop-Location
}
