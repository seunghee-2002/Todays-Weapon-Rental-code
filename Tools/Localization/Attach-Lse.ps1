<#
    Attach-Lse.ps1

    Attaches LocalizeStringEvent components to prefab/scene YAML, producing the
    same result as right-click > Localize in the editor.

    Input CSV (UTF-8 with BOM), columns:
        Path,Anchor,Collection,Key

      Path       : repo-relative prefab/scene path (the ledger's Path column)
      Anchor     : fileID of the TextMeshProUGUI component (the ledger's Anchor)
      Collection : string table collection name, e.g. UI_Screens
      Key        : entry key name, e.g. BGMChange_Title

    Idempotent: a TMP that already has a LocalizeStringEvent pointed at it is
    skipped. Objects belonging to a nested prefab instance are skipped loudly,
    since overrides need a different treatment.

    Usage:
        powershell -File Tools\Localization\Attach-Lse.ps1 -Csv <path> [-WhatIf]

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

$TMP_GUID = 'f4688fdb7df04437aeb418b961361dc5'
$LSE_GUID = '56eb0353ae6e5124bb35b17aff880f16'
$LOC_DIR  = Join-Path $Root 'Assets\_Projects\Data\Localization'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# ------------------------------------------- collection -> guid + key ids ----

$collections = @{}
Get-ChildItem $LOC_DIR -Recurse -Filter '*Shared Data.asset' | ForEach-Object {
    $name = $_.BaseName -replace ' Shared Data$',''
    $guid = (Select-String -Path ($_.FullName + '.meta') -Pattern '^guid: (\w+)').Matches[0].Groups[1].Value
    $lines = [System.IO.File]::ReadAllLines($_.FullName)
    $map = @{}   # key name -> id
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^  - m_Id: (\d+)\s*$') {
            $id = $matches[1]
            if ($lines[$i + 1] -match '^    m_Key: (\S+)\s*$') { $map[$matches[1]] = $id }
        }
    }
    $collections[$name] = @{ Guid = $guid; Keys = $map }
}

# ---------------------------------------------------------------- helpers ----

# A fileID must fit in Int64. Unity silently clamps anything larger to Int64.MaxValue,
# so two oversized anchors in one file collapse into the same id -> "Duplicate identifier"
# -> the whole prefab fails to import. Never build an anchor by concatenating two randoms.
function New-Anchor([hashtable]$used) {
    do {
        $v = [string](Get-Random -Minimum ([int64]1000000000000000000) -Maximum ([int64]9223372036854775000))
    } while ($used.ContainsKey($v))
    $used[$v] = $true
    return $v
}

function New-LseDocument([string]$anchor, [string]$goId, [string]$tableGuid, [string]$keyId, [string]$targetId) {
    # Unity writes empty string fields as "key: " - the trailing space is inside
    # the quotes on purpose. Do not "clean" it up; do not build it with + ' '
    # either, because PowerShell's comma binds tighter than + inside an array
    # literal and would split the space onto its own line.
    return @(
        "--- !u!114 &$anchor",
        'MonoBehaviour:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        "  m_GameObject: {fileID: $goId}",
        '  m_Enabled: 1',
        '  m_EditorHideFlags: 0',
        "  m_Script: {fileID: 11500000, guid: $LSE_GUID, type: 3}",
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        '  m_StringReference:',
        '    m_TableReference:',
        "      m_TableCollectionName: GUID:$tableGuid",
        '    m_TableEntryReference:',
        "      m_KeyId: $keyId",
        "      m_Key: ",
        '    m_FallbackState: 0',
        '    m_WaitForCompletion: 0',
        '    m_LocalVariables: []',
        '  m_FormatArguments: []',
        '  m_UpdateString:',
        '    m_PersistentCalls:',
        '      m_Calls:',
        "      - m_Target: {fileID: $targetId}",
        '        m_TargetAssemblyTypeName: TMPro.TextMeshProUGUI, Unity.TextMeshPro',
        '        m_MethodName: set_text',
        '        m_Mode: 0',
        '        m_Arguments:',
        '          m_ObjectArgument: {fileID: 0}',
        '          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine',
        '          m_IntArgument: 0',
        '          m_FloatArgument: 0',
        "          m_StringArgument: ",
        '          m_BoolArgument: 0',
        '        m_CallState: 1'
    )
}

# ------------------------------------------------------------------ main ----

# @() forces an array even for a single row, so .Count below is never $null
$rows = @(Import-Csv $Csv -Encoding UTF8)
if ($rows.Count -eq 0) { throw "no rows in $Csv" }

$attached = 0; $skipped = 0

foreach ($grp in ($rows | Group-Object Path)) {
    $path = Join-Path $Root $grp.Name
    if (-not (Test-Path $path)) { throw "file not found: $path" }

    # core.autocrlf is on, so the working tree holds a mix of CRLF and LF files.
    # Whichever this one uses, keep it - rewriting every line would bury the real
    # change in the diff.
    $raw   = [System.IO.File]::ReadAllText($path)
    $nl    = $(if ($raw -match "`r`n") { "`r`n" } else { "`n" })
    $lines = $raw -split "`r?`n"
    if ($lines.Count -gt 0 -and $lines[$lines.Count - 1] -eq '') {
        $lines = $lines[0..($lines.Count - 2)]
    }

    # index the file: anchors in use, TMP components, existing LSE targets,
    # and where each GameObject's m_Component list ends
    $used = @{}; $tmpInfo = @{}; $lseTargets = @{}; $goComponentEnd = @{}
    $cur = $null; $class = $null; $go = $null; $scr = $null; $prefabInst = $null; $target = $null
    $curGo = $null; $inComponents = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '^--- !u!(\d+) &(\d+)') {
            if ($class -eq '114' -and $scr -eq $TMP_GUID) { $tmpInfo[$cur] = @{ Go = $go; PrefabInstance = $prefabInst } }
            if ($class -eq '114' -and $scr -eq $LSE_GUID -and $target) { $lseTargets[$target] = $true }
            $class = $matches[1]; $cur = $matches[2]; $used[$cur] = $true
            $go = $null; $scr = $null; $prefabInst = $null; $target = $null
            $inComponents = $false
            $curGo = $(if ($class -eq '1') { $cur } else { $null })
            continue
        }
        if ($l -match '^  m_GameObject: \{fileID: (\d+)\}')      { $go = $matches[1]; continue }
        if ($l -match '^  m_Script:.*guid: (\w+)')               { $scr = $matches[1]; continue }
        if ($l -match '^  m_PrefabInstance: \{fileID: (\d+)\}')  { $prefabInst = $matches[1]; continue }
        if ($l -match '- m_Target: \{fileID: (\d+)\}')           { $target = $matches[1]; continue }
        if ($curGo -and $l -eq '  m_Component:')                 { $inComponents = $true; continue }
        if ($inComponents) {
            if ($l -match '^  - component: \{fileID: (\d+)\}') { $goComponentEnd[$curGo] = $i }
            else { $inComponents = $false }
        }
    }
    if ($class -eq '114' -and $scr -eq $TMP_GUID) { $tmpInfo[$cur] = @{ Go = $go; PrefabInstance = $prefabInst } }
    if ($class -eq '114' -and $scr -eq $LSE_GUID -and $target) { $lseTargets[$target] = $true }

    # build all insertions first, then apply from the bottom so indices hold
    $componentInserts = @()   # @{ At = lineIndex; Text = '  - component: {fileID: N}' }
    $newDocs = @()

    foreach ($r in $grp.Group) {
        $anchor = $r.Anchor
        if (-not $tmpInfo.ContainsKey($anchor)) {
            Write-Host ("  SKIP {0} anchor {1}: not a TextMeshProUGUI in this file" -f $grp.Name, $anchor); $script:skipped++; continue
        }
        if ($tmpInfo[$anchor].PrefabInstance -and $tmpInfo[$anchor].PrefabInstance -ne '0') {
            Write-Host ("  SKIP {0} anchor {1}: belongs to a nested prefab instance" -f $grp.Name, $anchor); $script:skipped++; continue
        }
        if ($lseTargets.ContainsKey($anchor)) {
            Write-Host ("  SKIP {0} anchor {1}: already localized" -f $grp.Name, $anchor); $script:skipped++; continue
        }
        if (-not $collections.ContainsKey($r.Collection)) { throw "unknown collection '$($r.Collection)'" }
        $keyId = $collections[$r.Collection].Keys[$r.Key]
        if (-not $keyId) { throw "key '$($r.Key)' not found in collection '$($r.Collection)'" }

        $goId = $tmpInfo[$anchor].Go
        if (-not $goComponentEnd.ContainsKey($goId)) { throw "no m_Component list found for GameObject $goId in $($grp.Name)" }

        $newAnchor = New-Anchor $used
        $componentInserts += @{ At = $goComponentEnd[$goId]; Text = "  - component: {fileID: $newAnchor}" }
        $newDocs += (New-LseDocument $newAnchor $goId $collections[$r.Collection].Guid $keyId $anchor)
        $script:attached++
    }

    if ($componentInserts.Count -eq 0) { continue }

    $out = New-Object System.Collections.ArrayList
    [void]$out.AddRange($lines)
    foreach ($ins in ($componentInserts | Sort-Object { $_.At } -Descending)) {
        $out.Insert($ins.At + 1, $ins.Text)
    }
    # the trailing empty line from ReadAllLines is not present; append docs at the end
    foreach ($d in $newDocs) { [void]$out.Add($d) }

    if ($WhatIf) {
        Write-Host ("[dry run] {0}: would attach {1}" -f $grp.Name, $componentInserts.Count)
    } else {
        [System.IO.File]::WriteAllText($path, (($out.ToArray() -join $nl) + $nl), $utf8NoBom)
        Write-Host ("{0}: attached {1}" -f $grp.Name, $componentInserts.Count)
    }
}

Write-Host ''
Write-Host ("attached {0}, skipped {1}" -f $attached, $skipped)
if ($attached -ne ($rows.Count - $skipped)) { throw 'attach count does not match the input; investigate before continuing' }
