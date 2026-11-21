param(
    [string]$TaskId = '',
    [string]$BaseBranch = 'develop',
    [switch]$StrictDocs,
    [switch]$CheckTimestamps
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

function Get-GitLines {
    param([string[]]$GitArgs)
    $res = Invoke-StdoutOnly -FilePath 'git' -Arguments $GitArgs
    if ($res.ExitCode -ne 0 -or -not $res.StdOut) { return @() }
    return @($res.StdOut -split "`r?`n" | Where-Object { $_.Trim() })
}

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    $kst = Get-KstTimestamp
    $branchRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('branch', '--show-current')
    $branch = if ($branchRes.ExitCode -eq 0 -and $branchRes.StdOut) { $branchRes.StdOut.Trim() } else { 'DETACHED' }
    Write-Output "KST_NOW=$kst"
    Write-Output "CURRENT_BRANCH=$branch"

    if ($TaskId) {
        $prefix = Get-TaskPrefix -TaskId $TaskId
        $histRel = "Docs/history/$prefix/$TaskId.md"
        $histExists = Test-Path -LiteralPath (Join-Path $root ($histRel -replace '/', '\')) -PathType Leaf
        $activeRel = "Docs/backlog/$TaskId.md"
        $activeExists = Test-Path -LiteralPath (Join-Path $root ($activeRel -replace '/', '\')) -PathType Leaf
        Write-Output "HISTORY_PATH=$histRel"
        Write-Output "HISTORY_EXISTS=$histExists"
        Write-Output "ACTIVE_BACKLOG_FILE=$activeRel"
        Write-Output "ACTIVE_BACKLOG_EXISTS=$activeExists"
    }

    $changed = @(Get-GitLines -GitArgs @('diff', '--name-only', "$BaseBranch...HEAD"))
    $uncommitted = @(Get-GitLines -GitArgs @('status', '--short'))
    Write-Output "CHANGED_VS_BASE=$($changed.Count)"
    if ($changed.Count -gt 0) {
        Write-BoundedLines -Lines $changed -MaxLines 20 -TruncateHint "run git diff --name-only $BaseBranch...HEAD"
    }
    Write-Output "UNCOMMITTED_COUNT=$($uncommitted.Count)"
    if ($uncommitted.Count -gt 0) {
        Write-BoundedLines -Lines $uncommitted -MaxLines 20 -TruncateHint "run git status --short"
    }

    # Collect all modified/untracked files across branch + working tree
    $allModified = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($list in @(
        $changed,
        @(Get-GitLines -GitArgs @('diff', '--name-only')),
        @(Get-GitLines -GitArgs @('diff', '--cached', '--name-only')),
        @(Get-GitLines -GitArgs @('ls-files', '--others', '--exclude-standard'))
    )) {
        foreach ($item in $list) {
            if ($item) { [void]$allModified.Add(($item.Trim() -replace '\\', '/')) }
        }
    }

    # Validate Docs/backlog/*.md frontmatter and check for unremoved Done tasks
    $activeTasks = @(Get-ActiveBacklogTasks -Root $root)
    $unarchivedDone = @($activeTasks | Where-Object { $_.Status -ieq 'Done' } | ForEach-Object { $_.RelativePath })
    $invalidBacklog = @($activeTasks | Where-Object { -not $_.Valid } | ForEach-Object { $_.RelativePath })
    Write-Output "UNARCHIVED_DONE_FILES=$($unarchivedDone.Count)"
    if ($unarchivedDone.Count -gt 0) {
        Write-BoundedLines -Lines $unarchivedDone -MaxLines 15
    }
    Write-Output "INVALID_BACKLOG_FILES=$($invalidBacklog.Count)"
    if ($invalidBacklog.Count -gt 0) {
        Write-BoundedLines -Lines $invalidBacklog -MaxLines 15
    }

    # Check internal markdown links
    $mdFiles = @()
    if (Test-Path (Join-Path $root 'AGENTS.md')) { $mdFiles += (Join-Path $root 'AGENTS.md') }
    if (Test-Path (Join-Path $root 'Docs')) {
        $mdFiles += (Get-ChildItem (Join-Path $root 'Docs') -Recurse -File -Filter '*.md' | ForEach-Object { $_.FullName })
    }

    $broken = @()
    foreach ($f in $mdFiles) {
        $dir = Split-Path $f -Parent
        $raw = Get-Content -LiteralPath $f -Raw -Encoding UTF8
        $matches = [regex]::Matches($raw, '\[[^\]]+\]\(([^)#\s]+)(?:#[^)]+)?\)')
        foreach ($m in $matches) {
            $target = $m.Groups[1].Value
            if (-not $target -or $target -match '^https?://') { continue }
            $resolved = Join-Path $dir $target
            if (-not (Test-Path -LiteralPath $resolved)) {
                $relFile = $f.Replace($root + '\', '') -replace '\\', '/'
                $broken += "$relFile -> $target"
            }
        }
    }

    Write-Output "BROKEN_LINKS=$($broken.Count)"
    if ($broken.Count -gt 0) {
        Write-BoundedLines -Lines $broken -MaxLines 20
    }

    # Optional Last Updated check (retired by default; Git commit timestamps are source of truth)
    $staleTimestamps = @()
    $koreanLines = @()
    $hangulRegex = '[\uAC00-\uD7A3]'
    $tsFormatRegex = '^Last Updated:\s+\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\s+\(KST\)\s*$'

    $modifiedDocs = @($allModified | Where-Object { $_ -eq 'AGENTS.md' -or $_ -match '^Docs/.*\.md$' } | Sort-Object)
    foreach ($docRel in $modifiedDocs) {
        $docFull = Join-Path $root ($docRel -replace '/', '\')
        if (-not (Test-Path -LiteralPath $docFull -PathType Leaf)) { continue }
        if ($docRel -eq 'Docs/archive/GameDesignGoogleDoc.md') { continue }

        if ($CheckTimestamps -and $docRel -notmatch '^Docs/history/' -and $docRel -notmatch '^Docs/backlog/') {
            $topLines = @(Get-Content -LiteralPath $docFull -Encoding UTF8 -TotalCount 12)
            $tsLine = $topLines | Where-Object { $_ -match '^Last Updated:' } | Select-Object -First 1
            if ($tsLine) {
                if ($tsLine -notmatch $tsFormatRegex) {
                    $staleTimestamps += "$docRel (invalid format: $tsLine)"
                } else {
                    $baseCheck = Invoke-StdoutOnly -FilePath 'git' -Arguments @('cat-file', '-e', "${BaseBranch}:$docRel")
                    if ($baseCheck.ExitCode -eq 0) {
                        $diffRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('diff', '-U0', $BaseBranch, '--', $docRel)
                        if ($diffRes.ExitCode -eq 0 -and $diffRes.StdOut) {
                            $tsChanged = ($diffRes.StdOut -split "`r?`n") | Where-Object { $_ -match '^\+Last Updated:' }
                            if (-not $tsChanged) {
                                $staleTimestamps += "$docRel (not updated vs $BaseBranch)"
                            }
                        }
                    }
                }
            }
        }

        # Check English-only Docs/ rule on added lines (skip AGENTS.md, Assignee names, and inline backtick UI literals)
        if ($docRel -match '^Docs/') {
            $isBacklogFile = ($docRel -match '^Docs/backlog/.*\.md$')
            $isLegacyNameTable = $docRel -in @('Docs/Backlog.md', 'Docs/archive/Backlog-Done.md', 'Docs/plan/Team.md')
            $linesToInspect = @()
            $baseCheck2 = Invoke-StdoutOnly -FilePath 'git' -Arguments @('cat-file', '-e', "${BaseBranch}:$docRel")
            if ($baseCheck2.ExitCode -eq 0) {
                $diffRes2 = Invoke-StdoutOnly -FilePath 'git' -Arguments @('diff', '-U0', $BaseBranch, '--', $docRel)
                if ($diffRes2.ExitCode -eq 0 -and $diffRes2.StdOut) {
                    $linesToInspect = @(($diffRes2.StdOut -split "`r?`n") | Where-Object { $_ -match '^\+' -and $_ -notmatch '^\+\+\+' } | ForEach-Object { $_.Substring(1) })
                }
            } else {
                $linesToInspect = @(Get-Content -LiteralPath $docFull -Encoding UTF8)
            }
            foreach ($dl in $linesToInspect) {
                $checkText = $dl
                if ($isBacklogFile -and $checkText -match '^\s*assignee\s*:') {
                    continue
                }
                if ($isLegacyNameTable -and $checkText -match '^\|') {
                    $cols = $checkText -split '\|'
                    if ($cols.Count -ge 6) {
                        $cols[4] = ''
                        $checkText = $cols -join '|'
                    }
                }
                # Strip inline backtick spans so verbatim Korean UI strings/identifiers stay allowed
                $checkText = [regex]::Replace($checkText, '`[^`]*`', '')
                if ($checkText -match $hangulRegex) {
                    $snippet = $dl.Trim()
                    if ($snippet.Length -gt 80) { $snippet = $snippet.Substring(0, 80) + '...' }
                    $koreanLines += "${docRel}: $snippet"
                }
            }
        }
    }

    if ($CheckTimestamps) {
        Write-Output "STALE_LAST_UPDATED=$($staleTimestamps.Count)"
        if ($staleTimestamps.Count -gt 0) {
            Write-BoundedLines -Lines $staleTimestamps -MaxLines 15
        }
    } else {
        Write-Output "STALE_LAST_UPDATED=0 (skipped; Git timestamps are source of truth)"
    }

    Write-Output "KOREAN_IN_DOCS=$($koreanLines.Count)"
    if ($koreanLines.Count -gt 0) {
        Write-BoundedLines -Lines $koreanLines -MaxLines 15
    }

    # Informational hint: newly added Runtime .cs scripts not yet listed in Docs/codemap/*.md
    $addedScripts = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($list in @(
        @(Get-GitLines -GitArgs @('diff', '--name-only', '--diff-filter=A', "$BaseBranch...HEAD")),
        @(Get-GitLines -GitArgs @('diff', '--cached', '--name-only', '--diff-filter=A')),
        @(Get-GitLines -GitArgs @('ls-files', '--others', '--exclude-standard'))
    )) {
        foreach ($item in $list) {
            $norm = ($item.Trim() -replace '\\', '/')
            if ($norm -match '^Assets/_Project/Scripts/Runtime/.*\.cs$') {
                [void]$addedScripts.Add($norm)
            }
        }
    }

    $unlistedScripts = @()
    if ($addedScripts.Count -gt 0) {
        $codemapDir = Join-Path $root 'Docs\codemap'
        $codemapText = ''
        if (Test-Path $codemapDir) {
            $codemapText = (Get-ChildItem $codemapDir -File -Filter '*.md' | ForEach-Object { Get-Content $_.FullName -Raw -Encoding UTF8 }) -join "`n"
        }
        foreach ($sc in ($addedScripts | Sort-Object)) {
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($sc)
            if ($codemapText -notmatch "\b$([regex]::Escape($baseName))\b") {
                $unlistedScripts += $sc
            }
        }
    }
    Write-Output "CODEMAP_UNLISTED_SCRIPTS=$($unlistedScripts.Count)"
    if ($unlistedScripts.Count -gt 0) {
        Write-BoundedLines -Lines $unlistedScripts -MaxLines 15 -TruncateHint "update relevant Docs/codemap/*.md leaf"
    }

    $diffCheckRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('diff', '--check')
    $diffCheckOk = ($diffCheckRes.ExitCode -eq 0)
    Write-Output "GIT_DIFF_CHECK_OK=$diffCheckOk"

    if ($broken.Count -gt 0 -or $invalidBacklog.Count -gt 0 -or -not $diffCheckOk) { exit 2 }
    if ($StrictDocs -and ($staleTimestamps.Count -gt 0 -or $koreanLines.Count -gt 0 -or $unarchivedDone.Count -gt 0)) { exit 2 }
} finally {
    Pop-Location
}
