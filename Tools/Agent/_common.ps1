# Shared helpers for ProjectNeo agent workflow wrappers (PowerShell 5.1 compatible)

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch {
}

function Get-ProjectNeoRoot {
    param([string]$FallbackDir)
    $gitRoot = (& git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -eq 0 -and $gitRoot) {
        return ($gitRoot.Trim() -replace '/', '\')
    }
    return $FallbackDir
}

function Get-KstTimestamp {
    return (Get-Date).ToString('yyyy-MM-dd HH:mm:ss') + ' (KST)'
}

function Write-BoundedLines {
    param(
        [string[]]$Lines,
        [int]$MaxLines = 30,
        [string]$TruncateHint = ''
    )
    if (-not $Lines -or $Lines.Count -eq 0) { return }
    $count = $Lines.Count
    $limit = [Math]::Min($count, $MaxLines)
    for ($i = 0; $i -lt $limit; $i++) {
        Write-Output $Lines[$i]
    }
    if ($count -gt $MaxLines) {
        $omitted = $count - $MaxLines
        if ($TruncateHint) {
            Write-Output "[... truncated $omitted lines; $TruncateHint]"
        } else {
            Write-Output "[... truncated $omitted lines]"
        }
    }
}

function Invoke-StdoutOnly {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.Arguments = ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    }) -join ' '
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    [void]$proc.Start()
    $stderrTask = $proc.StandardError.ReadToEndAsync()
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $proc.WaitForExit()

    return [PSCustomObject]@{
        ExitCode = $proc.ExitCode
        StdOut   = $stdout.Trim()
        StdErr   = $stderr.Trim()
    }
}

function Get-TaskPrefix {
    param([string]$TaskId)
    if ($TaskId -match '^([A-Za-z]+)-') {
        return $Matches[1].ToUpperInvariant()
    }
    if ($TaskId -match '^([A-Za-z]+)') {
        return $Matches[1].ToUpperInvariant()
    }
    return 'LEGACY'
}

function Read-TaskFrontmatterFromLines {
    param(
        [string[]]$Lines,
        [string]$FilePath = '',
        [string]$RelativePath = ''
    )
    $map = @{}
    $hasStart = $false
    $hasEnd = $false
    if ($Lines -and $Lines.Count -gt 0 -and $Lines[0].Trim() -eq '---') {
        $hasStart = $true
        for ($i = 1; $i -lt $Lines.Count; $i++) {
            $ln = $Lines[$i]
            if ($ln.Trim() -eq '---') {
                $hasEnd = $true
                break
            }
            if ($ln -match '^([A-Za-z0-9_]+)\s*:\s*(.*)$') {
                $k = $Matches[1].Trim().ToLowerInvariant()
                $v = $Matches[2].Trim()
                if (($v.Length -ge 2) -and (($v.StartsWith('"') -and $v.EndsWith('"')) -or ($v.StartsWith("'") -and $v.EndsWith("'")))) {
                    $v = $v.Substring(1, $v.Length - 2)
                }
                $map[$k] = $v
            }
        }
    }

    $expectedId = ''
    if ($FilePath) {
        $expectedId = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    }

    $id       = if ($map.ContainsKey('id') -and $map['id']) { $map['id'] } else { $expectedId }
    $title    = if ($map.ContainsKey('title')) { $map['title'] } else { '' }
    $status   = if ($map.ContainsKey('status')) { $map['status'] } else { '' }
    $assignee = if ($map.ContainsKey('assignee')) { $map['assignee'] } else { '' }
    $branch   = if ($map.ContainsKey('branch')) { $map['branch'] } else { '' }
    $area     = if ($map.ContainsKey('area') -and $map['area']) { $map['area'].ToUpperInvariant() } elseif ($id) { Get-TaskPrefix -TaskId $id } else { '' }
    $depends  = if ($map.ContainsKey('depends_on')) { $map['depends_on'] } else { '[]' }
    $created  = if ($map.ContainsKey('created')) { $map['created'] } else { '' }

    $valid = $hasStart -and $hasEnd -and ($map.ContainsKey('id') -and $map['id']) -and ($title -ne '') -and ($status -ne '') -and ($assignee -ne '')
    if ($valid -and $expectedId -and ($map['id'] -cne $expectedId)) {
        $valid = $false
    }

    return [PSCustomObject]@{
        Id           = $id
        Title        = $title
        Status       = $status
        Assignee     = $assignee
        Branch       = $branch
        Area         = $area
        DependsOn    = $depends
        Created      = $created
        FilePath     = $FilePath
        RelativePath = $RelativePath
        Valid        = $valid
    }
}

function Read-TaskFrontmatter {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$Root = ''
    )
    $topLines = @(Get-Content -LiteralPath $Path -Encoding UTF8 -TotalCount 16)
    $rel = $Path
    if ($Root) {
        $prefix = $Root.TrimEnd('\') + '\'
        if ($Path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $rel = $Path.Substring($prefix.Length) -replace '\\', '/'
        }
    }
    return Read-TaskFrontmatterFromLines -Lines $topLines -FilePath $Path -RelativePath $rel
}

function Get-ActiveBacklogTasks {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )
    $backlogDir = Join-Path $Root 'Docs\backlog'
    if (-not (Test-Path -LiteralPath $backlogDir -PathType Container)) {
        return @()
    }
    $files = @(Get-ChildItem -LiteralPath $backlogDir -File -Filter '*.md' |
        Where-Object { $_.Name -ine 'README.md' } |
        Sort-Object Name)
    $tasks = @()
    foreach ($f in $files) {
        $tasks += Read-TaskFrontmatter -Path $f.FullName -Root $Root
    }
    return @($tasks)
}

function Find-TaskHistoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$TaskId
    )
    $prefix = Get-TaskPrefix -TaskId $TaskId
    $directRel = "Docs/history/$prefix/$TaskId.md"
    $directFull = Join-Path $Root ($directRel -replace '/', '\')
    if (Test-Path -LiteralPath $directFull -PathType Leaf) {
        return $directRel
    }
    $histDir = Join-Path $Root 'Docs\history'
    if (Test-Path -LiteralPath $histDir -PathType Container) {
        $match = Get-ChildItem -LiteralPath $histDir -Recurse -File -Filter "$TaskId.md" | Select-Object -First 1
        if ($match) {
            $rootPrefix = $Root.TrimEnd('\') + '\'
            return ($match.FullName.Substring($rootPrefix.Length) -replace '\\', '/')
        }
    }
    return $null
}
