<#
    Apply-CodeEdits.ps1

    Applies a batch of exact string replacements to C# files, refusing to write
    anything unless every replacement matched exactly the expected number of
    times. Used to swap Korean literals for table lookups in bulk.

    The edit list is a separate .ps1 data file that dot-sources into $edits:
        $edits = @(
            @{ F = 'Assets\...\Foo.cs'; Old = '"...."'; New = 'L("Key")' },
            @{ F = 'Assets\...\Foo.cs'; Old = '...'; New = '...'; Times = 2 },
            @{ F = 'Assets\...\Foo.cs'; Using = 'UnityEngine.Localization.Settings' }
        )

    The repeat count key is `Times`, not `Count` - a hashtable already has a
    Count property (its number of keys), which would silently shadow it.

    Run `dotnet build Assembly-CSharp.csproj` afterwards - it compiles this
    project in a few seconds and is the only real check that the result is valid.

    Usage:
        powershell -File Tools\Localization\Apply-CodeEdits.ps1 -EditsFile <data.ps1> [-WhatIf]

    The parameter is -EditsFile, not -Edits, on purpose: PowerShell variables are
    case-insensitive, so a [string]$Edits parameter would type-constrain the
    $edits the data file defines and silently flatten the array into a string.

    NOTE: the DATA file will contain Korean, so it must be saved as UTF-8 *with
    BOM* - Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI and would corrupt
    the literals, matching nothing. This file itself stays pure ASCII.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EditsFile,
    [string]$Root = '.',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

. (Resolve-Path $EditsFile).Path
if (-not $edits) { throw "the data file did not define \$edits" }

# group by file so each file is read once and written once.
# Group-Object needs a script block here: it cannot see hashtable keys as properties.
$byFile = $edits | Group-Object { $_.F }

$plans = @{}
$newlines = @{}
foreach ($g in $byFile) {
    $path = Join-Path $Root $g.Name
    if (-not (Test-Path $path)) { throw "file not found: $path" }
    $raw  = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    # Match on LF-normalized text so a multi-line Old written in this data file
    # still matches a CRLF source file; the original newline is restored on write.
    $newlines[$path] = $(if ($raw -match "`r`n") { "`r`n" } else { "`n" })
    $text = $raw -replace "`r`n", "`n"

    foreach ($e in $g.Group) {
        # { F = '...'; Using = 'UnityEngine.Localization.Settings' } inserts the
        # directive after the last existing using, matching the file's newline.
        if ($e.Using) {
            $nl = "`n"
            $line = "using $($e.Using);"
            if ($text -match [regex]::Escape($line)) { continue }
            $ms = [regex]::Matches($text, '(?m)^using [^\r\n]+;')
            if ($ms.Count -eq 0) { throw "$($g.Name): no using directives to anchor on" }
            $last = $ms[$ms.Count - 1]
            $at = $last.Index + $last.Length
            $text = $text.Substring(0, $at) + $nl + $line + $text.Substring($at)
            continue
        }

        $want = $(if ($e.ContainsKey('Times')) { [int]$e.Times } else { 1 })
        $old  = ($e.Old -replace "`r`n", "`n")
        $new  = ($e.New -replace "`r`n", "`n")
        $have = ([regex]::Matches($text, [regex]::Escape($old))).Count
        if ($have -ne $want) {
            throw ("{0}: expected {1} occurrence(s) of [{2}], found {3}" -f $g.Name, $want, $e.Old, $have)
        }
        $text = $text.Replace($old, $new)
    }
    $plans[$path] = $text
}

foreach ($g in $byFile) {
    $path = Join-Path $Root $g.Name
    if ($WhatIf) {
        Write-Host ("[dry run] {0}: {1} replacement(s)" -f $g.Name, $g.Group.Count)
    } else {
        $outText = $plans[$path]
        if ($newlines[$path] -eq "`r`n") { $outText = $outText -replace "`n", "`r`n" }
        [System.IO.File]::WriteAllText($path, $outText, $utf8NoBom)
        Write-Host ("{0}: {1} replacement(s)" -f $g.Name, $g.Group.Count)
    }
}
Write-Host ''
Write-Host ("{0} file(s), {1} edit(s). Now run: dotnet build Assembly-CSharp.csproj" -f @($byFile).Count, @($edits).Count)
