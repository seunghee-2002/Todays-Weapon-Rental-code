<#
    Remove-LocEntries.ps1

    Deletes string table entries from all 5 files of a collection at once
    (Shared Data + ko-KR / en / ja / zh-Hans), including any SmartFormatTag
    registration, so the id sets stay identical.

    Used when a key turns out to duplicate an existing one and the call sites
    move to the shared key.

    Usage:
        powershell -File Tools\Localization\Remove-LocEntries.ps1 `
            -Collection UI_Messages -Keys Visitor_SendBackBlacksmithConfirm [-WhatIf]

    Refuses to run if any key is still referenced from C# or from a prefab/scene
    LocalizeStringEvent, so a live key can never be deleted by accident.

    IMPORTANT: keep this file pure ASCII (comments included). Windows PowerShell
    5.1 reads BOM-less UTF-8 as ANSI, which silently corrupts non-ASCII literals.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Collection,
    [Parameter(Mandatory = $true)][string[]]$Keys,
    [string]$Root = '.',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$dir = Join-Path $Root "Assets\_Projects\Data\Localization\Tables\$Collection"
if (-not (Test-Path $dir)) { throw "collection folder not found: $dir" }

$sharedPath = Join-Path $dir "$Collection Shared Data.asset"
$sharedText = [System.IO.File]::ReadAllText($sharedPath, [System.Text.Encoding]::UTF8)

# key -> id
$ids = @{}
$sl = $sharedText -split "`r?`n"
for ($i = 0; $i -lt $sl.Count; $i++) {
    if ($sl[$i] -match '^  - m_Id: (\d+)\s*$' -and $sl[$i + 1] -match '^    m_Key: (\S+)\s*$') {
        $ids[$matches[1]] = ($sl[$i] -replace '^  - m_Id: ','')
    }
}
$idOf = @{}
for ($i = 0; $i -lt $sl.Count; $i++) {
    if ($sl[$i] -match '^  - m_Id: (\d+)\s*$') {
        $id = $matches[1]
        if ($sl[$i + 1] -match '^    m_Key: (\S+)\s*$') { $idOf[$matches[1]] = $id }
    }
}

$targets = @{}
foreach ($k in $Keys) {
    if (-not $idOf.ContainsKey($k)) { throw "$Collection : key '$k' not found" }
    $targets[$idOf[$k]] = $k
}

# ------------------------------------------------------- safety: in use? ----

$stillUsed = @()
Get-ChildItem (Join-Path $Root 'Assets\_Projects\Scripts') -Recurse -Filter '*.cs' | ForEach-Object {
    $txt = [System.IO.File]::ReadAllText($_.FullName)
    foreach ($k in $Keys) {
        if ($txt -match ('"' + [regex]::Escape($k) + '"')) { $stillUsed += ("{0} in {1}" -f $k, $_.Name) }
    }
}
$guid = (Select-String -Path ($sharedPath + '.meta') -Pattern '^guid: (\w+)').Matches[0].Groups[1].Value
Get-ChildItem (Join-Path $Root 'Assets\_Projects\Prefabs'), (Join-Path $Root 'Assets\Scenes') -Recurse -Include *.prefab,*.unity | ForEach-Object {
    $txt = [System.IO.File]::ReadAllText($_.FullName)
    if ($txt -notmatch ('GUID:' + $guid)) { return }
    foreach ($id in $targets.Keys) {
        if ($txt -match ("m_KeyId: $id\s")) { $stillUsed += ("{0} (id {1}) in {2}" -f $targets[$id], $id, $_.Name) }
    }
}
if ($stillUsed.Count -gt 0) {
    $stillUsed | ForEach-Object { Write-Host ('  STILL USED: ' + $_) }
    throw 'refusing to delete keys that are still referenced'
}

# ------------------------------------------------------------------ edit ----

function Remove-Entries([string]$path, [hashtable]$targetIds) {
    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $nl   = $(if ($text -match "`r`n") { "`r`n" } else { "`n" })
    $lines = $text -split "`r?`n"
    $out = New-Object System.Collections.ArrayList
    $i = 0
    $removed = 0
    while ($i -lt $lines.Count) {
        if ($lines[$i] -match '^  - m_Id: (\d+)\s*$' -and $targetIds.ContainsKey($matches[1])) {
            # skip this entry: its lines run until the next entry or the end of the list
            $i++
            while ($i -lt $lines.Count -and $lines[$i] -notmatch '^  - m_Id: \d+\s*$' -and $lines[$i] -notmatch '^  \w') { $i++ }
            $removed++
            continue
        }
        # SmartFormatTag registration lines
        if ($lines[$i] -match '^        - id: (\d+)\s*$' -and $targetIds.ContainsKey($matches[1])) { $i++; continue }
        [void]$out.Add($lines[$i]); $i++
    }
    # Dropping the last registered smart entry above leaves a valueless
    # "m_SharedEntries:" key. That is the final line of a locale table, so
    # Unity's YAML parser dies at EOF. Collapse it back to an inline empty list.
    $arr = $out.ToArray()
    for ($j = 0; $j -lt $arr.Count; $j++) {
        if ($arr[$j] -match '^        m_SharedEntries:[ \t]*$') {
            if ($j + 1 -ge $arr.Count -or $arr[$j + 1] -notmatch '^        - id: \d+\s*$') {
                $arr[$j] = '        m_SharedEntries: []'
            }
            break
        }
    }
    return @{ Text = ($arr -join $nl); Removed = $removed }
}

$files = @($sharedPath)
foreach ($loc in @('ko-KR','en','ja','zh-Hans')) { $files += (Join-Path $dir "${Collection}_$loc.asset") }

$results = @{}
foreach ($f in $files) {
    $r = Remove-Entries $f $targets
    if ($r.Removed -ne $targets.Count) {
        throw ("{0}: expected to remove {1} entries, removed {2}" -f (Split-Path $f -Leaf), $targets.Count, $r.Removed)
    }
    $results[$f] = $r.Text
}

if ($WhatIf) {
    Write-Host ("[dry run] {0}: would remove {1} entries from 5 files" -f $Collection, $targets.Count)
} else {
    foreach ($f in $files) { [System.IO.File]::WriteAllText($f, $results[$f], $utf8NoBom) }
    Write-Host ("{0}: removed {1} ({2})" -f $Collection, $targets.Count, ($Keys -join ', '))
}
