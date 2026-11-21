param(
    [string]$OutPath = '',
    [switch]$NoOpen,
    [switch]$NoHistory
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

function Read-TaskBody {
    param([string]$Path)
    $allLines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
    if ($allLines.Count -eq 0) { return '' }
    $startIdx = 0
    if ($allLines[0].Trim() -eq '---') {
        for ($i = 1; $i -lt $allLines.Count; $i++) {
            if ($allLines[$i].Trim() -eq '---') {
                $startIdx = $i + 1
                break
            }
        }
    }
    if ($startIdx -ge $allLines.Count) { return '' }
    return (($allLines[$startIdx..($allLines.Count - 1)]) -join "`n").Trim()
}

function Read-FirstLine {
    param([string]$Path)
    $sr = $null
    try {
        $sr = New-Object System.IO.StreamReader($Path, [System.Text.Encoding]::UTF8)
        $line = $sr.ReadLine()
        if ($null -eq $line) { return '' }
        return $line.Trim()
    } finally {
        if ($sr) { $sr.Dispose() }
    }
}

function Get-LegacyArchiveIds {
    param([string]$Root)
    $set = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    $archivePath = Join-Path $Root 'Docs\archive\Backlog-Done.md'
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) { return $set }
    $lines = Get-Content -LiteralPath $archivePath -Encoding UTF8
    foreach ($ln in $lines) {
        if ($ln -match '^\|\s*`?([A-Za-z0-9-]+)`?\s*\|') {
            [void]$set.Add($Matches[1])
        }
    }
    return $set
}

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    if (-not $OutPath) {
        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) 'ProjectNeo'
        $OutPath = Join-Path $tempDir 'backlog-board.html'
    }
    $outFull = [System.IO.Path]::GetFullPath($OutPath)
    $outDir = Split-Path -Parent $outFull
    if ($outDir -and -not (Test-Path -LiteralPath $outDir -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $outDir -Force)
    }

    $kstNow = Get-KstTimestamp
    $today = (Get-Date).Date
    $branchRes = Invoke-StdoutOnly -FilePath 'git' -Arguments @('branch', '--show-current')
    $currentBranch = if ($branchRes.ExitCode -eq 0 -and $branchRes.StdOut) { $branchRes.StdOut.Trim() } else { 'DETACHED' }

    $activeTasks = @(Get-ActiveBacklogTasks -Root $root)
    $activeIdSet = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($t in $activeTasks) {
        if ($t.Id) { [void]$activeIdSet.Add($t.Id) }
    }

    $historyIdSet = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    $historyItems = New-Object System.Collections.Generic.List[object]
    $histRoot = Join-Path $root 'Docs\history'
    if (Test-Path -LiteralPath $histRoot -PathType Container) {
        $rootPrefix = $root.TrimEnd('\') + '\'
        $histFiles = @(Get-ChildItem -LiteralPath $histRoot -Recurse -File -Filter '*.md' |
            Where-Object { $_.Name -ine 'README.md' } |
            Sort-Object Name)
        foreach ($hf in $histFiles) {
            $hid = [System.IO.Path]::GetFileNameWithoutExtension($hf.Name)
            [void]$historyIdSet.Add($hid)
            if (-not $NoHistory) {
                $area = (Split-Path (Split-Path $hf.FullName -Parent) -Leaf).ToUpperInvariant()
                $rel = ($hf.FullName.Substring($rootPrefix.Length) -replace '\\', '/')
                $firstLine = Read-FirstLine -Path $hf.FullName
                $hDate = ''
                $hTitle = $firstLine -replace '^#+\s*', ''
                if ($hTitle -match '^[^|]+\|\s*(\d{4}-\d{2}-\d{2})\s*\|\s*(.+)$') {
                    $hDate = $Matches[1]
                    $hTitle = $Matches[2].Trim()
                }
                [void]$historyItems.Add([PSCustomObject]@{
                    id           = $hid
                    area         = $area
                    date         = $hDate
                    title        = $hTitle
                    relativePath = $rel
                    fullPath     = ($hf.FullName -replace '\\', '/')
                })
            }
        }
    }

    $legacyDoneSet = Get-LegacyArchiveIds -Root $root

    $taskItems = New-Object System.Collections.Generic.List[object]
    foreach ($t in $activeTasks) {
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

        $bodyText = ''
        if ($t.FilePath -and (Test-Path -LiteralPath $t.FilePath -PathType Leaf)) {
            $bodyText = Read-TaskBody -Path $t.FilePath
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
            fullPath     = ($t.FilePath -replace '\\', '/')
            body         = $bodyText
        })
    }

    $payload = [PSCustomObject]@{
        generatedAt   = $kstNow
        currentBranch = $currentBranch
        repoRoot      = ($root -replace '\\', '/')
        activeCount   = $taskItems.Count
        historyCount  = $historyItems.Count
        tasks         = @($taskItems.ToArray())
        history       = @($historyItems.ToArray())
    }

    $json = ($payload | ConvertTo-Json -Depth 6 -Compress) -replace '</', '<\/'

    $htmlTemplate = @'
<!DOCTYPE html>
<html lang="ko">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>ProjectNeo Backlog Board</title>
<style>
  :root {
    --bg: #0f141c;
    --surface: #161d28;
    --surface-2: #1d2635;
    --border: #2a364a;
    --text: #e6edf5;
    --muted: #93a4bd;
    --accent: #58a6ff;
    --accent-soft: rgba(88, 166, 255, 0.14);
    --green: #3fb950;
    --green-soft: rgba(63, 185, 80, 0.15);
    --yellow: #d29922;
    --yellow-soft: rgba(210, 153, 34, 0.16);
    --red: #f85149;
    --red-soft: rgba(248, 81, 73, 0.16);
    --purple: #bc8cff;
    --purple-soft: rgba(188, 140, 255, 0.16);
    --cyan: #39c5cf;
    --cyan-soft: rgba(57, 197, 207, 0.15);
    --shadow: 0 8px 24px rgba(0, 0, 0, 0.35);
    --radius: 10px;
  }
  [data-theme="light"] {
    --bg: #f4f6f9;
    --surface: #ffffff;
    --surface-2: #f8fafc;
    --border: #d8e0ea;
    --text: #1b2430;
    --muted: #5c6b7e;
    --accent: #0969da;
    --accent-soft: rgba(9, 105, 218, 0.10);
    --green: #1a7f37;
    --green-soft: rgba(26, 127, 55, 0.12);
    --yellow: #9a6700;
    --yellow-soft: rgba(154, 103, 0, 0.13);
    --red: #cf222e;
    --red-soft: rgba(207, 34, 46, 0.12);
    --purple: #8250df;
    --purple-soft: rgba(130, 80, 223, 0.12);
    --cyan: #0e7490;
    --cyan-soft: rgba(14, 116, 144, 0.12);
    --shadow: 0 8px 24px rgba(15, 23, 42, 0.10);
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "Malgun Gothic", sans-serif;
    background: var(--bg);
    color: var(--text);
    line-height: 1.45;
  }
  .wrap { max-width: 1520px; margin: 0 auto; padding: 18px 22px 40px; }
  header {
    display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between;
    gap: 12px; margin-bottom: 16px; padding-bottom: 14px; border-bottom: 1px solid var(--border);
  }
  .title-block h1 { margin: 0; font-size: 20px; font-weight: 700; letter-spacing: -0.01em; }
  .meta-line { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 4px; font-size: 12px; color: var(--muted); }
  .meta-pill {
    background: var(--surface); border: 1px solid var(--border);
    padding: 2px 8px; border-radius: 999px; font-family: ui-monospace, Consolas, monospace;
  }
  .header-actions { display: flex; gap: 8px; align-items: center; }
  button, select, input {
    font: inherit; color: var(--text); background: var(--surface);
    border: 1px solid var(--border); border-radius: 8px; padding: 6px 11px;
  }
  button { cursor: pointer; font-size: 13px; transition: border-color 0.12s, background 0.12s; }
  button:hover { border-color: var(--accent); }
  button.active { background: var(--accent-soft); border-color: var(--accent); color: var(--accent); font-weight: 600; }

  .kpi-grid {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
    gap: 10px; margin-bottom: 14px;
  }
  .kpi {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 11px 14px; cursor: pointer; transition: border-color 0.12s, transform 0.12s;
  }
  .kpi:hover { border-color: var(--accent); transform: translateY(-1px); }
  .kpi .label { font-size: 12px; color: var(--muted); }
  .kpi .val { font-size: 24px; font-weight: 700; margin-top: 2px; }
  .kpi.inprog .val { color: var(--accent); }
  .kpi.ready .val { color: var(--green); }
  .kpi.decision .val { color: var(--yellow); }
  .kpi.blocked .val { color: var(--red); }

  .toolbar {
    display: flex; flex-wrap: wrap; gap: 8px; align-items: center;
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 10px 12px; margin-bottom: 16px;
  }
  .toolbar input[type="search"] { flex: 1 1 220px; min-width: 180px; }
  .toolbar select { font-size: 13px; }
  .toolbar .seg { display: inline-flex; gap: 4px; }
  .toolbar .chk {
    display: inline-flex; align-items: center; gap: 6px; font-size: 13px;
    padding: 5px 10px; border: 1px solid var(--border); border-radius: 8px; cursor: pointer;
    user-select: none; background: var(--surface-2);
  }
  .toolbar .chk input { margin: 0; padding: 0; }
  .count-note { font-size: 12px; color: var(--muted); margin-left: auto; }

  .board {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(270px, 1fr));
    gap: 14px; align-items: start;
  }
  .col {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 12px; min-height: 180px;
  }
  .col-head {
    display: flex; align-items: center; justify-content: space-between;
    margin-bottom: 10px; padding-bottom: 8px; border-bottom: 1px solid var(--border);
  }
  .col-title { font-size: 13px; font-weight: 700; display: flex; align-items: center; gap: 7px; }
  .dot { width: 9px; height: 9px; border-radius: 50%; display: inline-block; }
  .col-count {
    font-size: 12px; color: var(--muted); background: var(--surface-2);
    border: 1px solid var(--border); border-radius: 999px; padding: 1px 8px;
  }
  .cards { display: flex; flex-direction: column; gap: 9px; }
  .card {
    background: var(--surface-2); border: 1px solid var(--border); border-radius: 9px;
    padding: 11px 12px; cursor: pointer; transition: border-color 0.12s, transform 0.1s;
  }
  .card:hover { border-color: var(--accent); transform: translateY(-1px); }
  .card-top { display: flex; align-items: center; justify-content: space-between; gap: 6px; margin-bottom: 6px; }
  .task-id { font-family: ui-monospace, Consolas, monospace; font-size: 12px; font-weight: 700; color: var(--accent); }
  .badges { display: flex; flex-wrap: wrap; gap: 4px; }
  .badge {
    font-size: 11px; font-weight: 600; padding: 1px 6px; border-radius: 5px;
    border: 1px solid transparent; white-space: nowrap;
  }
  .badge.area { background: var(--accent-soft); color: var(--accent); }
  .badge.area-BUG { background: var(--red-soft); color: var(--red); }
  .badge.area-Q { background: var(--yellow-soft); color: var(--yellow); }
  .badge.area-UI { background: var(--purple-soft); color: var(--purple); }
  .badge.area-TOOL, .badge.area-QA, .badge.area-DOC { background: var(--cyan-soft); color: var(--cyan); }
  .badge.area-COMBAT, .badge.area-SIEGE { background: var(--green-soft); color: var(--green); }
  .badge.blocked { background: var(--red-soft); color: var(--red); border-color: rgba(248, 81, 73, 0.35); }
  .badge.invalid { background: var(--red-soft); color: var(--red); }
  .card-title { font-size: 13.5px; font-weight: 600; margin-bottom: 8px; word-break: break-word; }
  .card-foot {
    display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between;
    gap: 6px; font-size: 11.5px; color: var(--muted);
  }
  .chip {
    background: var(--surface); border: 1px solid var(--border);
    padding: 1px 7px; border-radius: 6px; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }
  .branch-chip {
    font-family: ui-monospace, Consolas, monospace; font-size: 11px; color: var(--cyan);
    background: var(--cyan-soft); border: 1px solid transparent; padding: 1px 6px; border-radius: 5px;
    margin-top: 6px; display: inline-block; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }

  .table-wrap {
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    overflow-x: auto;
  }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  th, td { padding: 9px 12px; text-align: left; border-bottom: 1px solid var(--border); vertical-align: top; }
  th { background: var(--surface-2); font-size: 12px; color: var(--muted); cursor: pointer; user-select: none; white-space: nowrap; }
  th:hover { color: var(--accent); }
  tbody tr { cursor: pointer; }
  tbody tr:hover { background: var(--surface-2); }
  .mono { font-family: ui-monospace, Consolas, monospace; font-size: 12px; }
  .status-pill {
    display: inline-block; padding: 2px 8px; border-radius: 999px; font-size: 11.5px; font-weight: 600; white-space: nowrap;
  }
  .st-inprog { background: var(--accent-soft); color: var(--accent); }
  .st-wait { background: var(--green-soft); color: var(--green); }
  .st-verif { background: var(--cyan-soft); color: var(--cyan); }
  .st-decision { background: var(--yellow-soft); color: var(--yellow); }
  .st-prereq { background: var(--purple-soft); color: var(--purple); }
  .st-other { background: var(--surface-2); color: var(--muted); }

  .empty { text-align: center; color: var(--muted); padding: 28px 12px; font-size: 13px; }

  .modal-backdrop {
    position: fixed; inset: 0; background: rgba(0, 0, 0, 0.62);
    display: none; align-items: center; justify-content: center; padding: 18px; z-index: 100;
  }
  .modal-backdrop.open { display: flex; }
  .modal {
    background: var(--surface); border: 1px solid var(--border); border-radius: 12px;
    max-width: 780px; width: 100%; max-height: 86vh; display: flex; flex-direction: column;
    box-shadow: var(--shadow);
  }
  .modal-head {
    display: flex; align-items: flex-start; justify-content: space-between; gap: 12px;
    padding: 15px 18px; border-bottom: 1px solid var(--border);
  }
  .modal-head h2 { margin: 4px 0 0; font-size: 17px; }
  .modal-body { padding: 16px 18px; overflow-y: auto; }
  .meta-grid {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
    gap: 8px; margin-bottom: 14px;
  }
  .meta-box {
    background: var(--surface-2); border: 1px solid var(--border); border-radius: 8px; padding: 8px 10px;
  }
  .meta-box .k { font-size: 11px; color: var(--muted); }
  .meta-box .v { font-size: 13px; font-weight: 600; margin-top: 2px; word-break: break-all; }
  .dep-badge {
    display: inline-block; font-family: ui-monospace, Consolas, monospace; font-size: 11.5px;
    padding: 2px 7px; border-radius: 6px; margin: 2px 4px 2px 0;
  }
  .dep-done { background: var(--green-soft); color: var(--green); }
  .dep-active { background: var(--red-soft); color: var(--red); }
  .dep-unknown { background: var(--yellow-soft); color: var(--yellow); }
  .md-body {
    background: var(--surface-2); border: 1px solid var(--border); border-radius: 8px;
    padding: 14px 16px; font-size: 13.5px; line-height: 1.6;
  }
  .md-body h1, .md-body h2, .md-body h3 { margin: 10px 0 6px; font-size: 14.5px; }
  .md-body h1:first-child, .md-body h2:first-child { margin-top: 0; }
  .md-body ul { margin: 6px 0; padding-left: 20px; }
  .md-body code {
    font-family: ui-monospace, Consolas, monospace; font-size: 12px;
    background: var(--surface); border: 1px solid var(--border); padding: 1px 5px; border-radius: 4px;
  }
  .modal-foot {
    display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: space-between;
    padding: 12px 18px; border-top: 1px solid var(--border);
  }
  .modal-actions { display: flex; flex-wrap: wrap; gap: 8px; }
  .toast {
    position: fixed; bottom: 18px; right: 18px; background: var(--surface);
    border: 1px solid var(--accent); color: var(--text); padding: 9px 14px;
    border-radius: 8px; box-shadow: var(--shadow); font-size: 13px;
    opacity: 0; pointer-events: none; transition: opacity 0.18s; z-index: 200;
  }
  .toast.show { opacity: 1; }
  .hidden { display: none !important; }
</style>
</head>
<body>
<div class="wrap">
  <header>
    <div class="title-block">
      <h1>ProjectNeo Backlog Board</h1>
      <div class="meta-line">
        <span class="meta-pill" id="metaBranch">branch</span>
        <span class="meta-pill" id="metaGen">generated</span>
        <span class="meta-pill">Read-only snapshot (Tools/Agent/backlog-board.ps1)</span>
      </div>
    </div>
    <div class="header-actions">
      <button id="btnReset">Reset Filters</button>
      <button id="btnTheme">Theme</button>
    </div>
  </header>

  <section class="kpi-grid" id="kpiGrid"></section>

  <section class="toolbar">
    <div class="seg">
      <button id="viewKanban" class="active">Kanban</button>
      <button id="viewTable">Table</button>
      <button id="viewHistory">History</button>
    </div>
    <input type="search" id="qSearch" placeholder="Search ID, title, assignee, branch, scope...">
    <select id="fArea"><option value="">All Areas</option></select>
    <select id="fAssignee"><option value="">All Assignees</option></select>
    <select id="fStatus"><option value="">All Statuses</option></select>
    <label class="chk" id="lblReady"><input type="checkbox" id="fReadyOnly"> Ready to Claim</label>
    <span class="count-note" id="countNote"></span>
  </section>

  <main id="kanbanView" class="board"></main>
  <main id="tableView" class="table-wrap hidden"></main>
  <main id="historyView" class="table-wrap hidden"></main>
</div>

<div class="modal-backdrop" id="modalBackdrop">
  <div class="modal" role="dialog" aria-modal="true">
    <div class="modal-head">
      <div>
        <div class="task-id" id="mId"></div>
        <h2 id="mTitle"></h2>
      </div>
      <button id="mClose">Close</button>
    </div>
    <div class="modal-body">
      <div class="meta-grid" id="mMeta"></div>
      <div id="mDepsWrap" style="margin-bottom:12px;"></div>
      <div class="md-body" id="mBody"></div>
    </div>
    <div class="modal-foot">
      <div class="modal-actions">
        <button id="mOpenVscode">Open in VS Code</button>
        <button id="mCopyClaim">Copy /claim</button>
        <button id="mCopyCheck">Copy claim-check</button>
        <button id="mCopyPath">Copy Path</button>
      </div>
      <span class="mono" style="color:var(--muted)" id="mRelPath"></span>
    </div>
  </div>
</div>

<div class="toast" id="toast"></div>

<script>
const DATA = __BACKLOG_JSON__;

(function() {
  function toArray(v) {
    return Array.isArray(v) ? v : (v ? [v] : []);
  }
  const tasks = toArray(DATA.tasks);
  const history = toArray(DATA.history);

  const STATUS_ORDER = {
    'In Progress': 1,
    'Waiting for Verification': 2,
    'Waiting for Link': 3,
    'Waiting': 4,
    'Pending Decision': 5,
    'Waiting for Prerequisite': 6
  };

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

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
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
    toast._t = setTimeout(() => el.classList.remove('show'), 1800);
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

  function initHeaderAndFilters() {
    document.getElementById('metaBranch').textContent = 'branch: ' + (DATA.currentBranch || '-');
    document.getElementById('metaGen').textContent = 'updated: ' + (DATA.generatedAt || '-');

    const areas = Array.from(new Set([
      ...tasks.map(t => t.area).filter(Boolean),
      ...history.map(h => h.area).filter(Boolean)
    ])).sort();
    const assignees = Array.from(new Set(tasks.map(t => t.assignee).filter(Boolean))).sort();
    const statuses = Array.from(new Set(tasks.map(t => t.status).filter(Boolean)))
      .sort((a, b) => (STATUS_ORDER[a] || 99) - (STATUS_ORDER[b] || 99) || a.localeCompare(b));

    const fArea = document.getElementById('fArea');
    areas.forEach(a => { const o = document.createElement('option'); o.value = a; o.textContent = a; fArea.appendChild(o); });
    const fAssignee = document.getElementById('fAssignee');
    assignees.forEach(a => { const o = document.createElement('option'); o.value = a; o.textContent = a; fAssignee.appendChild(o); });
    const fStatus = document.getElementById('fStatus');
    statuses.forEach(s => { const o = document.createElement('option'); o.value = s; o.textContent = s; fStatus.appendChild(o); });

    if (history.length === 0) {
      document.getElementById('viewHistory').classList.add('hidden');
    } else {
      document.getElementById('viewHistory').textContent = 'History (' + history.length + ')';
    }
  }

  function renderKpis() {
    const inProg = tasks.filter(t => t.status === 'In Progress').length;
    const ready = tasks.filter(isReady).length;
    const decision = tasks.filter(t => t.status === 'Pending Decision').length;
    const blocked = tasks.filter(isBlocked).length;

    const kpis = [
      { cls: '', label: 'Active Total', val: tasks.length, action: () => resetFilters() },
      { cls: 'inprog', label: 'In Progress', val: inProg, action: () => quickFilter({ status: 'In Progress' }) },
      { cls: 'ready', label: 'Ready to Claim', val: ready, action: () => quickFilter({ readyOnly: true }) },
      { cls: 'decision', label: 'Pending Decision', val: decision, action: () => quickFilter({ status: 'Pending Decision' }) },
      { cls: 'blocked', label: 'Blocked / Prereq', val: blocked, action: () => quickFilter({ status: 'Waiting for Prerequisite' }) }
    ];

    const grid = document.getElementById('kpiGrid');
    grid.innerHTML = '';
    kpis.forEach(k => {
      const d = document.createElement('div');
      d.className = 'kpi ' + k.cls;
      d.innerHTML = '<div class="label">' + esc(k.label) + '</div><div class="val">' + k.val + '</div>';
      d.addEventListener('click', k.action);
      grid.appendChild(d);
    });
  }

  function quickFilter(patch) {
    state.view = state.view === 'history' ? 'kanban' : state.view;
    state.status = patch.status || '';
    state.readyOnly = Boolean(patch.readyOnly);
    document.getElementById('fStatus').value = state.status;
    document.getElementById('fReadyOnly').checked = state.readyOnly;
    syncViewButtons();
    render();
  }

  function resetFilters() {
    state.q = '';
    state.area = '';
    state.assignee = '';
    state.status = '';
    state.readyOnly = false;
    document.getElementById('qSearch').value = '';
    document.getElementById('fArea').value = '';
    document.getElementById('fAssignee').value = '';
    document.getElementById('fStatus').value = '';
    document.getElementById('fReadyOnly').checked = false;
    render();
  }

  function filteredTasks() {
    const q = state.q.trim().toLowerCase();
    return tasks.filter(t => {
      if (state.area && t.area !== state.area) return false;
      if (state.assignee && t.assignee !== state.assignee) return false;
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
      { key: 'Waiting for Prerequisite', label: 'Prerequisite / Blocked', dot: 'var(--purple)', match: t => t.status === 'Waiting for Prerequisite' }
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
      col.innerHTML = '<div class="col-head"><div class="col-title"><span class="dot" style="background:' + c.dot + '"></span>' + esc(c.label) + '</div><span class="col-count">' + items.length + '</span></div>';
      const cards = document.createElement('div');
      cards.className = 'cards';
      if (items.length === 0) {
        cards.innerHTML = '<div class="empty">No tasks</div>';
      } else {
        items.forEach(t => {
          const card = document.createElement('article');
          card.className = 'card';
          const blockedBadge = t.blockedByDep ? '<span class="badge blocked">BLOCKED</span>' : '';
          const invalidBadge = t.valid === false ? '<span class="badge invalid">INVALID</span>' : '';
          const branchHtml = t.branch ? '<div class="branch-chip" title="' + esc(t.branch) + '">' + esc(t.branch) + '</div>' : '';
          card.innerHTML =
            '<div class="card-top">' +
              '<span class="task-id">' + esc(t.id) + '</span>' +
              '<div class="badges">' +
                '<span class="badge area area-' + esc(t.area) + '">' + esc(t.area) + '</span>' +
                blockedBadge + invalidBadge +
              '</div>' +
            '</div>' +
            '<div class="card-title">' + esc(t.title) + '</div>' +
            '<div class="card-foot">' +
              '<span class="chip">' + esc(t.assignee || 'Unassigned') + '</span>' +
              '<span>' + esc(ageLabel(t)) + '</span>' +
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
      const arrow = state.sortKey === c.k ? (state.sortDir === 1 ? ' ▲' : ' ▼') : '';
      html += '<th data-sort="' + c.k + '">' + esc(c.label) + arrow + '</th>';
    });
    html += '</tr></thead><tbody>';
    list.forEach((t, idx) => {
      const deps = toArray(t.dependsOn);
      const blocked = t.blockedByDep ? ' <span class="badge blocked">BLOCKED</span>' : '';
      const depStr = deps.length ? '<div style="margin-top:3px;font-size:11px;color:var(--muted)">deps: ' + deps.map(d => esc(d.id)).join(', ') + '</div>' : '';
      html += '<tr data-idx="' + idx + '">' +
        '<td class="mono" style="color:var(--accent);font-weight:700;white-space:nowrap;">' + esc(t.id) + '</td>' +
        '<td><span class="status-pill ' + statusClass(t.status) + '">' + esc(t.status) + '</span>' + blocked + '</td>' +
        '<td><span class="badge area area-' + esc(t.area) + '">' + esc(t.area) + '</span></td>' +
        '<td style="white-space:nowrap;">' + esc(t.assignee || '-') + '</td>' +
        '<td>' + esc(t.title) + depStr + '</td>' +
        '<td class="mono" style="color:var(--cyan);">' + esc(t.branch || '-') + '</td>' +
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

    document.getElementById('countNote').textContent = list.length + ' / ' + history.length + ' history files';
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
        '<td>' + esc(h.title || '-') + '</td>' +
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
    document.getElementById('mId').textContent = t.id + (t.blockedByDep ? '  [BLOCKED]' : '');
    document.getElementById('mTitle').textContent = t.title;
    document.getElementById('mRelPath').textContent = t.relativePath || '';

    const metaPairs = [
      ['Status', t.status || '-'],
      ['Assignee', t.assignee || 'Unassigned'],
      ['Area', t.area || '-'],
      ['Branch', t.branch || '(not claimed)'],
      ['Created', (t.created || '-') + ' (' + ageLabel(t) + ')']
    ];
    document.getElementById('mMeta').innerHTML = metaPairs.map(p =>
      '<div class="meta-box"><div class="k">' + esc(p[0]) + '</div><div class="v">' + esc(p[1]) + '</div></div>'
    ).join('');

    const deps = toArray(t.dependsOn);
    const depsWrap = document.getElementById('mDepsWrap');
    if (deps.length > 0) {
      depsWrap.innerHTML = '<div style="font-size:12px;color:var(--muted);margin-bottom:4px;">Dependencies</div>' +
        deps.map(d => {
          const cls = d.state === 'done' ? 'dep-done' : (d.state === 'active' ? 'dep-active' : 'dep-unknown');
          return '<span class="dep-badge ' + cls + '">' + esc(d.id) + ' (' + esc(d.state) + ')</span>';
        }).join('');
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

  function syncViewButtons() {
    ['kanban', 'table', 'history'].forEach(v => {
      const btn = document.getElementById('view' + v.charAt(0).toUpperCase() + v.slice(1));
      btn.classList.toggle('active', state.view === v);
    });
    const isHist = state.view === 'history';
    document.getElementById('fAssignee').classList.toggle('hidden', isHist);
    document.getElementById('fStatus').classList.toggle('hidden', isHist);
    document.getElementById('lblReady').classList.toggle('hidden', isHist);
    document.getElementById('kanbanView').classList.toggle('hidden', state.view !== 'kanban');
    document.getElementById('tableView').classList.toggle('hidden', state.view !== 'table');
    document.getElementById('historyView').classList.toggle('hidden', state.view !== 'history');
  }

  function render() {
    syncViewButtons();
    if (state.view === 'history') {
      renderHistory();
      return;
    }
    const list = filteredTasks();
    document.getElementById('countNote').textContent = list.length + ' / ' + tasks.length + ' active tasks';
    if (state.view === 'kanban') renderKanban(list);
    else renderTable(list);
  }

  document.getElementById('viewKanban').addEventListener('click', () => { state.view = 'kanban'; render(); });
  document.getElementById('viewTable').addEventListener('click', () => { state.view = 'table'; render(); });
  document.getElementById('viewHistory').addEventListener('click', () => { state.view = 'history'; render(); });
  document.getElementById('qSearch').addEventListener('input', e => { state.q = e.target.value; render(); });
  document.getElementById('fArea').addEventListener('change', e => { state.area = e.target.value; render(); });
  document.getElementById('fAssignee').addEventListener('change', e => { state.assignee = e.target.value; render(); });
  document.getElementById('fStatus').addEventListener('change', e => { state.status = e.target.value; render(); });
  document.getElementById('fReadyOnly').addEventListener('change', e => { state.readyOnly = e.target.checked; render(); });
  document.getElementById('btnReset').addEventListener('click', resetFilters);

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
  document.addEventListener('keydown', e => { if (e.key === 'Escape') closeModal(); });

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

  initHeaderAndFilters();
  renderKpis();
  render();
})();
</script>
</body>
</html>
'@

    $html = $htmlTemplate.Replace('__BACKLOG_JSON__', $json)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($outFull, $html, $utf8NoBom)

    Write-Output "BOARD_HTML=$outFull"
    Write-Output "ACTIVE_TASKS=$($taskItems.Count)"
    Write-Output "HISTORY_TASKS=$($historyItems.Count)"

    if (-not $NoOpen) {
        Start-Process -FilePath $outFull
    }
} finally {
    Pop-Location
}
