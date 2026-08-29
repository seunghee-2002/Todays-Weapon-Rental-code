<#
    Audit-CodeText.ps1

    Builds a ledger of every Korean string literal in runtime C# code, split into
    "still to localize" and "deliberately excluded".

    It tokenizes each file rather than matching line by line, so it never mistakes
    a comment for code and never misses a literal that sits on the continuation
    line of a multi-line Debug.Log(...) or attribute.

    Output: out/code-ledger.csv  +  a summary on stdout.

    IMPORTANT: keep this file pure ASCII (comments included). Windows PowerShell
    5.1 reads BOM-less UTF-8 as ANSI, which silently corrupts non-ASCII literals.
#>
[CmdletBinding()]
param(
    [string]$Root   = '.',
    [string]$OutDir = 'Tools\Localization\out'
)

$ErrorActionPreference = 'Stop'

$scriptDir = Join-Path $Root 'Assets\_Projects\Scripts'

function Has-Korean([string]$s) {
    foreach ($ch in $s.ToCharArray()) {
        $cp = [int]$ch
        if (($cp -ge 0xAC00 -and $cp -le 0xD7A3) -or
            ($cp -ge 0x1100 -and $cp -le 0x11FF) -or
            ($cp -ge 0x3130 -and $cp -le 0x318F)) { return $true }
    }
    return $false
}

# Walks the file once and returns:
#   Literals - every string/char literal with its extent
#   Mask     - a copy of the text with comments and literal bodies blanked out,
#              so a backwards scan for statement boundaries only sees real code.
function Split-CSharp([string]$text) {
    $n    = $text.Length
    $mask = New-Object System.Text.StringBuilder $n
    $lits = New-Object System.Collections.ArrayList
    $i    = 0

    while ($i -lt $n) {
        $c  = $text[$i]
        $c2 = $(if ($i + 1 -lt $n) { $text[$i + 1] } else { [char]0 })

        if ($c -eq '/' -and $c2 -eq '/') {
            while ($i -lt $n -and $text[$i] -ne "`n") { [void]$mask.Append(' '); $i++ }
            continue
        }
        if ($c -eq '/' -and $c2 -eq '*') {
            [void]$mask.Append('  '); $i += 2
            while ($i -lt $n -and -not ($text[$i] -eq '*' -and $i + 1 -lt $n -and $text[$i + 1] -eq '/')) {
                [void]$mask.Append($(if ($text[$i] -eq "`n") { "`n" } else { ' ' })); $i++
            }
            if ($i -lt $n) { [void]$mask.Append('  '); $i += 2 }
            continue
        }

        # verbatim string: @"..."  or  $@"..."  or  @$"..."
        $verbStart = -1
        if ($c -eq '@' -and $c2 -eq '"') { $verbStart = $i; $skip = 2 }
        elseif (($c -eq '$' -or $c -eq '@') -and $c2 -eq '@' -and $i + 2 -lt $n -and $text[$i + 2] -eq '"') { $verbStart = $i; $skip = 3 }
        if ($verbStart -ge 0) {
            $start = $i
            for ($k = 0; $k -lt $skip; $k++) { [void]$mask.Append(' ') }
            $i += $skip
            $sb = New-Object System.Text.StringBuilder
            while ($i -lt $n) {
                if ($text[$i] -eq '"') {
                    if ($i + 1 -lt $n -and $text[$i + 1] -eq '"') {
                        [void]$sb.Append('"'); [void]$mask.Append('  '); $i += 2; continue
                    }
                    [void]$mask.Append(' '); $i++; break
                }
                [void]$sb.Append($text[$i])
                [void]$mask.Append($(if ($text[$i] -eq "`n") { "`n" } else { ' ' }))
                $i++
            }
            [void]$lits.Add(@{ Start = $start; Text = $sb.ToString() })
            continue
        }

        # regular string: "..."  or  $"..."
        if ($c -eq '"' -or ($c -eq '$' -and $c2 -eq '"')) {
            $start = $i
            if ($c -eq '$') { [void]$mask.Append(' '); $i++ }
            [void]$mask.Append(' '); $i++
            $sb = New-Object System.Text.StringBuilder
            while ($i -lt $n) {
                if ($text[$i] -eq '\' -and $i + 1 -lt $n) {
                    [void]$sb.Append($text[$i + 1]); [void]$mask.Append('  '); $i += 2; continue
                }
                if ($text[$i] -eq '"') { [void]$mask.Append(' '); $i++; break }
                if ($text[$i] -eq "`n") { [void]$mask.Append("`n"); $i++; break }   # unterminated guard
                [void]$sb.Append($text[$i]); [void]$mask.Append(' '); $i++
            }
            [void]$lits.Add(@{ Start = $start; Text = $sb.ToString() })
            continue
        }

        # char literal: '.'
        if ($c -eq "'") {
            [void]$mask.Append(' '); $i++
            while ($i -lt $n) {
                if ($text[$i] -eq '\' -and $i + 1 -lt $n) { [void]$mask.Append('  '); $i += 2; continue }
                if ($text[$i] -eq "'") { [void]$mask.Append(' '); $i++; break }
                [void]$mask.Append(' '); $i++
            }
            continue
        }

        [void]$mask.Append($c); $i++
    }

    return @{ Literals = $lits; Mask = $mask.ToString() }
}

# Nearest preceding statement boundary, so we can see what the literal is an
# argument to (Debug.Log, [Header], a method call, an assignment, ...).
function Get-StatementPrefix([string]$mask, [int]$pos) {
    $start = $pos
    while ($start -gt 0) {
        $ch = $mask[$start - 1]
        if ($ch -eq ';' -or $ch -eq '{' -or $ch -eq '}') { break }
        $start--
    }
    $len = $pos - $start
    if ($len -le 0) { return '' }
    return $mask.Substring($start, $len)
}

# True when the literal sits inside a still-open call to one of $namePattern.
# Matching the name anywhere in the statement is not enough: in
#   if (!EconomyManager.Instance.SpendGold(price, "reason")) return (false, "Not enough gold.");
# both literals share one statement, and only the first is a reason id.
function Test-InsideCall([string]$prefix, [string]$namePattern) {
    $ms = [regex]::Matches($prefix, '(?:' + $namePattern + ')\s*\(')
    if ($ms.Count -eq 0) { return $false }
    $last = $ms[$ms.Count - 1]
    $tail = $prefix.Substring($last.Index + $last.Length)
    # Depth, not "is there a )": SpendGold(GetRefreshCost(), "reason") has a
    # closing paren from the nested call while the outer call is still open.
    $depth = 1
    foreach ($ch in $tail.ToCharArray()) {
        if     ($ch -eq '(') { $depth++ }
        elseif ($ch -eq ')') { $depth--; if ($depth -le 0) { return $false } }
    }
    return $true
}

# ------------------------------------------------------------ exclusions ----

$exclusions = @{}
$exPath = Join-Path $Root 'Tools\Localization\code-exclusions.csv'
if (Test-Path $exPath) {
    # -Encoding UTF8 is mandatory: PowerShell 5.1 defaults to ANSI here and would
    # mangle the Korean literals used as lookup keys, silently matching nothing.
    Import-Csv $exPath -Encoding UTF8 | ForEach-Object { $exclusions[$_.Key] = $_.Reason }
}

# ------------------------------------------------------------- main scan ----

$rows  = New-Object System.Collections.ArrayList
$files = Get-ChildItem $scriptDir -Recurse -Filter '*.cs' |
         Where-Object { $_.FullName -notmatch '\\Editor\\' } | Sort-Object FullName

Write-Host ("Scanning {0} runtime C# files..." -f $files.Count)

foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName)
    if (-not (Has-Korean $text)) { continue }

    $parsed = Split-CSharp $text
    $mask   = $parsed.Mask
    $rel    = $f.FullName.Substring($f.FullName.IndexOf('Assets'))

    foreach ($lit in $parsed.Literals) {
        if (-not (Has-Korean $lit.Text)) { continue }

        $prefix = Get-StatementPrefix $mask $lit.Start
        $line   = ([regex]::Matches($text.Substring(0, $lit.Start), "`n")).Count + 1

        $status = 'TODO'
        $reason = ''

        # Log.Info/Warn/Error is this project's wrapper around UnityEngine.Debug.
        if     ($prefix -match '(?:Debug\s*\.\s*Log|(?<![\w.])Log\s*\.\s*(?:Info|Warn|Error|Assert))') { $status = 'EXCLUDED'; $reason = 'Debug log' }
        elseif ($prefix -match '\[\s*(Header|Tooltip|CreateAssetMenu|MenuItem|Space|SerializeField|ContextMenu|InspectorName)') { $status = 'EXCLUDED'; $reason = 'Editor attribute' }
        # Gold/reputation "reason" strings are internal ids: they only reach
        # Log.Info and the analytics source mapping, never a TMP.
        elseif (Test-InsideCall $prefix 'AddGold|SpendGold')     { $status = 'EXCLUDED'; $reason = 'Gold reason id (analytics/log only)' }
        elseif ($prefix -match 'reason\s*(?:==|!=)\s*$|reason\s*\.\s*(?:StartsWith|EndsWith|Contains)\s*\(\s*$') { $status = 'EXCLUDED'; $reason = 'Gold reason id comparison' }
        elseif (Test-InsideCall $prefix 'AddReputation')         { $status = 'EXCLUDED'; $reason = 'Reputation reason id (log only)' }

        $key = ($f.Name + '|' + $lit.Text)
        if ($exclusions.ContainsKey($key))            { $status = 'EXCLUDED'; $reason = $exclusions[$key] }
        elseif ($exclusions.ContainsKey($f.Name + '|*')) { $status = 'EXCLUDED'; $reason = $exclusions[$f.Name + '|*'] }

        $ctx = $prefix.Trim()
        if ($ctx.Length -gt 110) { $ctx = $ctx.Substring($ctx.Length - 110) }
        $ctx = ($ctx -replace '\s+', ' ')

        [void]$rows.Add([pscustomobject]@{
            File    = $f.Name
            Status  = $status
            Line    = $line
            Literal = $lit.Text
            Context = $ctx
            Reason  = $reason
            Path    = $rel
        })
    }
}

# --------------------------------------------------------------- output -----

if (-not (Test-Path $OutDir)) { [void](New-Item -ItemType Directory -Path $OutDir -Force) }
$csv = Join-Path $OutDir 'code-ledger.csv'
$rows | Sort-Object Status, Path, Line | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8

Write-Host ''
Write-Host ('Ledger written: {0}  ({1} rows)' -f $csv, $rows.Count)
Write-Host ''
$rows | Group-Object Status | Sort-Object Name | ForEach-Object {
    Write-Host ('  {0,-10} {1}' -f $_.Name, $_.Count)
}
Write-Host ''
$todo = $rows | Where-Object { $_.Status -eq 'TODO' }
if ($todo.Count -gt 0) {
    Write-Host 'Files with unlocalized literals:'
    $todo | Group-Object Path | Sort-Object Count -Descending | Select-Object -First 40 | ForEach-Object {
        Write-Host ('  {0,4}  {1}' -f $_.Count, $_.Name)
    }
} else {
    Write-Host 'No unlocalized literals remain.'
}
