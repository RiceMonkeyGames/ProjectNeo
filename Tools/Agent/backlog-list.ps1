param(
    [string]$Status = '',
    [string]$Area = '',
    [string]$Assignee = '',
    [switch]$IncludeHistory,
    [int]$MaxLines = 50
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    $allTasks = @(Get-ActiveBacklogTasks -Root $root)
    $statusFilters = @()
    if ($Status) {
        $statusFilters = @($Status -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
    $areaFilters = @()
    if ($Area) {
        $areaFilters = @($Area -split ',' | ForEach-Object { $_.Trim().ToUpperInvariant() } | Where-Object { $_ })
    }

    $matched = @($allTasks | Where-Object {
        $ok = $true
        if ($statusFilters.Count -gt 0) {
            $s = $_.Status
            $hit = $false
            foreach ($sf in $statusFilters) {
                if ($s -ieq $sf) { $hit = $true; break }
            }
            if (-not $hit) { $ok = $false }
        }
        if ($ok -and $areaFilters.Count -gt 0) {
            if ($areaFilters -notcontains $_.Area.ToUpperInvariant()) { $ok = $false }
        }
        if ($ok -and $Assignee) {
            if ($_.Assignee -notmatch [regex]::Escape($Assignee)) { $ok = $false }
        }
        $ok
    })

    $statusOrder = @{
        'In Progress'              = 1
        'Waiting for Verification' = 2
        'Waiting for Link'         = 3
        'Waiting'                  = 4
        'Pending Decision'         = 5
        'Waiting for Prerequisite' = 6
    }

    $sorted = @($matched | Sort-Object -Property @(
        @{ Expression = { if ($statusOrder.ContainsKey($_.Status)) { $statusOrder[$_.Status] } else { 99 } } },
        @{ Expression = { $_.Id } }
    ))

    Write-Output "ACTIVE_TOTAL=$($allTasks.Count)"
    Write-Output "ACTIVE_MATCHED=$($sorted.Count)"
    if ($sorted.Count -gt 0) {
        $lines = @()
        foreach ($t in $sorted) {
            if ($t.Branch) {
                $lines += "  - $($t.Id) | $($t.Status) | $($t.Assignee) | $($t.Branch) | $($t.Title)"
            } else {
                $lines += "  - $($t.Id) | $($t.Status) | $($t.Assignee) | $($t.Title)"
            }
        }
        Write-BoundedLines -Lines $lines -MaxLines $MaxLines -TruncateHint "filter with -Status, -Area, or -Assignee"
    }

    if ($IncludeHistory) {
        $histRoot = Join-Path $root 'Docs\history'
        $histFiles = @()
        if (Test-Path -LiteralPath $histRoot -PathType Container) {
            $histFiles = @(Get-ChildItem -LiteralPath $histRoot -Recurse -File -Filter '*.md' |
                Where-Object { $_.Name -ine 'README.md' } |
                ForEach-Object {
                    $dirName = Split-Path (Split-Path $_.FullName -Parent) -Leaf
                    [PSCustomObject]@{
                        Area = $dirName.ToUpperInvariant()
                        Id   = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
                        Rel  = ($_.FullName.Substring($root.TrimEnd('\').Length + 1) -replace '\\', '/')
                    }
                })
            if ($areaFilters.Count -gt 0) {
                $histFiles = @($histFiles | Where-Object { $areaFilters -contains $_.Area })
            }
        }
        Write-Output "HISTORY_MATCHED=$($histFiles.Count)"
        if ($histFiles.Count -gt 0) {
            $grouped = @($histFiles | Sort-Object Area, Id | Group-Object Area | ForEach-Object {
                $ids = ($_.Group | ForEach-Object { $_.Id }) -join ', '
                "  - $($_.Name)/ ($($_.Count)): $ids"
            })
            Write-BoundedLines -Lines $grouped -MaxLines $MaxLines -TruncateHint "filter history with -Area <PREFIX>"
        }
    }
} finally {
    Pop-Location
}
