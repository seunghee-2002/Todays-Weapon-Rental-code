<#
    Audit-UiText.ps1

    Builds a per-item ledger of every Korean string baked into prefab / scene
    YAML, and classifies each one by how it must be handled.

    Unlike a plain "korean count minus LocalizeStringEvent count", this walks the
    YAML object graph so it can tell apart:
      * a label nobody touches      -> must get a LocalizeStringEvent
      * a placeholder the code owns -> must NOT get one; the code does the lookup
      * something out of scope      -> listed in exclusions.csv with a reason

    Output: out/ui-ledger.csv  +  a summary on stdout.

    IMPORTANT: keep this file pure ASCII (comments included). Windows PowerShell
    5.1 reads BOM-less UTF-8 as ANSI, which silently corrupts non-ASCII literals.
#>
[CmdletBinding()]
param(
    [string]$Root   = '.',
    [string]$OutDir = 'Tools\Localization\out'
)

$ErrorActionPreference = 'Stop'

$TMP_GUID = 'f4688fdb7df04437aeb418b961361dc5'
$LSE_GUID = '56eb0353ae6e5124bb35b17aff880f16'

$scanDirs = @(
    (Join-Path $Root 'Assets\_Projects\Prefabs'),
    (Join-Path $Root 'Assets\Scenes')
)
$scriptDir = Join-Path $Root 'Assets\_Projects\Scripts'

# ---------------------------------------------------------------- helpers ----

function Decode-Unicode([string]$s) {
    if ($null -eq $s) { return '' }
    [regex]::Replace($s, '\\u([0-9A-Fa-f]{4})', {
        param($m) [string][char][int]('0x' + $m.Groups[1].Value)
    })
}

function Has-Korean([string]$raw) {
    # Unity escapes non-ASCII in prefab YAML, so Korean normally shows up as
    # \uXXXX with a codepoint in the syllable (AC00-D7A3), jamo (1100-11FF) or
    # compatibility-jamo (3130-318F) block. Literal UTF-8 is checked as a
    # fallback via the .NET regex escapes below (this file stays pure ASCII).
    if ($raw -match '\\u(?:[A-D][0-9A-Fa-f]{3})') {
        foreach ($m in [regex]::Matches($raw, '\\u([0-9A-Fa-f]{4})')) {
            $cp = [int]('0x' + $m.Groups[1].Value)
            if (($cp -ge 0xAC00 -and $cp -le 0xD7A3) -or
                ($cp -ge 0x1100 -and $cp -le 0x11FF) -or
                ($cp -ge 0x3130 -and $cp -le 0x318F)) { return $true }
        }
    }
    foreach ($ch in $raw.ToCharArray()) {
        $cp = [int]$ch
        if (($cp -ge 0xAC00 -and $cp -le 0xD7A3) -or
            ($cp -ge 0x1100 -and $cp -le 0x11FF) -or
            ($cp -ge 0x3130 -and $cp -le 0x318F)) { return $true }
    }
    return $false
}

# Reads a scalar that may be folded across several lines. Returns the joined raw
# text and advances the index past the last consumed line.
function Read-FoldedScalar([string[]]$lines, [int]$start, [string]$first) {
    $val = $first
    $i   = $start
    if ($val.StartsWith('"')) {
        while (-not ($val -match '(?<!\\)"\s*$') -or $val -eq '"') {
            $i++
            if ($i -ge $lines.Count) { break }
            $val = $val + ' ' + $lines[$i].Trim()
        }
    }
    return @{ Value = $val; End = $i }
}

# ------------------------------------------------- guid -> script name map ----

Write-Host 'Indexing script GUIDs...'
$guidToScript = @{}
Get-ChildItem $scriptDir -Recurse -Filter '*.cs.meta' | ForEach-Object {
    $mt = [System.IO.File]::ReadAllText($_.FullName)
    if ($mt -match 'guid:\s*(\w+)') {
        $guidToScript[$matches[1]] = @{
            Name = $_.BaseName -replace '\.cs$',''
            Path = ($_.FullName -replace '\.meta$','')
        }
    }
}

$scriptTextCache = @{}
function Get-ScriptText([string]$path) {
    if (-not $scriptTextCache.ContainsKey($path)) {
        if (Test-Path $path) { $scriptTextCache[$path] = [System.IO.File]::ReadAllText($path) }
        else { $scriptTextCache[$path] = '' }
    }
    return $scriptTextCache[$path]
}

# How the script uses this field:
#   write    - assigns <field>.text directly (or calls <field>.SetText)
#   indirect - hands <field> to a method, which may well set .text on it.
#              PreparationEventRateTooltip does exactly this: SetRow(battleRateText, ...)
#              sets label.text on the parameter, so looking only for "<field>.text ="
#              would call it a plain label and we would attach a component that
#              then fights the code for the same TMP.
#   none     - only touches .color / .gameObject / null checks -> a baked label
function Get-FieldUsage([string]$scriptPath, [string]$field) {
    $txt = Get-ScriptText $scriptPath
    if ($txt -eq '') { return 'none' }
    $f = [regex]::Escape($field)
    if ([regex]::IsMatch($txt, $f + '\s*(?:\?)?\s*\.\s*(?:text\s*(?:\+|\-)?=(?!=)|SetText\s*\()')) { return 'write' }
    if ([regex]::IsMatch($txt, '[(,]\s*' + $f + '\s*[,)]')) { return 'indirect' }
    return 'none'
}

# ------------------------------------------------------------ exclusions ----

$exclusions = @{}
$exPath = Join-Path $Root 'Tools\Localization\exclusions.csv'
if (Test-Path $exPath) {
    # -Encoding UTF8 is mandatory: PowerShell 5.1 defaults to ANSI here.
    Import-Csv $exPath -Encoding UTF8 | ForEach-Object { $exclusions[$_.Key] = $_.Reason }
}
function Get-Exclusion([string]$file, [string]$anchor) {
    if ($exclusions.ContainsKey("$file|*"))      { return $exclusions["$file|*"] }
    if ($exclusions.ContainsKey("$file|$anchor")){ return $exclusions["$file|$anchor"] }
    return $null
}

# ------------------------------------------------------------- main scan ----

$rows = New-Object System.Collections.ArrayList
$files = Get-ChildItem $scanDirs -Recurse -Include *.prefab,*.unity | Sort-Object Name

Write-Host ("Scanning {0} prefab/scene files..." -f $files.Count)

foreach ($file in $files) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)

    $goName      = @{}   # go anchor      -> name
    $goTransform = @{}   # go anchor      -> transform anchor
    $trToGo      = @{}   # transform      -> go anchor
    $trFather    = @{}   # transform      -> parent transform
    $tmpDocs     = @{}   # tmp anchor     -> @{ Go; Text; Line }
    $lseTargets  = @{}   # target anchor  -> key id
    $refs        = @{}   # tmp anchor     -> list of "Script.field"
    $otherKorean = New-Object System.Collections.ArrayList

    $cur = $null; $curClass = $null
    $docGo = $null; $docScript = $null; $docName = $null
    $docText = $null; $docTextLine = 0; $docFather = $null
    $docFields = @{}; $docTarget = $null; $docKeyId = $null
    $curField = $null
    $inPrefabInstance = $false

    function Flush-Doc {
        if ($null -eq $script:cur) { return }
        switch ($script:curClass) {
            '1'   { $goName[$script:cur] = $script:docName }
            '4'   { $trToGo[$script:cur] = $script:docGo; $trFather[$script:cur] = $script:docFather
                    if ($script:docGo) { $goTransform[$script:docGo] = $script:cur } }
            '224' { $trToGo[$script:cur] = $script:docGo; $trFather[$script:cur] = $script:docFather
                    if ($script:docGo) { $goTransform[$script:docGo] = $script:cur } }
            '114' {
                if ($script:docScript -eq $TMP_GUID) {
                    if ($null -ne $script:docText) {
                        $tmpDocs[$script:cur] = @{ Go = $script:docGo; Text = $script:docText; Line = $script:docTextLine }
                    }
                } elseif ($script:docScript -eq $LSE_GUID) {
                    if ($script:docTarget) { $lseTargets[$script:docTarget] = $script:docKeyId }
                } else {
                    $sn = 'UnknownScript'
                    if ($script:docScript -and $guidToScript.ContainsKey($script:docScript)) {
                        $sn = $guidToScript[$script:docScript].Name
                    }
                    foreach ($fk in $script:docFields.Keys) {
                        foreach ($tid in $script:docFields[$fk]) {
                            if (-not $refs.ContainsKey($tid)) { $refs[$tid] = New-Object System.Collections.ArrayList }
                            [void]$refs[$tid].Add(@{ Script = $sn; Field = $fk; Guid = $script:docScript })
                        }
                    }
                }
            }
        }
    }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]

        if ($l -match '^--- !u!(\d+) &(\d+)') {
            Flush-Doc
            $curClass = $matches[1]; $cur = $matches[2]
            $docGo = $null; $docScript = $null; $docName = $null
            $docText = $null; $docTextLine = 0; $docFather = $null
            $docFields = @{}; $docTarget = $null; $docKeyId = $null
            $curField = $null
            $inPrefabInstance = ($curClass -eq '1001')
            continue
        }

        if ($l -match '^  m_GameObject: \{fileID: (\d+)\}')      { $docGo = $matches[1]; continue }
        if ($l -match '^  m_Script: \{fileID: \d+, guid: (\w+)') { $docScript = $matches[1]; continue }
        if ($curClass -eq '1' -and $l -match '^  m_Name: (.*)$')  { $docName = $matches[1].Trim('"'); continue }
        if ($l -match '^  m_Father: \{fileID: (\d+)\}')           { $docFather = $matches[1]; continue }
        if ($l -match '^      - m_Target: \{fileID: (\d+)\}')     { $docTarget = $matches[1]; continue }
        if ($l -match '^      m_KeyId: (\d+)')                    { $docKeyId = $matches[1]; continue }

        if ($l -match '^  m_text: (.*)$') {
            $r = Read-FoldedScalar $lines $i $matches[1]
            $docText = $r.Value; $docTextLine = $i + 1; $i = $r.End
            continue
        }

        # local object references, used to work out which script field owns a TMP
        if ($l -match '^  (\w+): \{fileID: (\d+)\}$') {
            $fn = $matches[1]; $fid = $matches[2]
            if ($fid -ne '0' -and $fn -notmatch '^m_') {
                if (-not $docFields.ContainsKey($fn)) { $docFields[$fn] = New-Object System.Collections.ArrayList }
                [void]$docFields[$fn].Add($fid)
            }
            $curField = $null; continue
        }
        if ($l -match '^  (\w+):\s*$')      { $curField = $matches[1]; continue }
        if ($l -match '^  - \{fileID: (\d+)\}$') {
            if ($curField -and $matches[1] -ne '0' -and $curField -notmatch '^m_') {
                if (-not $docFields.ContainsKey($curField)) { $docFields[$curField] = New-Object System.Collections.ArrayList }
                [void]$docFields[$curField].Add($matches[1])
            }
            continue
        }

        # any other serialized field that carries Korean (e.g. displayName lists)
        if (-not $inPrefabInstance -and $l -match '\\u[A-D][0-9A-Fa-f]{3}' -and $l -notmatch '^\s*m_text:') {
            if ($l -match '^\s*-?\s*(\w+):\s*(.*)$') {
                $k = $matches[1]
                if ($k -ne 'm_text' -and (Has-Korean $matches[2])) {
                    $sn = 'n/a'
                    if ($docScript -and $guidToScript.ContainsKey($docScript)) { $sn = $guidToScript[$docScript].Name }
                    [void]$otherKorean.Add(@{ Line = $i + 1; Key = $k; Raw = $matches[2]; Script = $sn })
                }
            }
        }
    }
    Flush-Doc

    # path of a GameObject, for readable reporting
    function Get-GoPath([string]$go) {
        $parts = New-Object System.Collections.ArrayList
        $guard = 0
        $g = $go
        while ($g -and $guard -lt 40) {
            $guard++
            if ($goName.ContainsKey($g)) { [void]$parts.Insert(0, $goName[$g]) } else { [void]$parts.Insert(0, "?$g") }
            if (-not $goTransform.ContainsKey($g)) { break }
            $tr = $goTransform[$g]
            if (-not $trFather.ContainsKey($tr) -or -not $trFather[$tr] -or $trFather[$tr] -eq '0') { break }
            $ft = $trFather[$tr]
            if (-not $trToGo.ContainsKey($ft)) { break }
            $g = $trToGo[$ft]
        }
        return ($parts -join '/')
    }

    foreach ($anchor in $tmpDocs.Keys) {
        $d = $tmpDocs[$anchor]
        if (-not (Has-Korean $d.Text)) { continue }

        $excl = Get-Exclusion $file.Name $anchor
        $refList  = @()
        $writes   = @()
        $indirect = @()
        $unknown  = @()
        if ($refs.ContainsKey($anchor)) {
            foreach ($r in $refs[$anchor]) {
                $refList += ("{0}.{1}" -f $r.Script, $r.Field)
                if ($r.Guid -and $guidToScript.ContainsKey($r.Guid)) {
                    switch (Get-FieldUsage $guidToScript[$r.Guid].Path $r.Field) {
                        'write'    { $writes   += ("{0}.{1}" -f $r.Script, $r.Field) }
                        'indirect' { $indirect += ("{0}.{1}" -f $r.Script, $r.Field) }
                    }
                } else {
                    # The referencing MonoBehaviour's script could not be resolved, so
                    # there is no way to tell whether the code writes .text here.
                    # Never call this MISSING - attaching blind risks a conflict.
                    $unknown += ("{0} (guid {1})" -f $r.Field, $r.Guid)
                }
            }
        }

        $verdict = ''
        if     ($excl)                            { $verdict = 'EXCLUDED' }
        elseif ($lseTargets.ContainsKey($anchor)) { $verdict = 'ATTACHED' }
        elseif ($writes.Count -gt 0)              { $verdict = 'CODE_DRIVEN' }
        elseif ($indirect.Count -gt 0)            { $verdict = 'REVIEW' }
        elseif ($unknown.Count -gt 0)             { $verdict = 'UNKNOWN_SCRIPT' }
        else                                      { $verdict = 'MISSING' }

        [void]$rows.Add([pscustomobject]@{
            File     = $file.Name
            Kind     = 'm_text'
            Verdict  = $verdict
            GoPath   = (Get-GoPath $d.Go)
            Anchor   = $anchor
            Line     = $d.Line
            KeyId    = $(if ($lseTargets.ContainsKey($anchor)) { $lseTargets[$anchor] } else { '' })
            Refs     = ($refList  -join '; ')
            Writers  = ($writes   -join '; ')
            Indirect = ($indirect -join '; ')
            Unknown  = ($unknown  -join '; ')
            Text     = (Decode-Unicode $d.Text).Trim('"')
            Note     = $excl
            Path     = ($file.FullName.Substring($file.FullName.IndexOf('Assets')))
        })
    }

    foreach ($o in $otherKorean) {
        $excl = Get-Exclusion $file.Name '*'
        [void]$rows.Add([pscustomobject]@{
            File     = $file.Name
            Kind     = ('field:' + $o.Key)
            Verdict  = $(if ($excl) { 'EXCLUDED' } else { 'MISSING' })
            GoPath   = ''
            Anchor   = ''
            Line     = $o.Line
            KeyId    = ''
            Refs     = $o.Script
            Writers  = ''
            Indirect = ''
            Unknown  = ''
            Text     = (Decode-Unicode $o.Raw).Trim('"')
            Note     = $excl
            Path     = ($file.FullName.Substring($file.FullName.IndexOf('Assets')))
        })
    }
}

# --------------------------------------------------------------- output -----

if (-not (Test-Path $OutDir)) { [void](New-Item -ItemType Directory -Path $OutDir -Force) }
$csv = Join-Path $OutDir 'ui-ledger.csv'
$rows | Sort-Object Verdict, File, Line | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8

Write-Host ''
Write-Host ('Ledger written: {0}  ({1} rows)' -f $csv, $rows.Count)
Write-Host ''
$rows | Group-Object Verdict | Sort-Object Name | ForEach-Object {
    Write-Host ('  {0,-12} {1}' -f $_.Name, $_.Count)
}
Write-Host ''
$review = @($rows | Where-Object { $_.Verdict -eq 'REVIEW' })
if ($review.Count -gt 0) {
    Write-Host 'REVIEW - field is handed to a method; check by hand whether the code sets .text:'
    $review | Group-Object Path | Sort-Object Count -Descending | ForEach-Object {
        Write-Host ('  {0,4}  {1}' -f $_.Count, $_.Name)
    }
    Write-Host ''
}
$open = $rows | Where-Object { $_.Verdict -eq 'MISSING' }
if ($open.Count -gt 0) {
    Write-Host 'Files with unattached labels:'
    $open | Group-Object Path | Sort-Object Count -Descending | ForEach-Object {
        Write-Host ('  {0,4}  {1}' -f $_.Count, $_.Name)
    }
} else {
    Write-Host 'No unattached labels remain.'
}
