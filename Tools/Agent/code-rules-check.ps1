param(
    [string]$BaseBranch = 'develop',
    [string[]]$Files = @(),
    [switch]$AllRuntime,
    [int]$MaxLines = 30
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

function Strip-CSharpCommentsAndStrings {
    param([string]$Text)
    # Preserve line breaks so line numbers remain accurate.
    $sb = New-Object System.Text.StringBuilder ($Text.Length)
    $len = $Text.Length
    $i = 0
    while ($i -lt $len) {
        $c = $Text[$i]
        $next = if ($i + 1 -lt $len) { $Text[$i + 1] } else { [char]0 }

        # Line comment //
        if ($c -eq '/' -and $next -eq '/') {
            while ($i -lt $len -and $Text[$i] -ne "`n") {
                if ($Text[$i] -eq "`r") { [void]$sb.Append("`r") } else { [void]$sb.Append(' ') }
                $i++
            }
            continue
        }
        # Block comment /* ... */
        if ($c -eq '/' -and $next -eq '*') {
            [void]$sb.Append('  ')
            $i += 2
            while ($i -lt $len) {
                if ($Text[$i] -eq '*' -and ($i + 1 -lt $len) -and $Text[$i + 1] -eq '/') {
                    [void]$sb.Append('  ')
                    $i += 2
                    break
                }
                if ($Text[$i] -eq "`n" -or $Text[$i] -eq "`r") { [void]$sb.Append($Text[$i]) }
                else { [void]$sb.Append(' ') }
                $i++
            }
            continue
        }
        # Verbatim string @"..."
        if ($c -eq '@' -and $next -eq '"') {
            [void]$sb.Append('  ')
            $i += 2
            while ($i -lt $len) {
                if ($Text[$i] -eq '"') {
                    if (($i + 1 -lt $len) -and $Text[$i + 1] -eq '"') {
                        [void]$sb.Append('  ')
                        $i += 2
                        continue
                    }
                    [void]$sb.Append(' ')
                    $i++
                    break
                }
                if ($Text[$i] -eq "`n" -or $Text[$i] -eq "`r") { [void]$sb.Append($Text[$i]) }
                else { [void]$sb.Append(' ') }
                $i++
            }
            continue
        }
        # Regular string "..."
        if ($c -eq '"') {
            [void]$sb.Append(' ')
            $i++
            while ($i -lt $len) {
                if ($Text[$i] -eq '\' -and ($i + 1 -lt $len)) {
                    [void]$sb.Append('  ')
                    $i += 2
                    continue
                }
                if ($Text[$i] -eq '"') {
                    [void]$sb.Append(' ')
                    $i++
                    break
                }
                if ($Text[$i] -eq "`n" -or $Text[$i] -eq "`r") { [void]$sb.Append($Text[$i]) }
                else { [void]$sb.Append(' ') }
                $i++
            }
            continue
        }
        # Char literal '...'
        if ($c -eq "'") {
            [void]$sb.Append(' ')
            $i++
            while ($i -lt $len) {
                if ($Text[$i] -eq '\' -and ($i + 1 -lt $len)) {
                    [void]$sb.Append('  ')
                    $i += 2
                    continue
                }
                if ($Text[$i] -eq "'") {
                    [void]$sb.Append(' ')
                    $i++
                    break
                }
                [void]$sb.Append(' ')
                $i++
            }
            continue
        }

        [void]$sb.Append($c)
        $i++
    }
    return $sb.ToString()
}

function Get-LineNumberAtOffset {
    param([string]$Text, [int]$Offset)
    if ($Offset -le 0) { return 1 }
    $prefix = $Text.Substring(0, [Math]::Min($Offset, $Text.Length))
    return ([regex]::Matches($prefix, "`n").Count + 1)
}

$root = Get-ProjectNeoRoot -FallbackDir (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $root
try {
    $candidatePaths = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)

    if ($AllRuntime) {
        $runtimeDir = Join-Path $root 'Assets\_Project\Scripts\Runtime'
        if (Test-Path $runtimeDir) {
            Get-ChildItem $runtimeDir -Recurse -File -Filter '*.cs' | ForEach-Object {
                $rel = $_.FullName.Substring($root.Length + 1) -replace '\\', '/'
                [void]$candidatePaths.Add($rel)
            }
        }
    } elseif ($Files.Count -gt 0) {
        foreach ($f in $Files) {
            $norm = ($f -replace '\\', '/').TrimStart('./')
            [void]$candidatePaths.Add($norm)
        }
    } else {
        $gitQueries = @(
            @('diff', '--name-only', "$BaseBranch...HEAD"),
            @('diff', '--name-only'),
            @('diff', '--cached', '--name-only'),
            @('ls-files', '--others', '--exclude-standard')
        )
        foreach ($q in $gitQueries) {
            $res = Invoke-StdoutOnly -FilePath 'git' -Arguments $q
            if ($res.ExitCode -eq 0 -and $res.StdOut) {
                foreach ($item in ($res.StdOut -split "`r?`n")) {
                    if ($item -and $item.Trim()) {
                        [void]$candidatePaths.Add(($item.Trim() -replace '\\', '/'))
                    }
                }
            }
        }
    }

    $runtimeCs = @()
    $changedTests = @()
    $serializedAssets = @()
    $serializedExtRegex = '\.(unity|prefab|asset|mat|controller|anim)$'

    foreach ($p in ($candidatePaths | Sort-Object)) {
        $full = if ([System.IO.Path]::IsPathRooted($p)) { $p } else { Join-Path $root ($p -replace '/', '\') }
        if (-not (Test-Path $full -PathType Leaf)) { continue }
        if ($Files.Count -gt 0 -and $p -match '\.cs$') {
            $runtimeCs += $p
        } elseif ($p -match '^Assets/_Project/Scripts/Runtime/.*\.cs$') {
            $runtimeCs += $p
        } elseif ($p -match '^Assets/Tests/(EditMode|PlayMode)/.*\.cs$') {
            $changedTests += [System.IO.Path]::GetFileNameWithoutExtension($p)
        } elseif ($p -match '^Assets/' -and $p -match $serializedExtRegex) {
            $serializedAssets += $p
        }
    }

    $violations = @()

    foreach ($rel in $runtimeCs) {
        $full = if ([System.IO.Path]::IsPathRooted($rel)) { $rel } else { Join-Path $root ($rel -replace '/', '\') }
        $raw = Get-Content $full -Raw -Encoding UTF8
        if (-not $raw) { continue }
        $clean = Strip-CSharpCommentsAndStrings -Text $raw
        $lines = $clean -split "`r?`n"

        # Rule 1: Route randomness via ProjectNeo.Core.IRandomSource
        if ($rel -ne 'Assets/_Project/Scripts/Runtime/Core/RandomSource.cs') {
            for ($li = 0; $li -lt $lines.Count; $li++) {
                $line = $lines[$li]
                if ($line -match '\bUnityEngine\.Random\b' -or
                    $line -match '\bnew\s+(?:System\.)?Random\s*\(' -or
                    $line -match '\bRandom\.(?:Range|value|insideUnitSphere|insideUnitCircle|onUnitSphere|rotation|rotationUniform|ColorHSV|InitState|state)\b') {
                    $violations += "${rel}:$($li + 1) [RANDOM_DIRECT] Use ProjectNeo.Core.IRandomSource instead of direct Random"
                }
            }
        }

        # Rule 2: One class per file & filename match for MonoBehaviour / ScriptableObject
        $fileStem = [System.IO.Path]::GetFileNameWithoutExtension($rel)
        $unityTypes = @()
        $classMatches = [regex]::Matches($clean, '\bclass\s+([A-Za-z_]\w*)(?:\s*<[^>]+>)?\s*:\s*([^{]+)')
        foreach ($cm in $classMatches) {
            $clsName = $cm.Groups[1].Value
            $baseList = $cm.Groups[2].Value
            if ($baseList -match '\b(MonoBehaviour|ScriptableObject)\b') {
                $ln = Get-LineNumberAtOffset -Text $clean -Offset $cm.Index
                $unityTypes += [PSCustomObject]@{ Name = $clsName; Base = $Matches[1]; Line = $ln }
            }
        }
        if ($unityTypes.Count -gt 1) {
            $names = ($unityTypes | ForEach-Object { $_.Name }) -join ', '
            $violations += "${rel}:$($unityTypes[0].Line) [ONE_CLASS_PER_FILE] Multiple MonoBehaviour/ScriptableObject classes in one file ($names)"
        }
        foreach ($ut in $unityTypes) {
            if ($ut.Name -cne $fileStem) {
                $violations += "${rel}:$($ut.Line) [ONE_CLASS_PER_FILE] $($ut.Base) '$($ut.Name)' does not match filename '$fileStem.cs'"
            }
        }

        # Rule 3: Explicit numeric values on public/internal enums
        $enumMatches = [regex]::Matches($clean, '\b(public|internal)\s+enum\s+([A-Za-z_]\w*)(?:\s*:\s*[A-Za-z_]\w*)?\s*\{')
        foreach ($em in $enumMatches) {
            $enumName = $em.Groups[2].Value
            $bodyStart = $em.Index + $em.Length
            $bodyEnd = $clean.IndexOf('}', $bodyStart)
            if ($bodyEnd -lt 0) { continue }
            $body = $clean.Substring($bodyStart, $bodyEnd - $bodyStart)
            $entryCursor = 0
            foreach ($rawEntry in ($body -split ',')) {
                $trimmed = ($rawEntry -replace '\[[^\]]*\]', '').Trim()
                if ($trimmed -and ($trimmed -notmatch '=')) {
                    $memberName = ($trimmed -split '\s+')[0]
                    $memberIdx = $rawEntry.IndexOf($memberName)
                    $exactOffset = $bodyStart + $entryCursor + $(if ($memberIdx -ge 0) { $memberIdx } else { 0 })
                    $entryLine = Get-LineNumberAtOffset -Text $clean -Offset $exactOffset
                    $violations += "${rel}:${entryLine} [ENUM_EXPLICIT_VALUE] enum '$enumName' member '$memberName' missing explicit numeric value"
                }
                $entryCursor += $rawEntry.Length + 1
            }
        }
    }

    # Suggest scoped EditMode/PlayMode test filters (changed tests first, then stem/module matches)
    $suggestedFilters = New-Object System.Collections.Generic.List[string]
    if (-not $AllRuntime -and ($runtimeCs.Count -gt 0 -or $changedTests.Count -gt 0)) {
        $seenTests = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($ct in ($changedTests | Sort-Object -Unique)) {
            if ($seenTests.Add($ct)) { $suggestedFilters.Add($ct) }
        }

        $stemKeywords = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        $folderKeywords = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($rel in $runtimeCs) {
            $stem = [System.IO.Path]::GetFileNameWithoutExtension($rel)
            if ($stem.Length -ge 4) { [void]$stemKeywords.Add($stem) }
            $parts = $rel -split '/'
            if ($parts.Count -ge 5) {
                $folder = $parts[4]
                if ($folder -and $folder.Length -ge 4 -and $folder -notin @('Core', 'Flow')) {
                    $singular = if ($folder.EndsWith('s') -and $folder.Length -gt 4) { $folder.Substring(0, $folder.Length - 1) } else { $folder }
                    [void]$folderKeywords.Add($singular)
                }
            }
        }
        $testRoots = @(
            (Join-Path $root 'Assets\Tests\EditMode'),
            (Join-Path $root 'Assets\Tests\PlayMode')
        )
        $stemMatches = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        $folderMatches = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($tr in $testRoots) {
            if (-not (Test-Path $tr)) { continue }
            Get-ChildItem $tr -Recurse -File -Filter '*.cs' | ForEach-Object {
                $tStem = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
                if ($seenTests.Contains($tStem)) { return }
                $matched = $false
                foreach ($kw in $stemKeywords) {
                    if ($tStem -like "*$kw*") {
                        [void]$stemMatches.Add($tStem)
                        $matched = $true
                        break
                    }
                }
                if (-not $matched) {
                    foreach ($kw in $folderKeywords) {
                        if ($tStem -like "*$kw*") {
                            [void]$folderMatches.Add($tStem)
                            break
                        }
                    }
                }
            }
        }
        foreach ($m in ($stemMatches | Sort-Object)) {
            if ($suggestedFilters.Count -ge 8) { break }
            if ($seenTests.Add($m)) { $suggestedFilters.Add($m) }
        }
        foreach ($m in ($folderMatches | Sort-Object)) {
            if ($suggestedFilters.Count -ge 8) { break }
            if ($seenTests.Add($m)) { $suggestedFilters.Add($m) }
        }
    }

    Write-Output "SCANNED_CS_FILES=$($runtimeCs.Count)"
    Write-Output "VIOLATIONS=$($violations.Count)"
    if ($violations.Count -eq 0) {
        Write-Output "CODE_RULES=PASSED"
    } else {
        Write-Output "CODE_RULES=FAILED"
        Write-BoundedLines -Lines $violations -MaxLines $MaxLines -TruncateHint "re-run code-rules-check.ps1 with larger -MaxLines"
    }

    Write-Output "SERIALIZED_ASSET_CHANGES=$($serializedAssets.Count)"
    if ($serializedAssets.Count -gt 0) {
        Write-Output "NOTE=Verify serialized Unity assets were updated via Editor APIs, not hand-edited:"
        Write-BoundedLines -Lines $serializedAssets -MaxLines 15
    }

    if ($suggestedFilters.Count -gt 0) {
        Write-Output "SUGGESTED_TEST_FILTERS=$($suggestedFilters -join ', ')"
    } else {
        Write-Output "SUGGESTED_TEST_FILTERS=NONE"
    }

    if ($violations.Count -gt 0) { exit 2 }
} finally {
    Pop-Location
}
