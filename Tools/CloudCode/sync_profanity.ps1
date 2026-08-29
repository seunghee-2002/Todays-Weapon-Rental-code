# sync_profanity.ps1
# Pushes the nickname rules from NicknameConfig.asset into changeNickname.js, so the
# client check and the server check cannot disagree. Four values are synced:
#   MIN_LENGTH / MAX_LENGTH / ALLOWED_PATTERN / PROFANITY_WORDS
# Run this whenever any of them changes on the client, then redeploy changeNickname.js.
#
# Why it matters: the client rejects locally and the server rejects again. If the two
# drift, either the client blocks a nickname the server would accept, or the client
# lets one through and the player gets an opaque server error - and nothing tests it.
#
# Usage: powershell -ExecutionPolicy Bypass -File Tools\CloudCode\sync_profanity.ps1
#        add -Check to verify without writing (non-zero exit if out of sync).
# NOTE: ASCII-only comments on purpose - Windows PowerShell 5.1 misreads BOM-less UTF-8 scripts.
[CmdletBinding()]
param([switch]$Check)

$repoRoot  = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$assetPath = Join-Path $repoRoot "Assets\_Projects\Data\Config\NicknameConfig.asset"
$jsPath    = Join-Path $PSScriptRoot "changeNickname.js"

if (-not (Test-Path $assetPath)) { Write-Error "asset not found: $assetPath"; exit 1 }
if (-not (Test-Path $jsPath))    { Write-Error "script not found: $jsPath"; exit 1 }

# Unity writes non-ASCII in these assets as \uXXXX YAML escapes, so any scalar we
# lift out has to be decoded the same way the profanity words are.
function Decode-YamlScalar([string]$val) {
    if ($val.StartsWith('"') -and $val.EndsWith('"')) {
        $inner   = $val.Substring(1, $val.Length - 2).Replace('\"', '"')
        $decoded = [System.Text.RegularExpressions.Regex]::Replace($inner, '\\u([0-9A-Fa-f]{4})', {
            param($m) [string][char]([Convert]::ToInt32($m.Groups[1].Value, 16))
        })
        return [System.Text.RegularExpressions.Regex]::Replace($decoded, '\\x([0-9A-Fa-f]{2})', {
            param($m) [string][char]([Convert]::ToInt32($m.Groups[1].Value, 16))
        })
    }
    if ($val.StartsWith("'") -and $val.EndsWith("'")) {
        return $val.Substring(1, $val.Length - 2).Replace("''", "'")
    }
    return $val.Trim()
}

# 1) Parse profanityWords entries out of the Unity YAML asset.
$lines  = [System.IO.File]::ReadAllLines($assetPath)
$inList = $false
$words  = New-Object System.Collections.Generic.List[string]

foreach ($line in $lines) {
    if (-not $inList) {
        if ($line -match 'profanityWords:') { $inList = $true }
        continue
    }

    if ($line -match '^\s*-\s(.*)$') {
        $val = $Matches[1]

        if ($val.StartsWith('"') -and $val.EndsWith('"')) {
            # YAML double-quoted scalar: decode \uXXXX / \xXX escapes
            $inner   = $val.Substring(1, $val.Length - 2).Replace('\"', '"')
            $decoded = [System.Text.RegularExpressions.Regex]::Replace($inner, '\\u([0-9A-Fa-f]{4})', {
                param($m) [string][char]([Convert]::ToInt32($m.Groups[1].Value, 16))
            })
            $decoded = [System.Text.RegularExpressions.Regex]::Replace($decoded, '\\x([0-9A-Fa-f]{2})', {
                param($m) [string][char]([Convert]::ToInt32($m.Groups[1].Value, 16))
            })
            $words.Add($decoded)
        }
        elseif ($val.StartsWith("'") -and $val.EndsWith("'")) {
            $words.Add($val.Substring(1, $val.Length - 2).Replace("''", "'"))
        }
        else {
            $words.Add($val.Trim())
        }
    }
    elseif ($line -match '^\S') {
        break
    }
}

# Ordinal on purpose - do NOT use Sort-Object here. Sort-Object compares by the
# current culture, and Windows PowerShell 5.1 (NLS) orders these words differently
# from PowerShell 7 / CI (ICU): 3818 of 5033 entries land in different positions.
# The generated line would then differ between a local run and the CI -Check, and
# the deploy job fails with "OUT OF SYNC" even though no rule actually drifted.
# Order carries no meaning - changeNickname.js only scans the array with indexOf.
$set = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
foreach ($w in $words) { [void]$set.Add($w) }
$unique = New-Object 'string[]' $set.Count
$set.CopyTo($unique)
[Array]::Sort($unique, [System.StringComparer]::Ordinal)

if ($unique.Count -eq 0) { Write-Error "no profanity words parsed - aborting"; exit 1 }

$json = ($unique | ForEach-Object { '"' + ($_.Replace('\', '\\').Replace('"', '\"')) + '"' }) -join ','

# 1-B) The three scalar rules. These live next to profanityWords in the same asset.
$scalars = @{}
foreach ($line in $lines) {
    if ($line -match '^\s{2}(minLength|maxLength|allowedPattern):\s*(\S.*)$') {
        $scalars[$Matches[1]] = (Decode-YamlScalar $Matches[2].Trim())
    }
}
foreach ($k in @('minLength','maxLength','allowedPattern')) {
    if (-not $scalars.ContainsKey($k)) { Write-Error "could not read $k from NicknameConfig.asset"; exit 1 }
}

# 2) Replace the generated lines in changeNickname.js.
$js      = [System.IO.File]::ReadAllText($jsPath)
# core.autocrlf=true checks this .js out with CRLF, and in .NET multiline mode $
# matches only before the \n - so a pattern ending at '];$' never matches a CRLF
# file and this script aborts with "line not found" on a normal Windows clone.
# The line end is a LOOKAHEAD, not part of the match: consuming the \r would drop
# it from the replacement and leave one lone LF line in an otherwise CRLF file.
$before = $js

# name -> (regex that matches the whole line, replacement line)
$edits = @(
    @{ Name = 'MIN_LENGTH'
       Pattern = '(?m)^const MIN_LENGTH\s*=.*?;(?=\r?$)'
       Line    = 'const MIN_LENGTH      = ' + $scalars['minLength'] + ';' }
    @{ Name = 'MAX_LENGTH'
       Pattern = '(?m)^const MAX_LENGTH\s*=.*?;(?=\r?$)'
       Line    = 'const MAX_LENGTH      = ' + $scalars['maxLength'] + ';' }
    @{ Name = 'ALLOWED_PATTERN'
       Pattern = '(?m)^const ALLOWED_PATTERN\s*=.*?;(?=\r?$)'
       Line    = 'const ALLOWED_PATTERN = /' + $scalars['allowedPattern'] + '/;' }
    @{ Name = 'PROFANITY_WORDS'
       Pattern = '(?m)^const PROFANITY_WORDS = \[.*\];(?=\r?$)'
       Line    = 'const PROFANITY_WORDS = [' + $json + '];' }
)

foreach ($e in $edits) {
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($js, $e.Pattern)) {
        Write-Error ("const " + $e.Name + " line not found in changeNickname.js"); exit 1
    }
    # $ in the replacement is a substitution token to .NET Regex - double it.
    $js = [System.Text.RegularExpressions.Regex]::Replace($js, $e.Pattern, $e.Line.Replace('$', '$$'))
}

if ($Check) {
    if ($js -ne $before) {
        Write-Error "changeNickname.js is OUT OF SYNC with NicknameConfig.asset - run this script without -Check"
        exit 1
    }
    Write-Output ("in sync: minLength=" + $scalars['minLength'] + " maxLength=" + $scalars['maxLength'] +
                  " words=" + $unique.Count)
    exit 0
}

# Unity assets and this .js are checked out with CRLF here; ReadAllText/WriteAllText
# round-trip whatever was already in the file, so nothing is renormalised.
[System.IO.File]::WriteAllText($jsPath, $js, (New-Object System.Text.UTF8Encoding($false)))

$changed = if ($js -ne $before) { "updated" } else { "already in sync" }
Write-Output ("synced words=" + $unique.Count + ", minLength=" + $scalars['minLength'] +
              ", maxLength=" + $scalars['maxLength'] + " (" + $changed + "), script size=" +
              (Get-Item $jsPath).Length + " bytes")
