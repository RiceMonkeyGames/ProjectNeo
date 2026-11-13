# Shared helpers for ProjectNeo agent workflow wrappers (PowerShell 5.1 compatible)

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
