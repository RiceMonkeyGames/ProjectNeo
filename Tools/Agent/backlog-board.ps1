param(
    [string]$OutPath = '',
    [switch]$NoOpen,
    [switch]$NoHistory,
    [switch]$NoFetch,
    [switch]$Serve,
    [switch]$Static,
    [switch]$ServeWorker,
    [switch]$Foreground,
    [int]$Port = 41735,
    [int]$IdleMinutes = 60
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

function Parse-DependsOn {
    param([string]$Raw)
    $result = New-Object System.Collections.Generic.List[string]
    if (-not $Raw) { return $result.ToArray() }
    $trimmed = $Raw.Trim()
    if ($trimmed.StartsWith('[') -and $trimmed.EndsWith(']')) {
        $trimmed = $trimmed.Substring(1, $trimmed.Length - 2).Trim()
    }
    if (-not $trimmed) { return $result.ToArray() }
    foreach ($part in ($trimmed -split ',')) {
        $item = $part.Trim().Trim('"', "'")
        if ($item) { [void]$result.Add($item) }
    }
    return $result.ToArray()
}

function Extract-BodyFromLines {
    param([string[]]$AllLines)
    if (-not $AllLines -or $AllLines.Count -eq 0) { return '' }
    $startIdx = 0
    if ($AllLines[0].Trim() -eq '---') {
        for ($i = 1; $i -lt $AllLines.Count; $i++) {
            if ($AllLines[$i].Trim() -eq '---') {
                $startIdx = $i + 1
                break
            }
        }
    }
    if ($startIdx -ge $AllLines.Count) { return '' }
    return (($AllLines[$startIdx..($AllLines.Count - 1)]) -join "`n").Trim()
}

function Get-LegacyArchiveIdsFromText {
    param([string]$Text)
    $set = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    if (-not $Text) { return $set }
    foreach ($ln in ($Text -split "`r?`n")) {
        if ($ln -match '^\|\s*`?([A-Za-z0-9-]+)`?\s*\|') {
            [void]$set.Add($Matches[1])
        }
    }
    return $set
}

function Resolve-DevelopSource {
    param(
        [string]$Root,
        [switch]$SkipFetch
    )
    $fetchStatus = 'skipped'
    if (-not $SkipFetch) {
        $fetchRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('-C', $Root, 'fetch', '--quiet', 'origin', 'develop')
        if ($fetchRes.ExitCode -eq 0) {
            $fetchStatus = 'ok'
        } else {
            $fetchStatus = 'offline'
        }
    }

    $candidates = @('origin/develop', 'develop', 'HEAD')
    foreach ($ref in $candidates) {
        $rev = Invoke-StdoutOnly -FilePath 'git' -Arguments @('-C', $Root, 'rev-parse', '--verify', '--short', $ref)
        if ($rev.ExitCode -eq 0 -and $rev.StdOut) {
            return [PSCustomObject]@{
                Ref         = $ref
                ShortSha    = $rev.StdOut.Trim()
                FetchStatus = $fetchStatus
            }
        }
    }

    return [PSCustomObject]@{
        Ref         = 'HEAD'
        ShortSha    = 'unknown'
        FetchStatus = $fetchStatus
    }
}

function Read-GitBlobBatch {
    param(
        [string]$Root,
        [string]$Ref,
        [string[]]$Paths
    )
    $map = @{}
    if (-not $Paths -or $Paths.Count -eq 0) { return $map }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $chunkSize = 32
    for ($offset = 0; $offset -lt $Paths.Count; $offset += $chunkSize) {
        $end = [Math]::Min($offset + $chunkSize - 1, $Paths.Count - 1)
        $chunk = @($Paths[$offset..$end])

        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'git'
        $psi.Arguments = 'cat-file --batch'
        $psi.WorkingDirectory = $Root
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true

        $proc = New-Object System.Diagnostics.Process
        $proc.StartInfo = $psi
        [void]$proc.Start()

        $sb = New-Object System.Text.StringBuilder
        foreach ($p in $chunk) {
            [void]$sb.Append($Ref).Append(':').Append($p).Append("`n")
        }
        $inBytes = $utf8.GetBytes($sb.ToString())
        $proc.StandardInput.BaseStream.Write($inBytes, 0, $inBytes.Length)
        $proc.StandardInput.BaseStream.Flush()
        $proc.StandardInput.Close()

        $outStream = $proc.StandardOutput.BaseStream
        foreach ($p in $chunk) {
            $hdrBytes = New-Object System.Collections.Generic.List[byte]
            while ($true) {
                $b = $outStream.ReadByte()
                if ($b -lt 0 -or $b -eq 10) { break }
                if ($b -ne 13) { [void]$hdrBytes.Add([byte]$b) }
            }
            if ($hdrBytes.Count -eq 0) { continue }
            $hdr = $utf8.GetString($hdrBytes.ToArray())
            if ($hdr.EndsWith(' missing')) { continue }
            $parts = $hdr -split ' '
            if ($parts.Count -lt 3) { continue }
            $size = 0
            if (-not [int]::TryParse($parts[$parts.Count - 1], [ref]$size)) { continue }

            $buf = New-Object byte[] $size
            $readTotal = 0
            while ($readTotal -lt $size) {
                $n = $outStream.Read($buf, $readTotal, $size - $readTotal)
                if ($n -le 0) { break }
                $readTotal += $n
            }
            # Consume trailing LF written by git cat-file --batch
            [void]$outStream.ReadByte()

            $text = $utf8.GetString($buf, 0, $readTotal)
            if ($text.Length -gt 0 -and [int]$text[0] -eq 0xFEFF) {
                $text = $text.Substring(1)
            }
            $map[$p] = $text
        }

        $proc.WaitForExit()
        $proc.Dispose()
    }
    return $map
}

function Build-BoardPayload {
    param(
        [string]$Root,
        [switch]$SkipHistory,
        [switch]$SkipFetch,
        [bool]$LiveMode = $false
    )
    $kstNow = Get-KstTimestamp
    $today = (Get-Date).Date

    $localBranchRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('-C', $Root, 'branch', '--show-current')
    $localBranch = if ($localBranchRes.ExitCode -eq 0 -and $localBranchRes.StdOut) { $localBranchRes.StdOut.Trim() } else { 'DETACHED' }

    $src = Resolve-DevelopSource -Root $Root -SkipFetch:$SkipFetch
    $targetRef = $src.Ref

    $lsArgs = @('-C', $Root, '-c', 'core.quotepath=false', 'ls-tree', '-r', '--name-only', $targetRef, '--', 'Docs/backlog', 'Docs/history', 'Docs/archive/Backlog-Done.md')
    $lsRes = Invoke-StdoutOnly -FilePath 'git' -Arguments $lsArgs
    $allGitPaths = @()
    if ($lsRes.ExitCode -eq 0 -and $lsRes.StdOut) {
        $allGitPaths = @($lsRes.StdOut -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }

    $backlogPaths = @($allGitPaths | Where-Object {
        $_ -match '^Docs/backlog/[^/]+\.md$' -and $_ -inotmatch '/README\.md$'
    } | Sort-Object)

    $historyPaths = @($allGitPaths | Where-Object {
        $_ -match '^Docs/history/.+\.md$' -and $_ -inotmatch '/README\.md$'
    } | Sort-Object)

    $hasArchive = ($allGitPaths -contains 'Docs/archive/Backlog-Done.md')

    $pathsToRead = New-Object System.Collections.Generic.List[string]
    foreach ($p in $backlogPaths) { [void]$pathsToRead.Add($p) }
    if (-not $SkipHistory) {
        foreach ($p in $historyPaths) { [void]$pathsToRead.Add($p) }
    }
    if ($hasArchive) { [void]$pathsToRead.Add('Docs/archive/Backlog-Done.md') }

    $blobs = Read-GitBlobBatch -Root $Root -Ref $targetRef -Paths ($pathsToRead.ToArray())

    $activeRaw = New-Object System.Collections.Generic.List[object]
    $activeIdSet = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($rel in $backlogPaths) {
        $content = if ($blobs.ContainsKey($rel)) { $blobs[$rel] } else { '' }
        $lines = @($content -split "`r?`n")
        $topLimit = [Math]::Min($lines.Count, 16)
        $topLines = if ($topLimit -gt 0) { @($lines[0..($topLimit - 1)]) } else { @() }
        $fullFsPath = Join-Path $Root ($rel -replace '/', '\')
        $fm = Read-TaskFrontmatterFromLines -Lines $topLines -FilePath $fullFsPath -RelativePath $rel
        if ($fm.Id) { [void]$activeIdSet.Add($fm.Id) }
        [void]$activeRaw.Add([PSCustomObject]@{
            Meta     = $fm
            Body     = Extract-BodyFromLines -AllLines $lines
            FullPath = ($fullFsPath -replace '\\', '/')
        })
    }

    $historyIdSet = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    $historyItems = New-Object System.Collections.Generic.List[object]
    foreach ($rel in $historyPaths) {
        $fileName = $rel.Substring($rel.LastIndexOf('/') + 1)
        $hid = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
        [void]$historyIdSet.Add($hid)
        if (-not $SkipHistory) {
            $segs = $rel -split '/'
            $area = if ($segs.Count -ge 4) { $segs[$segs.Count - 2].ToUpperInvariant() } else { 'LEGACY' }
            $content = if ($blobs.ContainsKey($rel)) { $blobs[$rel] } else { '' }
            $firstLine = ''
            if ($content) {
                $nlIdx = $content.IndexOf("`n")
                $firstLine = if ($nlIdx -ge 0) { $content.Substring(0, $nlIdx).Trim() } else { $content.Trim() }
            }
            $hDate = ''
            $hTitle = $firstLine -replace '^#+\s*', ''
            if ($hTitle -match '^[^|]+\|\s*(\d{4}-\d{2}-\d{2})\s*\|\s*(.+)$') {
                $hDate = $Matches[1]
                $hTitle = $Matches[2].Trim()
            }
            $fullFsPath = (Join-Path $Root ($rel -replace '/', '\')) -replace '\\', '/'
            [void]$historyItems.Add([PSCustomObject]@{
                id           = $hid
                area         = $area
                date         = $hDate
                title        = $hTitle
                relativePath = $rel
                fullPath     = $fullFsPath
            })
        }
    }

    $archiveText = if ($blobs.ContainsKey('Docs/archive/Backlog-Done.md')) { $blobs['Docs/archive/Backlog-Done.md'] } else { '' }
    $legacyDoneSet = Get-LegacyArchiveIdsFromText -Text $archiveText

    $taskItems = New-Object System.Collections.Generic.List[object]
    foreach ($entry in $activeRaw) {
        $t = $entry.Meta
        $depsRaw = @(Parse-DependsOn -Raw $t.DependsOn)
        $depsResolved = New-Object System.Collections.Generic.List[object]
        $hasActiveDep = $false
        foreach ($depId in $depsRaw) {
            $depState = 'unknown'
            if ($activeIdSet.Contains($depId)) {
                $depState = 'active'
                $hasActiveDep = $true
            } elseif ($historyIdSet.Contains($depId) -or $legacyDoneSet.Contains($depId)) {
                $depState = 'done'
            }
            [void]$depsResolved.Add([PSCustomObject]@{
                id    = $depId
                state = $depState
            })
        }

        $ageDays = $null
        if ($t.Created -match '^\d{4}-\d{2}-\d{2}$') {
            try {
                $createdDate = [DateTime]::ParseExact($t.Created, 'yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture)
                $ageDays = [int]($today - $createdDate.Date).TotalDays
            } catch {
                $ageDays = $null
            }
        }

        [void]$taskItems.Add([PSCustomObject]@{
            id           = $t.Id
            title        = $t.Title
            status       = $t.Status
            assignee     = $t.Assignee
            branch       = $t.Branch
            area         = $t.Area
            created      = $t.Created
            ageDays      = $ageDays
            dependsOn    = @($depsResolved.ToArray())
            blockedByDep = $hasActiveDep
            valid        = $t.Valid
            relativePath = $t.RelativePath
            fullPath     = $entry.FullPath
            body         = $entry.Body
        })
    }

    return [PSCustomObject]@{
        generatedAt   = $kstNow
        sourceRef     = $src.Ref
        sourceSha     = $src.ShortSha
        fetchStatus   = $src.FetchStatus
        localBranch   = $localBranch
        currentBranch = $src.Ref
        liveMode      = $LiveMode
        repoRoot      = ($Root -replace '\\', '/')
        activeCount   = $taskItems.Count
        historyCount  = $historyItems.Count
        tasks         = @($taskItems.ToArray())
        history       = @($historyItems.ToArray())
    }
}

function Get-BoardHtmlTemplate {
    return @'
<!DOCTYPE html>
<html lang="ko">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>ProjectNeo &mdash; Backlog</title>
<style>
  :root {
    --bg: #0b0d12;
    --sidebar: #10131b;
    --surface: #141822;
    --surface-2: #191f2c;
    --surface-hover: #1e2536;
    --border: rgba(255, 255, 255, 0.08);
    --border-strong: rgba(255, 255, 255, 0.14);
    --text: #eef2f8;
    --text-2: #c4cedd;
    --muted: #7f8ca3;
    --accent: #6e8efb;
    --accent-soft: rgba(110, 142, 251, 0.14);
    --green: #3dd68c;
    --green-soft: rgba(61, 214, 140, 0.13);
    --yellow: #f0b429;
    --yellow-soft: rgba(240, 180, 41, 0.14);
    --red: #f96b6b;
    --red-soft: rgba(249, 107, 107, 0.14);
    --purple: #b78bfa;
    --purple-soft: rgba(183, 139, 250, 0.14);
    --cyan: #3ec7d6;
    --cyan-soft: rgba(62, 199, 214, 0.13);
    --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.25);
    --shadow-md: 0 10px 28px rgba(0, 0, 0, 0.42);
    --shadow-lg: 0 24px 60px rgba(0, 0, 0, 0.62);
    --radius: 10px;
    --radius-sm: 7px;
    --sidebar-w: 256px;
    --font-sans: "Inter", "Pretendard", -apple-system, BlinkMacSystemFont, "Segoe UI", "Malgun Gothic", sans-serif;
    --font-mono: "JetBrains Mono", "Cascadia Code", ui-monospace, Consolas, monospace;
  }
  [data-theme="light"] {
    --bg: #f6f7f9;
    --sidebar: #fbfcfd;
    --surface: #ffffff;
    --surface-2: #f4f6f9;
    --surface-hover: #edf1f7;
    --border: rgba(15, 23, 42, 0.08);
    --border-strong: rgba(15, 23, 42, 0.16);
    --text: #151922;
    --text-2: #364152;
    --muted: #667285;
    --accent: #4c6ef5;
    --accent-soft: rgba(76, 110, 245, 0.10);
    --green: #179156;
    --green-soft: rgba(23, 145, 86, 0.10);
    --yellow: #b47808;
    --yellow-soft: rgba(180, 120, 8, 0.11);
    --red: #d9383a;
    --red-soft: rgba(217, 56, 58, 0.10);
    --purple: #7c4dff;
    --purple-soft: rgba(124, 77, 255, 0.10);
    --cyan: #0b8799;
    --cyan-soft: rgba(11, 135, 153, 0.10);
    --shadow-sm: 0 1px 2px rgba(15, 23, 42, 0.05);
    --shadow-md: 0 8px 24px rgba(15, 23, 42, 0.08);
    --shadow-lg: 0 24px 60px rgba(15, 23, 42, 0.18);
  }

  * { box-sizing: border-box; }
  html, body { height: 100%; }
  body {
    margin: 0;
    font-family: var(--font-sans);
    background: var(--bg);
    color: var(--text);
    font-size: 13.5px;
    line-height: 1.45;
    -webkit-font-smoothing: antialiased;
  }

  ::-webkit-scrollbar { width: 7px; height: 7px; }
  ::-webkit-scrollbar-track { background: transparent; }
  ::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }

  @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
  .spinning { animation: spin 0.75s linear infinite; }

  .app { display: flex; min-height: 100vh; }

  /* Left Sidebar */
  .sidebar {
    width: var(--sidebar-w);
    flex: 0 0 var(--sidebar-w);
    background: var(--sidebar);
    border-right: 1px solid var(--border);
    display: flex;
    flex-direction: column;
    position: sticky;
    top: 0;
    height: 100vh;
    overflow: hidden;
    z-index: 30;
    transition: transform 0.2s ease;
  }
  .sb-head {
    padding: 15px 14px 12px;
    border-bottom: 1px solid var(--border);
  }
  .brand { display: flex; align-items: center; gap: 9px; }
  .brand-logo {
    width: 26px; height: 26px; border-radius: 7px;
    background: linear-gradient(135deg, var(--accent), var(--purple));
    display: grid; place-items: center; color: #fff;
    font-weight: 800; font-size: 13px; letter-spacing: -0.02em; flex-shrink: 0;
  }
  .brand-name { font-size: 14px; font-weight: 700; letter-spacing: -0.015em; }
  .brand-sub {
    font-size: 11px; color: var(--muted); font-family: var(--font-mono);
    white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
  }
  .sb-branch {
    margin-top: 9px; display: flex; align-items: center; gap: 6px;
    padding: 4px 8px; border-radius: 6px;
    background: var(--surface); border: 1px solid var(--border);
    font-family: var(--font-mono); font-size: 11px; color: var(--text-2); overflow: hidden;
  }
  .sb-branch span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .sb-local-note {
    margin-top: 5px; font-family: var(--font-mono); font-size: 10.5px; color: var(--muted);
    padding: 0 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }

  .sb-scroll { flex: 1; overflow-y: auto; padding: 10px 10px 14px; }
  .sb-section { margin-bottom: 16px; }
  .sb-section:last-child { margin-bottom: 0; }
  .sb-label {
    font-size: 10.5px; font-weight: 700; text-transform: uppercase;
    letter-spacing: 0.06em; color: var(--muted); padding: 4px 8px 6px;
    display: flex; align-items: center; justify-content: space-between;
  }
  .sb-clear {
    font-size: 10.5px; color: var(--accent); cursor: pointer;
    text-transform: none; letter-spacing: 0; font-weight: 600;
  }
  .sb-clear:hover { text-decoration: underline; }

  .nav-item {
    display: flex; align-items: center; justify-content: space-between; gap: 8px;
    width: 100%; padding: 6px 8px; margin-bottom: 2px;
    border: 1px solid transparent; border-radius: var(--radius-sm);
    background: transparent; color: var(--text-2);
    font: inherit; font-size: 12.5px; font-weight: 500; text-align: left;
    cursor: pointer; transition: background 0.12s, color 0.12s, border-color 0.12s;
  }
  .nav-item:hover { background: var(--surface-hover); color: var(--text); }
  .nav-item.active {
    background: var(--accent-soft); border-color: rgba(110, 142, 251, 0.25);
    color: var(--accent); font-weight: 600;
  }
  .nav-left { display: flex; align-items: center; gap: 8px; min-width: 0; }
  .nav-text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .nav-count {
    font-family: var(--font-mono); font-size: 11px; color: var(--muted);
    background: var(--surface-2); padding: 1px 6px; border-radius: 999px; flex-shrink: 0;
  }
  .nav-item.active .nav-count { background: rgba(110, 142, 251, 0.2); color: var(--accent); }
  .nav-subcount { display: inline-flex; align-items: center; gap: 4px; flex-shrink: 0; }
  .inprog-pill {
    font-family: var(--font-mono); font-size: 10.5px; font-weight: 600;
    color: var(--accent); background: var(--accent-soft); padding: 1px 5px; border-radius: 999px;
  }

  .dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; display: inline-block; }
  .avatar {
    width: 20px; height: 20px; border-radius: 50%; display: inline-grid; place-items: center;
    font-size: 10px; font-weight: 700; flex-shrink: 0; color: #fff;
  }
  .avatar.sm { width: 18px; height: 18px; font-size: 9.5px; }
  .avatar.unassigned {
    background: var(--surface-2) !important; color: var(--muted);
    border: 1px dashed var(--border-strong);
  }

  .sb-foot {
    padding: 10px 12px; border-top: 1px solid var(--border);
    display: flex; align-items: center; justify-content: space-between; gap: 6px;
  }
  .sb-foot-left { display: flex; align-items: center; gap: 6px; min-width: 0; }
  .sb-updated {
    font-size: 10.5px; color: var(--muted); font-family: var(--font-mono);
    overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }
  .sb-foot-actions { display: flex; align-items: center; gap: 5px; flex-shrink: 0; }

  /* Main Area */
  .main { flex: 1; min-width: 0; display: flex; flex-direction: column; }
  .topbar {
    position: sticky; top: 0; z-index: 20;
    background: color-mix(in srgb, var(--bg) 88%, transparent);
    backdrop-filter: blur(10px);
    border-bottom: 1px solid var(--border);
    padding: 11px 22px; display: flex; flex-wrap: wrap; align-items: center; gap: 9px;
  }
  .topbar-title {
    font-size: 15px; font-weight: 700; letter-spacing: -0.015em;
    display: flex; align-items: center; gap: 8px;
  }
  .topbar-count { font-family: var(--font-mono); font-size: 11.5px; color: var(--muted); font-weight: 500; }
  .search-wrap { position: relative; flex: 1 1 220px; max-width: 340px; margin-left: auto; }
  .search-wrap input {
    width: 100%; padding: 6px 34px 6px 30px; border-radius: 8px;
    border: 1px solid var(--border); background: var(--surface); color: var(--text);
    font: inherit; font-size: 12.5px; outline: none;
    transition: border-color 0.14s, box-shadow 0.14s;
  }
  .search-wrap input:focus { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-soft); }
  .search-ico {
    position: absolute; left: 10px; top: 50%; transform: translateY(-50%);
    color: var(--muted); pointer-events: none;
  }
  .kbd-hint {
    position: absolute; right: 8px; top: 50%; transform: translateY(-50%);
    font-family: var(--font-mono); font-size: 10px; color: var(--muted);
    border: 1px solid var(--border-strong); border-radius: 4px; padding: 0 5px; pointer-events: none;
  }

  .btn {
    font: inherit; font-size: 12.5px; font-weight: 500; color: var(--text-2);
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius-sm);
    padding: 6px 10px; cursor: pointer; display: inline-flex; align-items: center; gap: 6px;
    transition: background 0.12s, border-color 0.12s, color 0.12s;
  }
  .btn:hover { background: var(--surface-hover); border-color: var(--border-strong); color: var(--text); }
  .btn:disabled { opacity: 0.6; cursor: wait; }
  .btn.active {
    background: var(--accent-soft); border-color: rgba(110, 142, 251, 0.35);
    color: var(--accent); font-weight: 600;
  }
  .btn.primary { background: var(--accent); border-color: var(--accent); color: #fff; font-weight: 600; }
  .btn.primary:hover { filter: brightness(1.08); }
  .btn.danger-subtle { color: var(--red); }
  .btn.danger-subtle:hover { background: var(--red-soft); border-color: rgba(249, 107, 107, 0.35); color: var(--red); }
  .btn.icon-only { padding: 6px 8px; }
  .btn.menu-btn { display: none; padding: 6px 9px; }

  .seg {
    display: inline-flex; background: var(--surface); border: 1px solid var(--border);
    border-radius: 8px; padding: 2px; gap: 2px;
  }
  .seg .btn { border: none; background: transparent; padding: 4px 10px; border-radius: 6px; }
  .seg .btn.active { background: var(--surface-hover); color: var(--text); box-shadow: var(--shadow-sm); }

  .content { padding: 16px 22px 36px; flex: 1; }

  .active-filters { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-bottom: 12px; }
  .filter-chip {
    display: inline-flex; align-items: center; gap: 6px; padding: 3px 9px; border-radius: 999px;
    background: var(--accent-soft); border: 1px solid rgba(110, 142, 251, 0.3);
    color: var(--accent); font-size: 11.5px; font-weight: 600;
  }
  .filter-chip button {
    border: none; background: transparent; color: inherit; cursor: pointer;
    padding: 0; font-size: 13px; line-height: 1;
  }

  .summary-panel {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 12px 14px; margin-bottom: 16px; box-shadow: var(--shadow-sm);
  }
  .kpi-row { display: grid; grid-template-columns: repeat(5, minmax(110px, 1fr)); gap: 8px; margin-bottom: 10px; }
  .kpi-card {
    padding: 8px 10px; border-radius: 8px; border: 1px solid transparent;
    background: var(--surface-2); cursor: pointer; transition: border-color 0.12s, background 0.12s;
  }
  .kpi-card:hover { background: var(--surface-hover); border-color: var(--border-strong); }
  .kpi-card.selected { border-color: var(--accent); background: var(--accent-soft); }
  .kpi-top { display: flex; align-items: center; gap: 6px; font-size: 11.5px; color: var(--muted); font-weight: 500; }
  .kpi-val { font-size: 21px; font-weight: 700; letter-spacing: -0.02em; margin-top: 2px; }
  .progress-bar {
    height: 5px; border-radius: 999px; background: var(--surface-2);
    overflow: hidden; display: flex; gap: 2px;
  }
  .progress-seg { height: 100%; border-radius: 999px; transition: width 0.25s ease; }

  .board {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(245px, 1fr));
    gap: 12px; align-items: start;
  }
  .col {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    display: flex; flex-direction: column; max-height: calc(100vh - 210px); min-height: 200px;
    box-shadow: var(--shadow-sm);
  }
  .col-head {
    padding: 11px 13px; border-bottom: 1px solid var(--border);
    display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-shrink: 0;
  }
  .col-title { display: flex; align-items: center; gap: 8px; font-size: 12.5px; font-weight: 700; color: var(--text); }
  .col-count {
    font-family: var(--font-mono); font-size: 11px; color: var(--muted);
    background: var(--surface-2); border: 1px solid var(--border); padding: 1px 7px; border-radius: 999px;
  }
  .cards { padding: 10px; display: flex; flex-direction: column; gap: 8px; overflow-y: auto; }
  .card {
    position: relative; background: var(--surface-2); border: 1px solid var(--border);
    border-left: 3px solid var(--card-accent, var(--accent)); border-radius: 8px;
    padding: 10px 11px 10px 12px; cursor: pointer;
    transition: background 0.12s, border-color 0.12s, transform 0.12s, box-shadow 0.12s;
  }
  .card:hover {
    background: var(--surface-hover); border-color: var(--border-strong);
    border-left-color: var(--card-accent, var(--accent));
    transform: translateY(-1px); box-shadow: var(--shadow-md);
  }
  .card-top { display: flex; align-items: center; justify-content: space-between; gap: 6px; margin-bottom: 5px; }
  .task-id { font-family: var(--font-mono); font-size: 11.5px; font-weight: 700; color: var(--muted); letter-spacing: -0.01em; }
  .card:hover .task-id { color: var(--accent); }
  .badges { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; }
  .badge { font-size: 10.5px; font-weight: 600; padding: 1px 6px; border-radius: 5px; white-space: nowrap; }
  .badge.area { background: var(--accent-soft); color: var(--accent); }
  .badge.area-BUG { background: var(--red-soft); color: var(--red); }
  .badge.area-Q { background: var(--yellow-soft); color: var(--yellow); }
  .badge.area-UI { background: var(--purple-soft); color: var(--purple); }
  .badge.area-TOOL, .badge.area-QA, .badge.area-DOC, .badge.area-PERF { background: var(--cyan-soft); color: var(--cyan); }
  .badge.area-COMBAT, .badge.area-SIEGE, .badge.area-FLOW { background: var(--green-soft); color: var(--green); }
  .badge.blocked { background: var(--red-soft); color: var(--red); border: 1px solid rgba(249, 107, 107, 0.35); }
  .badge.invalid { background: var(--red-soft); color: var(--red); }

  .card-title { font-size: 13px; font-weight: 600; color: var(--text); line-height: 1.4; margin-bottom: 8px; word-break: break-word; }
  .card-deps { display: flex; flex-wrap: wrap; gap: 4px; margin-bottom: 8px; }
  .mini-dep { font-family: var(--font-mono); font-size: 10px; padding: 1px 5px; border-radius: 4px; }
  .mini-dep.active { background: var(--red-soft); color: var(--red); }
  .mini-dep.done { background: var(--green-soft); color: var(--green); }
  .mini-dep.unknown { background: var(--yellow-soft); color: var(--yellow); }

  .card-foot { display: flex; align-items: center; justify-content: space-between; gap: 8px; font-size: 11.5px; color: var(--muted); }
  .assignee-pill { display: inline-flex; align-items: center; gap: 6px; min-width: 0; color: var(--text-2); font-weight: 500; }
  .assignee-pill span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .age-pill { font-family: var(--font-mono); font-size: 10.5px; color: var(--muted); flex-shrink: 0; }
  .branch-tag {
    margin-top: 7px; padding-top: 6px; border-top: 1px solid var(--border);
    font-family: var(--font-mono); font-size: 10.5px; color: var(--cyan);
    overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }

  .table-wrap {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    overflow-x: auto; box-shadow: var(--shadow-sm);
  }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  th, td { padding: 10px 13px; text-align: left; border-bottom: 1px solid var(--border); vertical-align: middle; }
  th {
    background: var(--surface-2); font-size: 11px; font-weight: 700; text-transform: uppercase;
    letter-spacing: 0.04em; color: var(--muted); cursor: pointer; user-select: none; white-space: nowrap;
  }
  th:hover { color: var(--text); }
  tbody tr { cursor: pointer; transition: background 0.1s; }
  tbody tr:last-child td { border-bottom: none; }
  tbody tr:hover { background: var(--surface-hover); }
  .mono { font-family: var(--font-mono); font-size: 12px; }
  .status-pill {
    display: inline-flex; align-items: center; gap: 6px; padding: 2px 8px; border-radius: 999px;
    font-size: 11.5px; font-weight: 600; white-space: nowrap;
  }
  .st-inprog { background: var(--accent-soft); color: var(--accent); }
  .st-wait { background: var(--green-soft); color: var(--green); }
  .st-verif { background: var(--cyan-soft); color: var(--cyan); }
  .st-decision { background: var(--yellow-soft); color: var(--yellow); }
  .st-prereq { background: var(--purple-soft); color: var(--purple); }
  .st-other { background: var(--surface-2); color: var(--muted); }
  .empty { text-align: center; color: var(--muted); padding: 30px 12px; font-size: 12.5px; }

  /* Modal */
  .modal-backdrop {
    position: fixed; inset: 0; background: rgba(5, 7, 11, 0.68); backdrop-filter: blur(4px);
    display: none; align-items: center; justify-content: center; padding: 18px; z-index: 100;
  }
  .modal-backdrop.open { display: flex; }
  .modal {
    background: var(--surface); border: 1px solid var(--border-strong); border-radius: 13px;
    max-width: 760px; width: 100%; max-height: 86vh; display: flex; flex-direction: column;
    box-shadow: var(--shadow-lg); overflow: hidden;
  }
  .modal-head {
    padding: 16px 20px 14px; border-bottom: 1px solid var(--border);
    display: flex; align-items: flex-start; justify-content: space-between; gap: 12px;
  }
  .modal-badges { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-bottom: 6px; }
  .modal-head h2 { margin: 0; font-size: 17px; font-weight: 700; letter-spacing: -0.015em; line-height: 1.35; }
  .modal-body { padding: 16px 20px; overflow-y: auto; }
  .prop-list {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: 8px 16px; padding: 12px 14px; background: var(--surface-2);
    border: 1px solid var(--border); border-radius: 9px; margin-bottom: 14px;
  }
  .prop-row { display: flex; align-items: center; gap: 10px; min-width: 0; font-size: 12.5px; }
  .prop-k { width: 74px; flex-shrink: 0; color: var(--muted); font-size: 11.5px; font-weight: 500; }
  .prop-v { color: var(--text); font-weight: 600; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .dep-badge {
    display: inline-flex; align-items: center; gap: 4px; font-family: var(--font-mono); font-size: 11.5px;
    padding: 2px 8px; border-radius: 6px; margin: 2px 5px 2px 0; border: 1px solid transparent;
  }
  .dep-badge.clickable { cursor: pointer; }
  .dep-badge.clickable:hover { border-color: currentColor; }
  .dep-done { background: var(--green-soft); color: var(--green); }
  .dep-active { background: var(--red-soft); color: var(--red); }
  .dep-unknown { background: var(--yellow-soft); color: var(--yellow); }

  .md-body {
    background: var(--surface-2); border: 1px solid var(--border); border-radius: 9px;
    padding: 14px 16px; font-size: 13.5px; line-height: 1.65; color: var(--text-2);
  }
  .md-body h1, .md-body h2, .md-body h3 { color: var(--text); margin: 12px 0 6px; font-size: 14px; font-weight: 700; }
  .md-body h1:first-child, .md-body h2:first-child { margin-top: 0; }
  .md-body ul { margin: 6px 0; padding-left: 20px; }
  .md-body code {
    font-family: var(--font-mono); font-size: 12px; color: var(--text);
    background: var(--surface); border: 1px solid var(--border); padding: 1px 5px; border-radius: 4px;
  }
  .modal-foot {
    padding: 12px 20px; border-top: 1px solid var(--border); background: var(--sidebar);
    display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px;
  }
  .modal-actions { display: flex; flex-wrap: wrap; gap: 7px; }

  .toast {
    position: fixed; bottom: 20px; right: 20px; background: var(--surface);
    border: 1px solid var(--accent); color: var(--text); padding: 9px 14px;
    border-radius: 9px; box-shadow: var(--shadow-md); font-size: 12.5px; font-weight: 600;
    opacity: 0; transform: translateY(6px); pointer-events: none;
    transition: opacity 0.18s, transform 0.18s; z-index: 200;
  }
  .toast.show { opacity: 1; transform: translateY(0); }

  .sb-backdrop { display: none; position: fixed; inset: 0; background: rgba(0, 0, 0, 0.5); z-index: 25; }
  .hidden { display: none !important; }

  @media (max-width: 980px) {
    .sidebar { position: fixed; left: 0; top: 0; transform: translateX(-100%); box-shadow: var(--shadow-lg); }
    .sidebar.open { transform: translateX(0); }
    .sb-backdrop.open { display: block; }
    .btn.menu-btn { display: inline-flex; }
    .kpi-row { grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); }
    .col { max-height: none; }
  }
</style>
</head>
<body>
<div class="app">
  <div class="sb-backdrop" id="sbBackdrop"></div>
  <aside class="sidebar" id="sidebar">
    <div class="sb-head">
      <div class="brand">
        <div class="brand-logo">PN</div>
        <div style="min-width:0;">
          <div class="brand-name">ProjectNeo</div>
          <div class="brand-sub">Backlog Workspace</div>
        </div>
      </div>
      <div class="sb-branch" id="metaSourceBox" title="Data source Git ref">
        <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6" style="color:var(--cyan);flex-shrink:0;"><circle cx="4" cy="3.5" r="1.8"/><circle cx="4" cy="12.5" r="1.8"/><circle cx="12" cy="5.5" r="1.8"/><path d="M4 5.3v5.4M12 7.3c0 2.7-2.2 3.4-8 3.4"/></svg>
        <span id="metaBranch">origin/develop</span>
      </div>
      <div class="sb-local-note hidden" id="metaLocalNote"></div>
    </div>

    <div class="sb-scroll">
      <div class="sb-section">
        <div class="sb-label">Views</div>
        <button class="nav-item active" data-sb-view="kanban">
          <span class="nav-left"><svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="flex-shrink:0;"><rect x="2" y="2.5" width="3.5" height="11" rx="1"/><rect x="6.25" y="2.5" width="3.5" height="7.5" rx="1"/><rect x="10.5" y="2.5" width="3.5" height="9.5" rx="1"/></svg><span class="nav-text">Kanban Board</span></span>
          <span class="nav-count" id="sbCountActive">0</span>
        </button>
        <button class="nav-item" data-sb-view="table">
          <span class="nav-left"><svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="flex-shrink:0;"><path d="M2.5 4h11M2.5 8h11M2.5 12h11"/></svg><span class="nav-text">Table View</span></span>
          <span class="nav-count" id="sbCountTable">0</span>
        </button>
        <button class="nav-item" data-sb-view="history" id="sbViewHistory">
          <span class="nav-left"><svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6" style="flex-shrink:0;"><path d="M3 8.5l3.2 3.2L13 4.8"/></svg><span class="nav-text">Completed History</span></span>
          <span class="nav-count" id="sbCountHistory">0</span>
        </button>
      </div>

      <div class="sb-section">
        <div class="sb-label">
          <span>Status</span>
          <span class="sb-clear hidden" id="clearStatus">Clear</span>
        </div>
        <div id="sbStatusList"></div>
      </div>

      <div class="sb-section">
        <div class="sb-label">
          <span>Assignees</span>
          <span class="sb-clear hidden" id="clearAssignee">Clear</span>
        </div>
        <div id="sbAssigneeList"></div>
      </div>

      <div class="sb-section">
        <div class="sb-label">
          <span>Areas</span>
          <span class="sb-clear hidden" id="clearArea">Clear</span>
        </div>
        <div id="sbAreaList"></div>
      </div>
    </div>

    <div class="sb-foot">
      <div class="sb-foot-left">
        <span class="dot" id="liveDot" style="background:var(--muted);" title="Server status"></span>
        <span class="sb-updated" id="metaGen" title="Snapshot timestamp">updated</span>
      </div>
      <div class="sb-foot-actions">
        <button class="btn icon-only danger-subtle hidden" id="btnStopServer" title="Stop local board server"><svg width="13" height="13" viewBox="0 0 16 16" fill="currentColor"><rect x="3.5" y="3.5" width="9" height="9" rx="1.5"/></svg></button>
        <button class="btn icon-only" id="btnTheme" title="Toggle Dark / Light theme"><svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="8" cy="8" r="5.5"/><path d="M8 2.5a5.5 5.5 0 0 1 0 11z" fill="currentColor"/></svg></button>
      </div>
    </div>
  </aside>

  <div class="main">
    <header class="topbar">
      <button class="btn menu-btn" id="btnMenu" title="Toggle menu"><svg width="15" height="15" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M2.5 4h11M2.5 8h11M2.5 12h11"/></svg></button>
      <div class="topbar-title">
        <span id="viewHeading">Kanban Board</span>
        <span class="topbar-count" id="countNote"></span>
      </div>

      <div class="search-wrap">
        <span class="search-ico"><svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6" style="display:block;"><circle cx="7" cy="7" r="4.5"/><path d="M10.5 10.5L14 14"/></svg></span>
        <input type="search" id="qSearch" placeholder="Search ID, title, assignee, branch, scope...">
        <span class="kbd-hint">/</span>
      </div>

      <button class="btn" id="btnRefresh" title="Fetch origin/develop and refresh (shortcut: r)">
        <svg id="refreshIco" width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M13.5 8a5.5 5.5 0 1 1-1.4-3.65M13.5 2.5v2.7h-2.7"/></svg>
        <span>Refresh</span>
      </button>

      <button class="btn" id="btnReadyToggle">Ready to Claim</button>

      <div class="seg">
        <button class="btn active" id="viewKanban">Board</button>
        <button class="btn" id="viewTable">Table</button>
        <button class="btn" id="viewHistory">History</button>
      </div>
    </header>

    <div class="content">
      <div class="active-filters hidden" id="activeFilters"></div>

      <section class="summary-panel" id="summaryPanel">
        <div class="kpi-row" id="kpiGrid"></div>
        <div class="progress-bar" id="progressBar"></div>
      </section>

      <main id="kanbanView" class="board"></main>
      <main id="tableView" class="table-wrap hidden"></main>
      <main id="historyView" class="table-wrap hidden"></main>
    </div>
  </div>
</div>

<div class="modal-backdrop" id="modalBackdrop">
  <div class="modal" role="dialog" aria-modal="true">
    <div class="modal-head">
      <div style="min-width:0;">
        <div class="modal-badges" id="mBadges"></div>
        <h2 id="mTitle"></h2>
      </div>
      <button class="btn" id="mClose">Esc</button>
    </div>
    <div class="modal-body">
      <div class="prop-list" id="mMeta"></div>
      <div id="mDepsWrap" style="margin-bottom:14px;"></div>
      <div class="md-body" id="mBody"></div>
    </div>
    <div class="modal-foot">
      <div class="modal-actions">
        <button class="btn primary" id="mOpenVscode">Open in VS Code</button>
        <button class="btn" id="mCopyClaim">Copy /claim</button>
        <button class="btn" id="mCopyCheck">Copy claim-check</button>
        <button class="btn" id="mCopyPath">Copy Path</button>
      </div>
      <span class="mono" style="color:var(--muted)" id="mRelPath"></span>
    </div>
  </div>
</div>

<div class="toast" id="toast"></div>

<script>
let DATA = __BACKLOG_JSON__;

(function() {
  function toArray(v) {
    return Array.isArray(v) ? v : (v ? [v] : []);
  }

  let tasks = [];
  let history = [];
  let taskById = new Map();
  let isLive = false;
  let isRefreshing = false;

  const STATUS_ORDER = {
    'In Progress': 1,
    'Waiting for Verification': 2,
    'Waiting for Link': 3,
    'Waiting': 4,
    'Pending Decision': 5,
    'Waiting for Prerequisite': 6
  };

  const AVATAR_COLORS = ['#5c7cfa', '#20c997', '#f08c00', '#ae3ec9', '#1098ad', '#e64980', '#37b24d', '#7048e8'];

  const state = {
    view: 'kanban',
    q: '',
    area: '',
    assignee: '',
    status: '',
    readyOnly: false,
    sortKey: 'status',
    sortDir: 1
  };

  function hydrateData(nextData) {
    DATA = nextData || {};
    tasks = toArray(DATA.tasks);
    history = toArray(DATA.history);
    taskById = new Map(tasks.map(t => [String(t.id).toUpperCase(), t]));
    isLive = Boolean(DATA.liveMode) && window.location.protocol.startsWith('http');

    const ref = DATA.sourceRef || DATA.currentBranch || 'develop';
    const sha = DATA.sourceSha ? (' @ ' + DATA.sourceSha) : '';
    const offlineTag = DATA.fetchStatus === 'offline' ? ' (offline)' : '';
    document.getElementById('metaBranch').textContent = ref + sha + offlineTag;

    const localNote = document.getElementById('metaLocalNote');
    if (DATA.localBranch && DATA.localBranch !== 'develop') {
      localNote.textContent = 'working tree: ' + DATA.localBranch;
      localNote.classList.remove('hidden');
    } else {
      localNote.classList.add('hidden');
    }

    document.getElementById('metaGen').textContent = (DATA.generatedAt || '-').replace(' (KST)', '');

    const liveDot = document.getElementById('liveDot');
    const stopBtn = document.getElementById('btnStopServer');
    if (isLive) {
      liveDot.style.background = DATA.fetchStatus === 'offline' ? 'var(--yellow)' : 'var(--green)';
      liveDot.title = DATA.fetchStatus === 'offline' ? 'Live server (offline fallback)' : 'Live server connected';
      stopBtn.classList.remove('hidden');
    } else {
      liveDot.style.background = 'var(--muted)';
      liveDot.title = 'Static snapshot';
      stopBtn.classList.add('hidden');
    }

    if (history.length === 0) {
      document.getElementById('viewHistory').classList.add('hidden');
      document.getElementById('sbViewHistory').classList.add('hidden');
      if (state.view === 'history') state.view = 'kanban';
    } else {
      document.getElementById('viewHistory').classList.remove('hidden');
      document.getElementById('sbViewHistory').classList.remove('hidden');
    }
  }

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function hashColor(name) {
    let h = 0;
    const s = String(name || '');
    for (let i = 0; i < s.length; i++) h = ((h << 5) - h + s.charCodeAt(i)) | 0;
    return AVATAR_COLORS[Math.abs(h) % AVATAR_COLORS.length];
  }

  function avatarHtml(name, small) {
    const n = String(name || 'Unassigned').trim();
    const cls = small ? 'avatar sm' : 'avatar';
    if (!n || n.toLowerCase() === 'unassigned') {
      return '<span class="' + cls + ' unassigned">&mdash;</span>';
    }
    const ch = Array.from(n)[0].toUpperCase();
    return '<span class="' + cls + '" style="background:' + hashColor(n) + '">' + esc(ch) + '</span>';
  }

  function areaColor(area) {
    if (area === 'BUG') return 'var(--red)';
    if (area === 'Q') return 'var(--yellow)';
    if (area === 'UI') return 'var(--purple)';
    if (area === 'TOOL' || area === 'QA' || area === 'DOC' || area === 'PERF') return 'var(--cyan)';
    if (area === 'COMBAT' || area === 'SIEGE' || area === 'FLOW') return 'var(--green)';
    return 'var(--accent)';
  }

  function statusColor(st, blocked) {
    if (blocked) return 'var(--red)';
    if (st === 'In Progress') return 'var(--accent)';
    if (st === 'Waiting') return 'var(--green)';
    if (st === 'Waiting for Verification' || st === 'Waiting for Link') return 'var(--cyan)';
    if (st === 'Pending Decision') return 'var(--yellow)';
    if (st === 'Waiting for Prerequisite') return 'var(--purple)';
    return 'var(--muted)';
  }

  function statusClass(st) {
    if (st === 'In Progress') return 'st-inprog';
    if (st === 'Waiting') return 'st-wait';
    if (st === 'Waiting for Verification' || st === 'Waiting for Link') return 'st-verif';
    if (st === 'Pending Decision') return 'st-decision';
    if (st === 'Waiting for Prerequisite') return 'st-prereq';
    return 'st-other';
  }

  function isBlocked(t) {
    return Boolean(t.blockedByDep) || t.status === 'Waiting for Prerequisite';
  }

  function isReady(t) {
    return t.status === 'Waiting' && !t.blockedByDep;
  }

  function ageLabel(t) {
    if (typeof t.ageDays !== 'number' || Number.isNaN(t.ageDays)) return t.created || '-';
    if (t.ageDays <= 0) return 'today';
    return t.ageDays + 'd';
  }

  function toast(msg) {
    const el = document.getElementById('toast');
    el.textContent = msg;
    el.classList.add('show');
    clearTimeout(toast._t);
    toast._t = setTimeout(() => el.classList.remove('show'), 2200);
  }

  function copyText(text, label) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(() => toast('Copied: ' + label)).catch(() => fallbackCopy(text, label));
    } else {
      fallbackCopy(text, label);
    }
  }

  function fallbackCopy(text, label) {
    const ta = document.createElement('textarea');
    ta.value = text;
    document.body.appendChild(ta);
    ta.select();
    try { document.execCommand('copy'); toast('Copied: ' + label); } catch (e) {}
    document.body.removeChild(ta);
  }

  function renderMarkdownLite(md) {
    if (!md) return '<div class="empty">No description body.</div>';
    const lines = md.replace(/\r\n/g, '\n').split('\n');
    let html = '';
    let inList = false;
    function inlineFmt(str) {
      let s = esc(str);
      s = s.replace(/`([^`]+)`/g, '<code>$1</code>');
      s = s.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
      s = s.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<span class="mono">$1 ($2)</span>');
      return s;
    }
    for (const rawLine of lines) {
      const line = rawLine.trim();
      if (!line) {
        if (inList) { html += '</ul>'; inList = false; }
        continue;
      }
      if (/^#{1,3}\s+/.test(line)) {
        if (inList) { html += '</ul>'; inList = false; }
        const lvl = line.match(/^(#+)/)[1].length;
        html += '<h' + lvl + '>' + inlineFmt(line.replace(/^#+\s+/, '')) + '</h' + lvl + '>';
      } else if (/^[-*]\s+/.test(line)) {
        if (!inList) { html += '<ul>'; inList = true; }
        html += '<li>' + inlineFmt(line.replace(/^[-*]\s+/, '')) + '</li>';
      } else {
        if (inList) { html += '</ul>'; inList = false; }
        html += '<p style="margin:6px 0;">' + inlineFmt(line) + '</p>';
      }
    }
    if (inList) html += '</ul>';
    return html;
  }

  function closeMobileSidebar() {
    document.getElementById('sidebar').classList.remove('open');
    document.getElementById('sbBackdrop').classList.remove('open');
  }

  function resetFilters() {
    state.q = '';
    state.area = '';
    state.assignee = '';
    state.status = '';
    state.readyOnly = false;
    document.getElementById('qSearch').value = '';
    render();
  }

  async function refreshBoard() {
    if (isRefreshing) return;
    if (!isLive) {
      toast('Static HTML mode: reloading file...');
      setTimeout(() => window.location.reload(), 200);
      return;
    }
    isRefreshing = true;
    const btn = document.getElementById('btnRefresh');
    const ico = document.getElementById('refreshIco');
    btn.disabled = true;
    ico.classList.add('spinning');
    try {
      const res = await fetch('/api/refresh?t=' + Date.now(), { cache: 'no-store' });
      if (!res.ok) throw new Error('HTTP ' + res.status);
      const next = await res.json();
      hydrateData(next);
      render();
      if (currentModalTask) {
        const updated = taskById.get(String(currentModalTask.id).toUpperCase());
        if (updated) openModal(updated);
        else closeModal();
      }
      const tag = next.fetchStatus === 'offline' ? 'Offline fallback: ' : 'Refreshed: ';
      toast(tag + (next.sourceRef || 'develop') + (next.sourceSha ? ' @ ' + next.sourceSha : ''));
    } catch (err) {
      document.getElementById('liveDot').style.background = 'var(--red)';
      toast('Server unreachable. Re-run backlog-board.bat');
    } finally {
      isRefreshing = false;
      btn.disabled = false;
      ico.classList.remove('spinning');
    }
  }

  async function stopServer() {
    if (!isLive) return;
    try {
      await fetch('/api/shutdown', { method: 'POST' });
    } catch (e) {}
    isLive = false;
    document.getElementById('liveDot').style.background = 'var(--red)';
    document.getElementById('liveDot').title = 'Server stopped';
    document.getElementById('btnStopServer').classList.add('hidden');
    toast('Local board server stopped');
  }

  function renderSidebar() {
    document.getElementById('sbCountActive').textContent = tasks.length;
    document.getElementById('sbCountTable').textContent = tasks.length;
    document.getElementById('sbCountHistory').textContent = history.length;

    document.querySelectorAll('[data-sb-view]').forEach(btn => {
      btn.classList.toggle('active', btn.getAttribute('data-sb-view') === state.view);
    });

    const statusItems = [
      { key: 'In Progress', label: 'In Progress', color: 'var(--accent)', count: tasks.filter(t => t.status === 'In Progress').length },
      { key: '__READY__', label: 'Ready to Claim', color: 'var(--green)', count: tasks.filter(isReady).length },
      { key: 'Pending Decision', label: 'Pending Decision', color: 'var(--yellow)', count: tasks.filter(t => t.status === 'Pending Decision').length },
      { key: 'Waiting for Prerequisite', label: 'Prerequisite', color: 'var(--purple)', count: tasks.filter(t => t.status === 'Waiting for Prerequisite').length }
    ];
    const verifCnt = tasks.filter(t => t.status === 'Waiting for Verification' || t.status === 'Waiting for Link').length;
    if (verifCnt > 0) {
      statusItems.splice(1, 0, { key: 'Waiting for Verification', label: 'Verification', color: 'var(--cyan)', count: verifCnt });
    }
    const sbStatus = document.getElementById('sbStatusList');
    sbStatus.innerHTML = '';
    statusItems.forEach(item => {
      const active = item.key === '__READY__' ? state.readyOnly : (!state.readyOnly && state.status === item.key);
      const b = document.createElement('button');
      b.className = 'nav-item' + (active ? ' active' : '');
      b.innerHTML =
        '<span class="nav-left"><span class="dot" style="background:' + item.color + '"></span><span class="nav-text">' + esc(item.label) + '</span></span>' +
        '<span class="nav-count">' + item.count + '</span>';
      b.addEventListener('click', () => {
        if (state.view === 'history') state.view = 'kanban';
        if (item.key === '__READY__') {
          state.readyOnly = !state.readyOnly;
          if (state.readyOnly) state.status = '';
        } else {
          state.readyOnly = false;
          state.status = state.status === item.key ? '' : item.key;
        }
        closeMobileSidebar();
        render();
      });
      sbStatus.appendChild(b);
    });
    document.getElementById('clearStatus').classList.toggle('hidden', !state.status && !state.readyOnly);

    const assigneeMap = new Map();
    tasks.forEach(t => {
      const name = (t.assignee || 'Unassigned').trim() || 'Unassigned';
      if (!assigneeMap.has(name)) assigneeMap.set(name, { name, total: 0, inProg: 0 });
      const rec = assigneeMap.get(name);
      rec.total += 1;
      if (t.status === 'In Progress') rec.inProg += 1;
    });
    const assignees = Array.from(assigneeMap.values()).sort((a, b) => {
      const aUn = a.name.toLowerCase() === 'unassigned' ? 1 : 0;
      const bUn = b.name.toLowerCase() === 'unassigned' ? 1 : 0;
      if (aUn !== bUn) return aUn - bUn;
      return b.inProg - a.inProg || b.total - a.total || a.name.localeCompare(b.name);
    });

    const sbAssignees = document.getElementById('sbAssigneeList');
    sbAssignees.innerHTML = '';
    assignees.forEach(a => {
      const active = state.assignee === a.name;
      const b = document.createElement('button');
      b.className = 'nav-item' + (active ? ' active' : '');
      const rightHtml = a.inProg > 0
        ? '<span class="nav-subcount"><span class="inprog-pill" title="In Progress">' + a.inProg + ' active</span><span class="nav-count">' + a.total + '</span></span>'
        : '<span class="nav-count">' + a.total + '</span>';
      b.innerHTML =
        '<span class="nav-left">' + avatarHtml(a.name, true) + '<span class="nav-text">' + esc(a.name) + '</span></span>' +
        rightHtml;
      b.addEventListener('click', () => {
        if (state.view === 'history') state.view = 'kanban';
        state.assignee = state.assignee === a.name ? '' : a.name;
        closeMobileSidebar();
        render();
      });
      sbAssignees.appendChild(b);
    });
    document.getElementById('clearAssignee').classList.toggle('hidden', !state.assignee);

    const srcList = state.view === 'history' ? history : tasks;
    const areaMap = new Map();
    srcList.forEach(item => {
      const k = item.area || 'OTHER';
      areaMap.set(k, (areaMap.get(k) || 0) + 1);
    });
    const areas = Array.from(areaMap.entries()).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));

    const sbAreas = document.getElementById('sbAreaList');
    sbAreas.innerHTML = '';
    areas.forEach(([area, cnt]) => {
      const active = state.area === area;
      const b = document.createElement('button');
      b.className = 'nav-item' + (active ? ' active' : '');
      b.innerHTML =
        '<span class="nav-left"><span class="dot" style="background:' + areaColor(area) + '"></span><span class="nav-text">' + esc(area) + '</span></span>' +
        '<span class="nav-count">' + cnt + '</span>';
      b.addEventListener('click', () => {
        state.area = state.area === area ? '' : area;
        closeMobileSidebar();
        render();
      });
      sbAreas.appendChild(b);
    });
    document.getElementById('clearArea').classList.toggle('hidden', !state.area);
  }

  function renderActiveFilterChips() {
    const wrap = document.getElementById('activeFilters');
    const chips = [];
    if (state.status) chips.push({ label: 'Status: ' + state.status, clear: () => { state.status = ''; } });
    if (state.readyOnly) chips.push({ label: 'Ready to Claim', clear: () => { state.readyOnly = false; } });
    if (state.assignee) chips.push({ label: 'Assignee: ' + state.assignee, clear: () => { state.assignee = ''; } });
    if (state.area) chips.push({ label: 'Area: ' + state.area, clear: () => { state.area = ''; } });
    if (state.q.trim()) chips.push({ label: 'Query: "' + state.q.trim() + '"', clear: () => { state.q = ''; document.getElementById('qSearch').value = ''; } });

    if (chips.length === 0) {
      wrap.classList.add('hidden');
      wrap.innerHTML = '';
      return;
    }
    wrap.classList.remove('hidden');
    wrap.innerHTML = '';
    chips.forEach(c => {
      const el = document.createElement('span');
      el.className = 'filter-chip';
      el.innerHTML = '<span>' + esc(c.label) + '</span><button title="Remove">&times;</button>';
      el.querySelector('button').addEventListener('click', () => { c.clear(); render(); });
      wrap.appendChild(el);
    });
    const resetBtn = document.createElement('button');
    resetBtn.className = 'btn';
    resetBtn.style.padding = '3px 9px';
    resetBtn.style.fontSize = '11.5px';
    resetBtn.textContent = 'Reset all';
    resetBtn.addEventListener('click', resetFilters);
    wrap.appendChild(resetBtn);
  }

  function renderSummary() {
    const panel = document.getElementById('summaryPanel');
    if (state.view === 'history') {
      panel.classList.add('hidden');
      return;
    }
    panel.classList.remove('hidden');

    const total = tasks.length;
    const inProg = tasks.filter(t => t.status === 'In Progress').length;
    const ready = tasks.filter(isReady).length;
    const decision = tasks.filter(t => t.status === 'Pending Decision').length;
    const blocked = tasks.filter(isBlocked).length;

    const kpis = [
      { label: 'Active Total', val: total, color: 'var(--text)', selected: !state.status && !state.readyOnly, action: () => { state.status = ''; state.readyOnly = false; render(); } },
      { label: 'In Progress', val: inProg, color: 'var(--accent)', selected: state.status === 'In Progress' && !state.readyOnly, action: () => { state.readyOnly = false; state.status = state.status === 'In Progress' ? '' : 'In Progress'; render(); } },
      { label: 'Ready to Claim', val: ready, color: 'var(--green)', selected: state.readyOnly, action: () => { state.readyOnly = !state.readyOnly; if (state.readyOnly) state.status = ''; render(); } },
      { label: 'Pending Decision', val: decision, color: 'var(--yellow)', selected: state.status === 'Pending Decision' && !state.readyOnly, action: () => { state.readyOnly = false; state.status = state.status === 'Pending Decision' ? '' : 'Pending Decision'; render(); } },
      { label: 'Blocked / Prereq', val: blocked, color: 'var(--red)', selected: state.status === 'Waiting for Prerequisite' && !state.readyOnly, action: () => { state.readyOnly = false; state.status = state.status === 'Waiting for Prerequisite' ? '' : 'Waiting for Prerequisite'; render(); } }
    ];

    const grid = document.getElementById('kpiGrid');
    grid.innerHTML = '';
    kpis.forEach(k => {
      const d = document.createElement('div');
      d.className = 'kpi-card' + (k.selected ? ' selected' : '');
      d.innerHTML =
        '<div class="kpi-top"><span class="dot" style="background:' + k.color + '"></span>' + esc(k.label) + '</div>' +
        '<div class="kpi-val" style="color:' + k.color + '">' + k.val + '</div>';
      d.addEventListener('click', k.action);
      grid.appendChild(d);
    });

    const bar = document.getElementById('progressBar');
    if (total === 0) {
      bar.innerHTML = '';
      return;
    }
    const segs = [
      { n: inProg, color: 'var(--accent)', title: 'In Progress: ' + inProg },
      { n: ready, color: 'var(--green)', title: 'Ready to Claim: ' + ready },
      { n: decision, color: 'var(--yellow)', title: 'Pending Decision: ' + decision },
      { n: blocked, color: 'var(--purple)', title: 'Blocked / Prereq: ' + blocked }
    ];
    bar.innerHTML = segs.filter(s => s.n > 0).map(s =>
      '<div class="progress-seg" title="' + esc(s.title) + '" style="width:' + ((s.n / total) * 100).toFixed(2) + '%;background:' + s.color + '"></div>'
    ).join('');
  }

  function filteredTasks() {
    const q = state.q.trim().toLowerCase();
    return tasks.filter(t => {
      if (state.area && t.area !== state.area) return false;
      if (state.assignee && ((t.assignee || 'Unassigned').trim() || 'Unassigned') !== state.assignee) return false;
      if (state.status && t.status !== state.status) return false;
      if (state.readyOnly && !isReady(t)) return false;
      if (q) {
        const hay = [t.id, t.title, t.assignee, t.area, t.branch, t.status, t.body].join(' ').toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    }).sort((a, b) => {
      let cmp = 0;
      if (state.sortKey === 'status') {
        cmp = (STATUS_ORDER[a.status] || 99) - (STATUS_ORDER[b.status] || 99) || a.id.localeCompare(b.id);
      } else if (state.sortKey === 'age') {
        cmp = (b.ageDays || -1) - (a.ageDays || -1);
      } else {
        cmp = String(a[state.sortKey] || '').localeCompare(String(b[state.sortKey] || ''));
      }
      return cmp * state.sortDir;
    });
  }

  function renderKanban(list) {
    const cols = [
      { key: 'In Progress', label: 'In Progress', dot: 'var(--accent)', match: t => t.status === 'In Progress' },
      { key: 'Waiting', label: 'Waiting', dot: 'var(--green)', match: t => t.status === 'Waiting' },
      { key: 'Pending Decision', label: 'Pending Decision', dot: 'var(--yellow)', match: t => t.status === 'Pending Decision' },
      { key: 'Waiting for Prerequisite', label: 'Prerequisite', dot: 'var(--purple)', match: t => t.status === 'Waiting for Prerequisite' }
    ];
    const verifItems = list.filter(t => t.status === 'Waiting for Verification' || t.status === 'Waiting for Link');
    if (verifItems.length > 0) {
      cols.splice(1, 0, { key: 'Verif', label: 'Verification / Link', dot: 'var(--cyan)', match: t => t.status === 'Waiting for Verification' || t.status === 'Waiting for Link' });
    }
    const known = new Set(['In Progress', 'Waiting', 'Pending Decision', 'Waiting for Prerequisite', 'Waiting for Verification', 'Waiting for Link']);
    const otherItems = list.filter(t => !known.has(t.status));
    if (otherItems.length > 0) {
      cols.push({ key: 'Other', label: 'Other', dot: 'var(--muted)', match: t => !known.has(t.status) });
    }

    const wrap = document.getElementById('kanbanView');
    wrap.innerHTML = '';
    cols.forEach(c => {
      const items = list.filter(c.match);
      const col = document.createElement('section');
      col.className = 'col';
      col.innerHTML =
        '<div class="col-head">' +
          '<div class="col-title"><span class="dot" style="background:' + c.dot + '"></span>' + esc(c.label) + '</div>' +
          '<span class="col-count">' + items.length + '</span>' +
        '</div>';
      const cards = document.createElement('div');
      cards.className = 'cards';
      if (items.length === 0) {
        cards.innerHTML = '<div class="empty">No tasks</div>';
      } else {
        items.forEach(t => {
          const card = document.createElement('article');
          card.className = 'card';
          card.style.setProperty('--card-accent', statusColor(t.status, t.blockedByDep));

          const blockedBadge = t.blockedByDep ? '<span class="badge blocked">BLOCKED</span>' : '';
          const invalidBadge = t.valid === false ? '<span class="badge invalid">INVALID</span>' : '';
          const deps = toArray(t.dependsOn);
          const depsHtml = deps.length > 0
            ? '<div class="card-deps">' + deps.map(d => '<span class="mini-dep ' + esc(d.state) + '">&#8627; ' + esc(d.id) + '</span>').join('') + '</div>'
            : '';
          const branchHtml = t.branch ? '<div class="branch-tag" title="' + esc(t.branch) + '">branch: ' + esc(t.branch) + '</div>' : '';

          card.innerHTML =
            '<div class="card-top">' +
              '<span class="task-id">' + esc(t.id) + '</span>' +
              '<div class="badges">' +
                '<span class="badge area area-' + esc(t.area) + '">' + esc(t.area) + '</span>' +
                blockedBadge + invalidBadge +
              '</div>' +
            '</div>' +
            '<div class="card-title">' + esc(t.title) + '</div>' +
            depsHtml +
            '<div class="card-foot">' +
              '<span class="assignee-pill">' + avatarHtml(t.assignee, true) + '<span>' + esc(t.assignee || 'Unassigned') + '</span></span>' +
              '<span class="age-pill">' + esc(ageLabel(t)) + '</span>' +
            '</div>' +
            branchHtml;
          card.addEventListener('click', () => openModal(t));
          cards.appendChild(card);
        });
      }
      col.appendChild(cards);
      wrap.appendChild(col);
    });
  }

  function renderTable(list) {
    const wrap = document.getElementById('tableView');
    if (list.length === 0) {
      wrap.innerHTML = '<div class="empty">No matching active tasks.</div>';
      return;
    }
    const cols = [
      { k: 'id', label: 'ID' },
      { k: 'status', label: 'Status' },
      { k: 'area', label: 'Area' },
      { k: 'assignee', label: 'Assignee' },
      { k: 'title', label: 'Title' },
      { k: 'branch', label: 'Branch' },
      { k: 'age', label: 'Created' }
    ];
    let html = '<table><thead><tr>';
    cols.forEach(c => {
      const arrow = state.sortKey === c.k ? (state.sortDir === 1 ? ' &#9650;' : ' &#9660;') : '';
      html += '<th data-sort="' + c.k + '">' + esc(c.label) + arrow + '</th>';
    });
    html += '</tr></thead><tbody>';
    list.forEach((t, idx) => {
      const deps = toArray(t.dependsOn);
      const blocked = t.blockedByDep ? ' <span class="badge blocked">BLOCKED</span>' : '';
      const depStr = deps.length
        ? '<div style="margin-top:4px;display:flex;flex-wrap:wrap;gap:4px;">' + deps.map(d => '<span class="mini-dep ' + esc(d.state) + '">&#8627; ' + esc(d.id) + '</span>').join('') + '</div>'
        : '';
      html += '<tr data-idx="' + idx + '">' +
        '<td class="mono" style="color:var(--accent);font-weight:700;white-space:nowrap;">' + esc(t.id) + '</td>' +
        '<td><span class="status-pill ' + statusClass(t.status) + '"><span class="dot" style="background:' + statusColor(t.status, false) + '"></span>' + esc(t.status) + '</span>' + blocked + '</td>' +
        '<td><span class="badge area area-' + esc(t.area) + '">' + esc(t.area) + '</span></td>' +
        '<td style="white-space:nowrap;"><span class="assignee-pill">' + avatarHtml(t.assignee, true) + '<span>' + esc(t.assignee || 'Unassigned') + '</span></span></td>' +
        '<td><div style="font-weight:600;">' + esc(t.title) + '</div>' + depStr + '</td>' +
        '<td class="mono" style="color:var(--cyan);">' + esc(t.branch || '\u2014') + '</td>' +
        '<td class="mono" style="white-space:nowrap;color:var(--muted);">' + esc(t.created || '-') + ' (' + esc(ageLabel(t)) + ')</td>' +
      '</tr>';
    });
    html += '</tbody></table>';
    wrap.innerHTML = html;

    wrap.querySelectorAll('th[data-sort]').forEach(th => {
      th.addEventListener('click', () => {
        const k = th.getAttribute('data-sort');
        if (state.sortKey === k) state.sortDir *= -1;
        else { state.sortKey = k; state.sortDir = 1; }
        render();
      });
    });
    wrap.querySelectorAll('tbody tr').forEach(tr => {
      tr.addEventListener('click', () => openModal(list[Number(tr.getAttribute('data-idx'))]));
    });
  }

  function renderHistory() {
    const q = state.q.trim().toLowerCase();
    const list = history.filter(h => {
      if (state.area && h.area !== state.area) return false;
      if (q) {
        const hay = [h.id, h.area, h.date, h.title, h.relativePath].join(' ').toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    }).sort((a, b) => (b.date || '').localeCompare(a.date || '') || a.id.localeCompare(b.id));

    document.getElementById('countNote').textContent = list.length + ' / ' + history.length;
    const wrap = document.getElementById('historyView');
    if (list.length === 0) {
      wrap.innerHTML = '<div class="empty">No matching history records.</div>';
      return;
    }
    let html = '<table><thead><tr><th>ID</th><th>Area</th><th>Date</th><th>Title</th><th>Path</th></tr></thead><tbody>';
    list.forEach((h, idx) => {
      html += '<tr data-hidx="' + idx + '">' +
        '<td class="mono" style="color:var(--green);font-weight:700;white-space:nowrap;">' + esc(h.id) + '</td>' +
        '<td><span class="badge area area-' + esc(h.area) + '">' + esc(h.area) + '</span></td>' +
        '<td class="mono" style="white-space:nowrap;color:var(--muted);">' + esc(h.date || '-') + '</td>' +
        '<td style="font-weight:500;">' + esc(h.title || '-') + '</td>' +
        '<td class="mono" style="color:var(--muted);">' + esc(h.relativePath) + '</td>' +
      '</tr>';
    });
    html += '</tbody></table>';
    wrap.innerHTML = html;
    wrap.querySelectorAll('tbody tr').forEach(tr => {
      tr.addEventListener('click', () => {
        const h = list[Number(tr.getAttribute('data-hidx'))];
        window.location.href = 'vscode://file/' + encodeURI(h.fullPath);
      });
    });
  }

  let currentModalTask = null;
  function openModal(t) {
    currentModalTask = t;
    const badgesHtml =
      '<span class="task-id" style="color:var(--accent);font-size:12.5px;">' + esc(t.id) + '</span>' +
      '<span class="status-pill ' + statusClass(t.status) + '"><span class="dot" style="background:' + statusColor(t.status, false) + '"></span>' + esc(t.status) + '</span>' +
      '<span class="badge area area-' + esc(t.area) + '">' + esc(t.area) + '</span>' +
      (t.blockedByDep ? '<span class="badge blocked">BLOCKED</span>' : '');
    document.getElementById('mBadges').innerHTML = badgesHtml;
    document.getElementById('mTitle').textContent = t.title;
    document.getElementById('mRelPath').textContent = t.relativePath || '';

    const props = [
      { k: 'Assignee', html: '<span class="assignee-pill">' + avatarHtml(t.assignee, true) + '<span>' + esc(t.assignee || 'Unassigned') + '</span></span>' },
      { k: 'Branch', html: '<span class="mono" style="color:var(--cyan);">' + esc(t.branch || '(not claimed)') + '</span>' },
      { k: 'Created', html: '<span class="mono">' + esc(t.created || '-') + ' (' + esc(ageLabel(t)) + ')</span>' },
      { k: 'Area', html: esc(t.area || '-') }
    ];
    document.getElementById('mMeta').innerHTML = props.map(p =>
      '<div class="prop-row"><span class="prop-k">' + esc(p.k) + '</span><span class="prop-v">' + p.html + '</span></div>'
    ).join('');

    const deps = toArray(t.dependsOn);
    const depsWrap = document.getElementById('mDepsWrap');
    if (deps.length > 0) {
      depsWrap.innerHTML = '<div style="font-size:11.5px;color:var(--muted);font-weight:600;margin-bottom:6px;">Dependencies</div>';
      deps.forEach(d => {
        const cls = d.state === 'done' ? 'dep-done' : (d.state === 'active' ? 'dep-active' : 'dep-unknown');
        const target = taskById.get(String(d.id).toUpperCase());
        const sp = document.createElement('span');
        sp.className = 'dep-badge ' + cls + (target ? ' clickable' : '');
        sp.textContent = d.id + ' (' + d.state + ')';
        if (target) {
          sp.title = 'Jump to ' + target.id;
          sp.addEventListener('click', () => openModal(target));
        }
        depsWrap.appendChild(sp);
      });
    } else {
      depsWrap.innerHTML = '';
    }

    document.getElementById('mBody').innerHTML = renderMarkdownLite(t.body);
    document.getElementById('modalBackdrop').classList.add('open');
  }

  function closeModal() {
    document.getElementById('modalBackdrop').classList.remove('open');
    currentModalTask = null;
  }

  function syncTopbar() {
    const headings = { kanban: 'Kanban Board', table: 'Table View', history: 'Completed History' };
    document.getElementById('viewHeading').textContent = headings[state.view] || 'Backlog';

    ['kanban', 'table', 'history'].forEach(v => {
      const btn = document.getElementById('view' + v.charAt(0).toUpperCase() + v.slice(1));
      btn.classList.toggle('active', state.view === v);
    });
    const isHist = state.view === 'history';
    document.getElementById('btnReadyToggle').classList.toggle('hidden', isHist);
    document.getElementById('btnReadyToggle').classList.toggle('active', state.readyOnly);
    document.getElementById('kanbanView').classList.toggle('hidden', state.view !== 'kanban');
    document.getElementById('tableView').classList.toggle('hidden', state.view !== 'table');
    document.getElementById('historyView').classList.toggle('hidden', state.view !== 'history');
  }

  function render() {
    syncTopbar();
    renderSidebar();
    renderActiveFilterChips();
    renderSummary();
    if (state.view === 'history') {
      renderHistory();
      return;
    }
    const list = filteredTasks();
    document.getElementById('countNote').textContent = list.length + ' / ' + tasks.length;
    if (state.view === 'kanban') renderKanban(list);
    else renderTable(list);
  }

  // Event wiring
  document.querySelectorAll('[data-sb-view]').forEach(btn => {
    btn.addEventListener('click', () => {
      state.view = btn.getAttribute('data-sb-view');
      closeMobileSidebar();
      render();
    });
  });
  document.getElementById('viewKanban').addEventListener('click', () => { state.view = 'kanban'; render(); });
  document.getElementById('viewTable').addEventListener('click', () => { state.view = 'table'; render(); });
  document.getElementById('viewHistory').addEventListener('click', () => { state.view = 'history'; render(); });

  document.getElementById('qSearch').addEventListener('input', e => { state.q = e.target.value; render(); });
  document.getElementById('btnRefresh').addEventListener('click', refreshBoard);
  document.getElementById('btnStopServer').addEventListener('click', stopServer);
  document.getElementById('btnReadyToggle').addEventListener('click', () => {
    state.readyOnly = !state.readyOnly;
    if (state.readyOnly) state.status = '';
    render();
  });

  document.getElementById('clearStatus').addEventListener('click', () => { state.status = ''; state.readyOnly = false; render(); });
  document.getElementById('clearAssignee').addEventListener('click', () => { state.assignee = ''; render(); });
  document.getElementById('clearArea').addEventListener('click', () => { state.area = ''; render(); });

  document.getElementById('btnMenu').addEventListener('click', () => {
    document.getElementById('sidebar').classList.toggle('open');
    document.getElementById('sbBackdrop').classList.toggle('open');
  });
  document.getElementById('sbBackdrop').addEventListener('click', closeMobileSidebar);

  const savedTheme = localStorage.getItem('projectneo-board-theme') || 'dark';
  if (savedTheme === 'light') document.documentElement.setAttribute('data-theme', 'light');
  document.getElementById('btnTheme').addEventListener('click', () => {
    const next = document.documentElement.getAttribute('data-theme') === 'light' ? 'dark' : 'light';
    if (next === 'light') document.documentElement.setAttribute('data-theme', 'light');
    else document.documentElement.removeAttribute('data-theme');
    localStorage.setItem('projectneo-board-theme', next);
  });

  document.getElementById('mClose').addEventListener('click', closeModal);
  document.getElementById('modalBackdrop').addEventListener('click', e => { if (e.target.id === 'modalBackdrop') closeModal(); });
  document.addEventListener('keydown', e => {
    const tag = (e.target && e.target.tagName) ? e.target.tagName.toLowerCase() : '';
    const inInput = tag === 'input' || tag === 'textarea' || tag === 'select';
    if (e.key === 'Escape') {
      if (currentModalTask) closeModal();
      else if (inInput) e.target.blur();
      else closeMobileSidebar();
      return;
    }
    if (!inInput && !currentModalTask && !e.ctrlKey && !e.metaKey && !e.altKey) {
      if (e.key === '/') { e.preventDefault(); document.getElementById('qSearch').focus(); }
      else if (e.key === 'r' || e.key === 'R') { e.preventDefault(); refreshBoard(); }
      else if (e.key === '1') { state.view = 'kanban'; render(); }
      else if (e.key === '2') { state.view = 'table'; render(); }
      else if (e.key === '3' && history.length > 0) { state.view = 'history'; render(); }
    }
  });

  document.getElementById('mOpenVscode').addEventListener('click', () => {
    if (!currentModalTask) return;
    window.location.href = 'vscode://file/' + encodeURI(currentModalTask.fullPath);
  });
  document.getElementById('mCopyClaim').addEventListener('click', () => {
    if (!currentModalTask) return;
    copyText('/claim ' + currentModalTask.id, '/claim ' + currentModalTask.id);
  });
  document.getElementById('mCopyCheck').addEventListener('click', () => {
    if (!currentModalTask) return;
    copyText('powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Agent/claim-check.ps1 -TaskId "' + currentModalTask.id + '"', 'claim-check command');
  });
  document.getElementById('mCopyPath').addEventListener('click', () => {
    if (!currentModalTask) return;
    copyText(currentModalTask.relativePath, currentModalTask.relativePath);
  });

  hydrateData(DATA);
  render();
})();
</script>
</body>
</html>
'@
}

function Render-BoardHtml {
    param([object]$Payload)
    $json = ($Payload | ConvertTo-Json -Depth 6 -Compress) -replace '</', '<\/'
    return (Get-BoardHtmlTemplate).Replace('__BACKLOG_JSON__', $json)
}

function Test-BoardServerPing {
    param(
        [int]$TargetPort,
        [string]$ExpectedRepo
    )
    $url = "http://127.0.0.1:$TargetPort/api/ping"
    try {
        $req = [System.Net.HttpWebRequest]::Create($url)
        $req.Method = 'GET'
        $req.Timeout = 500
        $req.ReadWriteTimeout = 500
        $resp = $req.GetResponse()
        try {
            $sr = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
            $body = $sr.ReadToEnd()
            $sr.Dispose()
            $obj = $body | ConvertFrom-Json
            if ($obj -and $obj.ok -eq $true) {
                $same = ($obj.repo -ieq ($ExpectedRepo -replace '\\', '/'))
                return [PSCustomObject]@{ Running = $true; SameRepo = $same; Pid = $obj.pid }
            }
        } finally {
            $resp.Close()
        }
    } catch {
    }
    return [PSCustomObject]@{ Running = $false; SameRepo = $false; Pid = 0 }
}

function Write-HttpResponse {
    param(
        [System.Net.HttpListenerResponse]$Response,
        [int]$StatusCode,
        [string]$ContentType,
        [string]$Body
    )
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $bytes = $utf8.GetBytes($Body)
    $Response.StatusCode = $StatusCode
    $Response.ContentType = $ContentType
    $Response.Headers['Cache-Control'] = 'no-store, no-cache, must-revalidate'
    $Response.ContentLength64 = $bytes.Length
    $Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $Response.OutputStream.Close()
}

function Start-BoardHttpLoop {
    param(
        [string]$Root,
        [int]$ListenPort,
        [int]$IdleMin,
        [switch]$SkipHistory,
        [switch]$SkipFetch
    )
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://127.0.0.1:$ListenPort/")
    $listener.Start()

    $normRepo = ($Root -replace '\\', '/')
    $lastActivity = Get-Date
    $running = $true
    try {
        $ctxTask = $listener.GetContextAsync()
        while ($running -and $listener.IsListening) {
            if (-not $ctxTask.Wait(1000)) {
                if ($IdleMin -gt 0 -and ((Get-Date) - $lastActivity).TotalMinutes -ge $IdleMin) {
                    break
                }
                continue
            }
            $ctx = $ctxTask.GetAwaiter().GetResult()
            $lastActivity = Get-Date
            $ctxTask = $listener.GetContextAsync()

            $path = $ctx.Request.Url.AbsolutePath.ToLowerInvariant()
            try {
                if ($path -eq '/api/ping') {
                    $pingJson = ([PSCustomObject]@{ ok = $true; repo = $normRepo; pid = $PID } | ConvertTo-Json -Compress)
                    Write-HttpResponse -Response $ctx.Response -StatusCode 200 -ContentType 'application/json; charset=utf-8' -Body $pingJson
                } elseif ($path -eq '/api/shutdown') {
                    Write-HttpResponse -Response $ctx.Response -StatusCode 200 -ContentType 'application/json; charset=utf-8' -Body '{"ok":true}'
                    $running = $false
                } elseif ($path -eq '/api/refresh' -or $path -eq '/api/data') {
                    $payload = Build-BoardPayload -Root $Root -SkipHistory:$SkipHistory -SkipFetch:$SkipFetch -LiveMode:$true
                    $json = ($payload | ConvertTo-Json -Depth 6 -Compress)
                    Write-HttpResponse -Response $ctx.Response -StatusCode 200 -ContentType 'application/json; charset=utf-8' -Body $json
                } elseif ($path -eq '/' -or $path -eq '/index.html') {
                    $payload = Build-BoardPayload -Root $Root -SkipHistory:$SkipHistory -SkipFetch:$SkipFetch -LiveMode:$true
                    $html = Render-BoardHtml -Payload $payload
                    Write-HttpResponse -Response $ctx.Response -StatusCode 200 -ContentType 'text/html; charset=utf-8' -Body $html
                } else {
                    Write-HttpResponse -Response $ctx.Response -StatusCode 404 -ContentType 'text/plain; charset=utf-8' -Body 'Not Found'
                }
            } catch {
                try {
                    $errJson = ([PSCustomObject]@{ ok = $false; error = $_.Exception.Message } | ConvertTo-Json -Compress)
                    Write-HttpResponse -Response $ctx.Response -StatusCode 500 -ContentType 'application/json; charset=utf-8' -Body $errJson
                } catch {
                }
            }
        }
    } finally {
        try { $listener.Stop() } catch {}
        try { $listener.Close() } catch {}
    }
}

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    if ($ServeWorker) {
        Start-BoardHttpLoop -Root $root -ListenPort $Port -IdleMin $IdleMinutes -SkipHistory:$NoHistory -SkipFetch:$NoFetch
        return
    }

    # Mode selection:
    # - Explicit -Static or -OutPath => static HTML snapshot
    # - Explicit -Serve => live localhost server
    # - Default: static if -NoOpen (backward compatible for hooks/CLI checks), else live server
    $useStatic = $Static.IsPresent -or ([bool]$OutPath) -or ((-not $Serve.IsPresent) -and $NoOpen.IsPresent)

    if (-not $useStatic) {
        $chosenPort = $Port
        $alreadyRunning = $false
        for ($tryPort = $Port; $tryPort -lt ($Port + 10); $tryPort++) {
            $ping = Test-BoardServerPing -TargetPort $tryPort -ExpectedRepo $root
            if ($ping.Running -and $ping.SameRepo) {
                $chosenPort = $tryPort
                $alreadyRunning = $true
                break
            }
            if (-not $ping.Running) {
                $chosenPort = $tryPort
                break
            }
        }

        $url = "http://127.0.0.1:$chosenPort/"
        if ($Foreground) {
            Write-Output "BOARD_URL=$url"
            if (-not $NoOpen) { Start-Process -FilePath $url }
            Start-BoardHttpLoop -Root $root -ListenPort $chosenPort -IdleMin $IdleMinutes -SkipHistory:$NoHistory -SkipFetch:$NoFetch
            return
        }

        if (-not $alreadyRunning) {
            $workerArgs = New-Object System.Collections.Generic.List[string]
            [void]$workerArgs.Add('-NoProfile')
            [void]$workerArgs.Add('-ExecutionPolicy')
            [void]$workerArgs.Add('Bypass')
            [void]$workerArgs.Add('-WindowStyle')
            [void]$workerArgs.Add('Hidden')
            [void]$workerArgs.Add('-File')
            [void]$workerArgs.Add("`"$PSCommandPath`"")
            [void]$workerArgs.Add('-ServeWorker')
            [void]$workerArgs.Add('-Port')
            [void]$workerArgs.Add("$chosenPort")
            [void]$workerArgs.Add('-IdleMinutes')
            [void]$workerArgs.Add("$IdleMinutes")
            if ($NoHistory) { [void]$workerArgs.Add('-NoHistory') }
            if ($NoFetch) { [void]$workerArgs.Add('-NoFetch') }

            $proc = Start-Process -FilePath 'powershell.exe' -ArgumentList ($workerArgs -join ' ') -WorkingDirectory $root -WindowStyle Hidden -PassThru
            $ready = $false
            for ($i = 0; $i -lt 50; $i++) {
                Start-Sleep -Milliseconds 150
                $p = Test-BoardServerPing -TargetPort $chosenPort -ExpectedRepo $root
                if ($p.Running -and $p.SameRepo) {
                    $ready = $true
                    break
                }
                if ($proc.HasExited) { break }
            }
            if (-not $ready) {
                throw "Failed to start local backlog board server on port $chosenPort."
            }
        }

        Write-Output "BOARD_URL=$url"
        Write-Output "SERVER_REUSED=$alreadyRunning"
        if (-not $NoOpen) {
            Start-Process -FilePath $url
        }
        return
    }

    # Static HTML snapshot mode (reads from origin/develop -> develop fallback)
    if (-not $OutPath) {
        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) 'ProjectNeo'
        $OutPath = Join-Path $tempDir 'backlog-board.html'
    }
    $outFull = [System.IO.Path]::GetFullPath($OutPath)
    $outDir = Split-Path -Parent $outFull
    if ($outDir -and -not (Test-Path -LiteralPath $outDir -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $outDir -Force)
    }

    $payload = Build-BoardPayload -Root $root -SkipHistory:$NoHistory -SkipFetch:$NoFetch -LiveMode:$false
    $html = Render-BoardHtml -Payload $payload
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($outFull, $html, $utf8NoBom)

    Write-Output "BOARD_HTML=$outFull"
    Write-Output "SOURCE_REF=$($payload.sourceRef)@$($payload.sourceSha)"
    Write-Output "FETCH_STATUS=$($payload.fetchStatus)"
    Write-Output "ACTIVE_TASKS=$($payload.activeCount)"
    Write-Output "HISTORY_TASKS=$($payload.historyCount)"

    if (-not $NoOpen) {
        Start-Process -FilePath $outFull
    }
} finally {
    Pop-Location
}
