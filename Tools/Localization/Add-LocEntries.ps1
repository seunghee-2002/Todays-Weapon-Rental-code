<#
    Add-LocEntries.ps1

    Adds string table entries to a collection's 5 asset files at once
    (Shared Data + ko-KR / en / ja / zh-Hans), keeping the m_Id sets identical.

    Editing those files by hand is how id sets drift apart, and UseFallback then
    hides the drift by quietly rendering Korean. This does all five together or
    fails without touching anything.

    Input CSV (UTF-8 with BOM), columns:
        Collection,Id,Key,Smart,ko,en,ja,zhHans

      Smart  : 1 when the value uses Smart String named arguments, else 0
      values : write newlines as the two characters \n (YAML double-quoted style)

    Usage:
        powershell -File Tools\Localization\Add-LocEntries.ps1 -Csv <path> [-WhatIf]

    IMPORTANT: keep this file pure ASCII (comments included). Windows PowerShell
    5.1 reads BOM-less UTF-8 as ANSI, which silently corrupts non-ASCII literals.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Csv,
    [string]$Root = '.',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

$LOCALES  = @('ko-KR','en','ja','zh-Hans')
$COLUMN   = @{ 'ko-KR' = 'ko'; 'en' = 'en'; 'ja' = 'ja'; 'zh-Hans' = 'zhHans' }
$SMART_RID = '100'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Read-Asset([string]$path) {
    return [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
}
function Get-Newline([string]$text) {
    # core.autocrlf is on, so the working tree holds a mix of CRLF and LF files.
    if ($text -match "`r`n") { return "`r`n" } else { return "`n" }
}
function Write-Asset([string]$path, [string]$text) {
    # Unity writes these assets as UTF-8 without BOM; keep it that way or the
    # importer rewrites the whole file and the diff becomes unreviewable.
    [System.IO.File]::WriteAllText($path, $text, $utf8NoBom)
}

function Escape-Yaml([string]$v) {
    if ($null -eq $v) { $v = '' }
    # Backslash sequences the author typed (\n) must survive verbatim, so only
    # the quote character needs escaping here.
    return $v -replace '"', '\"'
}

# Inserts $block (already newline-terminated) so entries stay ordered by id.
# $entryHead is the regex that starts an entry line, e.g. '^  - m_Id: (\d+)$'.
function Insert-Sorted([string[]]$lines, [int]$id, [string[]]$block, [string]$listStart, [string]$listEnd) {
    $out   = New-Object System.Collections.ArrayList
    $inList = $false
    $done   = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if (-not $inList -and $l -eq $listStart) { $inList = $true; [void]$out.Add($l); continue }
        if ($inList -and -not $done) {
            if ($l -match '^  - m_Id: (\d+)\s*$' -and [int]$matches[1] -gt $id) {
                foreach ($b in $block) { [void]$out.Add($b) }
                $done = $true
            }
            elseif ($l -eq $listEnd) {
                foreach ($b in $block) { [void]$out.Add($b) }
                $done = $true
            }
        }
        [void]$out.Add($l)
    }
    if (-not $done) { throw "could not find an insertion point (listEnd '$listEnd' missing)" }
    return $out.ToArray()
}

# ------------------------------------------------------------------ input ----

$rows = Import-Csv $Csv -Encoding UTF8
if (-not $rows) { throw "no rows in $Csv" }

foreach ($r in $rows) {
    if ($r.Id -notmatch '^\d+$') { throw "bad Id '$($r.Id)' for key '$($r.Key)'" }
    if ([string]::IsNullOrWhiteSpace($r.Key)) { throw "empty Key for id $($r.Id)" }
    foreach ($loc in $LOCALES) {
        $v = $r.($COLUMN[$loc])
        if ([string]::IsNullOrWhiteSpace($v)) { throw "empty '$($COLUMN[$loc])' value for key '$($r.Key)'" }
    }
}

$byCollection = $rows | Group-Object Collection

foreach ($grp in $byCollection) {
    $col = $grp.Name
    $dir = Join-Path $Root "Assets\_Projects\Data\Localization\Tables\$col"
    if (-not (Test-Path $dir)) { throw "collection folder not found: $dir" }

    $sharedPath = Join-Path $dir "$col Shared Data.asset"
    $sharedText = Read-Asset $sharedPath
    $existing   = @{}
    foreach ($m in [regex]::Matches($sharedText, '(?m)^  - m_Id: (\d+)\s*$'))  { $existing[$m.Groups[1].Value] = $true }
    $existingKeys = @{}
    foreach ($m in [regex]::Matches($sharedText, '(?m)^    m_Key: (\S+)\s*$')) { $existingKeys[$m.Groups[1].Value] = $true }

    foreach ($r in $grp.Group) {
        if ($existing.ContainsKey($r.Id))       { throw "$col : id $($r.Id) already exists" }
        if ($existingKeys.ContainsKey($r.Key))  { throw "$col : key $($r.Key) already exists" }
    }

    # ---- shared data -------------------------------------------------------
    # A brand new collection has empty lists written inline ("m_Entries: []").
    # Open them up so the sorted insert has a list to insert into.
    $sharedNl = Get-Newline $sharedText
    $sharedText = $sharedText -replace '(?m)^  m_Entries: \[\]\s*$', '  m_Entries:'
    $lines = $sharedText -split "`r?`n"
    foreach ($r in ($grp.Group | Sort-Object { [int]$_.Id })) {
        $block = @(
            "  - m_Id: $($r.Id)",
            "    m_Key: $($r.Key)",
            "    m_Metadata:",
            "      m_Items: []"
        )
        $lines = Insert-Sorted $lines ([int]$r.Id) $block '  m_Entries:' '  m_Metadata:'
    }
    $newShared = ($lines -join $sharedNl)

    # ---- locale tables -----------------------------------------------------
    $localeOut = @{}
    foreach ($loc in $LOCALES) {
        $lp = Join-Path $dir "${col}_$loc.asset"
        if (-not (Test-Path $lp)) { throw "locale table not found: $lp" }
        $ltext = Read-Asset $lp
        $localeNl = Get-Newline $ltext
        $ltext = $ltext -replace '(?m)^  m_TableData: \[\]\s*$', '  m_TableData:'
        $llines = $ltext -split "`r?`n"

        foreach ($r in ($grp.Group | Sort-Object { [int]$_.Id })) {
            $val = Escape-Yaml $r.($COLUMN[$loc])
            $meta = if ($r.Smart -eq '1') { @("    m_Metadata:", "      m_Items:", "      - rid: $SMART_RID") }
                    else                  { @("    m_Metadata:", "      m_Items: []") }
            $block = @("  - m_Id: $($r.Id)", "    m_Localized: `"$val`"") + $meta
            $llines = Insert-Sorted $llines ([int]$r.Id) $block '  m_TableData:' '  references:'
        }
        $ltext = ($llines -join $localeNl)

        # register smart entries under the SmartFormatTag metadata
        $smartIds = @($grp.Group | Where-Object { $_.Smart -eq '1' } | ForEach-Object { $_.Id })
        if ($smartIds.Count -gt 0) {
            if ($ltext -notmatch 'SmartFormatTag') {
                throw "$col/$loc : Smart entries requested but the table has no SmartFormatTag metadata block"
            }
            # Open the inline empty list only now that there is an id to put in
            # it. A batch with no Smart rows used to open it anyway and leave a
            # valueless "m_SharedEntries:" key; that is the file's last line, so
            # Unity's YAML parser died at EOF.
            # [ \t]*$ instead of \s*$ on both patterns: \s* is greedy and eats
            # the file's trailing newline at EOF, which shifts the insert point
            # past it and injects a blank line before the first id.
            # CRLF safety: in .NET multiline mode '$' matches only before \n, so a
            # trailing \r makes '[ \t]*$' fail outright - on a CRLF checkout this
            # threw "m_SharedEntries list not found" even though the list was there.
            # Match \r with a LOOKAHEAD so it is never consumed (consuming it turns
            # that one line into LF and mixes line endings), and insert using the
            # file's own newline sequence.
            $eol = if ($ltext -match "`r`n") { "`r`n" } else { "`n" }
            $ltext = $ltext -replace '(?m)^        m_SharedEntries: \[\][ \t]*(?=\r?$)', '        m_SharedEntries:'
            $m = [regex]::Match($ltext, '(?m)^        m_SharedEntries:[ \t]*(?=\r?$)')
            if (-not $m.Success) { throw "$col/$loc : m_SharedEntries list not found" }
            $insertAt = $m.Index + $m.Length
            $add = ''
            foreach ($sid in $smartIds) { $add += "$eol        - id: $sid" }
            $ltext = $ltext.Substring(0, $insertAt) + $add + $ltext.Substring($insertAt)
        }
        $localeOut[$loc] = $ltext
    }

    # ---- commit ------------------------------------------------------------
    if ($WhatIf) {
        Write-Host ("[dry run] {0}: would add {1} entries" -f $col, $grp.Group.Count)
    } else {
        Write-Asset $sharedPath $newShared
        foreach ($loc in $LOCALES) { Write-Asset (Join-Path $dir "${col}_$loc.asset") $localeOut[$loc] }
        Write-Host ("{0}: added {1} entries ({2})" -f $col, $grp.Group.Count,
                    (($grp.Group | Sort-Object { [int]$_.Id } | ForEach-Object { $_.Id }) -join ', '))
    }
}

Write-Host 'done.'
