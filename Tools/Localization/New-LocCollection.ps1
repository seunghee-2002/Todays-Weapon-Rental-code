<#
    New-LocCollection.ps1

    Creates a String Table Collection the way Unity would: six assets
    (Shared Data + ko-KR / en / ja / zh-Hans + the StringTableCollection
    itself), their .meta files with fresh GUIDs, and the Addressables entries
    that make them loadable in a build.

    Two halves that fail independently, which is the whole reason this script
    exists:

      runtime  - the four locale tables and Shared Data must be registered in
                 Addressables. UI_Messages was once created by hand without
                 that and worked in the editor while being completely missing
                 from the device build. Invisible until you ship.

      editor   - "<Name>.asset" (StringTableCollection) is what the
                 Localization Tables window enumerates. It is editor-only and
                 NOT an Addressables entry. Without it the tables load and
                 translate perfectly at runtime but the window shows nothing,
                 so no one can edit them. Data / Names / Dialogue were all
                 born this way before this script emitted it.

    Usage:
        powershell -File Tools\Localization\New-LocCollection.ps1 -Name Data [-WhatIf]

    IMPORTANT: keep this file pure ASCII (comments included). Windows PowerShell
    5.1 reads BOM-less UTF-8 as ANSI, which silently corrupts non-ASCII literals.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$Root = '.',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$SHARED_SCRIPT_GUID     = '5b11a58205ec3474ca216360e9fa74a8'   # SharedTableData
$LOCALE_SCRIPT_GUID     = 'e9620f8c34305754d8cc9a7e49e852d9'   # StringTable
$COLLECTION_SCRIPT_GUID = '5be51871efa6c3e4eae1703925c8f5ac'   # StringTableCollection (editor-only)
$LOCALES = @('ko-KR','en','ja','zh-Hans')

$tablesDir = Join-Path $Root 'Assets\_Projects\Data\Localization\Tables'
$dir       = Join-Path $tablesDir $Name
if (Test-Path $dir) { throw "collection folder already exists: $dir" }

function New-Guid32 { ([guid]::NewGuid().ToString('N')) }

function New-Meta([string]$guid) {
@"
fileFormatVersion: 2
guid: $guid
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"@
}

$sharedGuid     = New-Guid32
$collectionGuid = New-Guid32
$localeGuid     = @{}
foreach ($loc in $LOCALES) { $localeGuid[$loc] = New-Guid32 }

# ------------------------------------------------------------ asset bodies ----

$sharedBody = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $SHARED_SCRIPT_GUID, type: 3}
  m_Name: $Name Shared Data
  m_EditorClassIdentifier:
  m_TableCollectionName: $Name
  m_TableCollectionNameGuidString: $sharedGuid
  m_Entries: []
  m_Metadata:
    m_Items: []
  m_KeyGenerator:
    rid: 1
  references:
    version: 2
    RefIds:
    - rid: 1
      type: {class: DistributedUIDGenerator, ns: UnityEngine.Localization.Tables, asm: Unity.Localization}
      data:
        m_CustomEpoch: 1785715200000
"@

function New-LocaleBody([string]$loc) {
@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $LOCALE_SCRIPT_GUID, type: 3}
  m_Name: ${Name}_$loc
  m_EditorClassIdentifier:
  m_LocaleId:
    m_Code: $loc
  m_SharedData: {fileID: 11400000, guid: $sharedGuid, type: 2}
  m_Metadata:
    m_Items:
    - rid: 100
  m_TableData: []
  references:
    version: 2
    RefIds:
    - rid: 100
      type: {class: SmartFormatTag, ns: UnityEngine.Localization.Metadata, asm: Unity.Localization}
      data:
        m_Entries: []
        m_SharedEntries: []
"@
}

# The editor-side index. m_Tables is listed in $LOCALES order, which is how
# Unity writes it for the collections it created itself.
function New-CollectionBody {
    $rows = ($LOCALES | ForEach-Object { "  - {fileID: 11400000, guid: $($localeGuid[$_]), type: 2}" }) -join "`n"
@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $COLLECTION_SCRIPT_GUID, type: 3}
  m_Name: $Name
  m_EditorClassIdentifier:
  m_SharedTableData: {fileID: 11400000, guid: $sharedGuid, type: 2}
  m_Tables:
$rows
  m_Extensions: []
  m_Group: String Table
  references:
    version: 2
    RefIds: []
"@
}

# ------------------------------------------------------- addressables edits ----

# Adds one entry to an Addressables group asset, just before the group's own
# trailing "  m_ReadOnly:" line (the last entry is the insertion anchor).
function Add-GroupEntry([string]$groupPath, [string]$guid, [string]$address, [string[]]$labels) {
    $raw   = [System.IO.File]::ReadAllText($groupPath, [System.Text.Encoding]::UTF8)
    if ($raw -match [regex]::Escape("m_Address: $address")) { return $false }
    $nl    = $(if ($raw -match "`r`n") { "`r`n" } else { "`n" })
    $lines = $raw -split "`r?`n"

    $at = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -eq '  m_ReadOnly: 1' -or $lines[$i] -eq '  m_ReadOnly: 0') { $at = $i; break }
    }
    if ($at -lt 0) { throw "$groupPath : could not find the group's m_ReadOnly line" }

    $block = @(
        "  - m_GUID: $guid",
        "    m_Address: $address",
        '    m_ReadOnly: 1'
    )
    if ($labels.Count -gt 0) {
        $block += '    m_SerializedLabels:'
        foreach ($lb in $labels) { $block += "    - $lb" }
    } else {
        $block += '    m_SerializedLabels: []'
    }
    $block += '    FlaggedDuringContentUpdateRestriction: 0'

    $out = New-Object System.Collections.ArrayList
    [void]$out.AddRange($lines[0..($at - 1)])
    foreach ($b in $block) { [void]$out.Add($b) }
    [void]$out.AddRange($lines[$at..($lines.Count - 1)])
    [System.IO.File]::WriteAllText($groupPath, (($out.ToArray() -join $nl)), $utf8NoBom)
    return $true
}

# ------------------------------------------------------------------ apply ----

$groupDir = Join-Path $Root 'Assets\AddressableAssetsData\AssetGroups'
$localeGroup = @{
    'ko-KR'   = Join-Path $groupDir 'Localization-String-Tables-Korean (South Korea) (ko-KR).asset'
    'en'      = Join-Path $groupDir 'Localization-String-Tables-English (en).asset'
    'ja'      = Join-Path $groupDir 'Localization-String-Tables-Japanese (ja).asset'
    'zh-Hans' = Join-Path $groupDir 'Localization-String-Tables-Chinese (Simplified) (zh-Hans).asset'
}
foreach ($g in $localeGroup.Values) { if (-not (Test-Path $g)) { throw "addressables group not found: $g" } }
$sharedGroup = Join-Path $groupDir 'Localization-Assets-Shared.asset'
if (-not (Test-Path $sharedGroup)) { throw "addressables group not found: $sharedGroup" }

if ($WhatIf) {
    Write-Host ("[dry run] would create {0} with 6 assets + 6 metas" -f $dir)
    Write-Host ("[dry run] shared guid     {0}" -f $sharedGuid)
    Write-Host ("[dry run] collection guid {0}  (editor-only, no addressables entry)" -f $collectionGuid)
    foreach ($loc in $LOCALES) { Write-Host ("[dry run]   {0}_{1}  guid {2}" -f $Name, $loc, $localeGuid[$loc]) }
    Write-Host '[dry run] would register 4 locale entries (labels: Locale-<code>, Preload) + 1 shared entry'
    return
}

[void](New-Item -ItemType Directory -Path $dir -Force)

$sharedPath = Join-Path $dir "$Name Shared Data.asset"
[System.IO.File]::WriteAllText($sharedPath, ($sharedBody -replace "`r`n", "`n") + "`n", $utf8NoBom)
[System.IO.File]::WriteAllText($sharedPath + '.meta', ((New-Meta $sharedGuid) -replace "`r`n", "`n") + "`n", $utf8NoBom)

foreach ($loc in $LOCALES) {
    $p = Join-Path $dir "${Name}_$loc.asset"
    [System.IO.File]::WriteAllText($p, ((New-LocaleBody $loc) -replace "`r`n", "`n") + "`n", $utf8NoBom)
    [System.IO.File]::WriteAllText($p + '.meta', ((New-Meta $localeGuid[$loc]) -replace "`r`n", "`n") + "`n", $utf8NoBom)
}

# Editor-only, deliberately not an Addressables entry.
$collectionPath = Join-Path $dir "$Name.asset"
[System.IO.File]::WriteAllText($collectionPath, ((New-CollectionBody) -replace "`r`n", "`n") + "`n", $utf8NoBom)
[System.IO.File]::WriteAllText($collectionPath + '.meta', ((New-Meta $collectionGuid) -replace "`r`n", "`n") + "`n", $utf8NoBom)

foreach ($loc in $LOCALES) {
    $ok = Add-GroupEntry $localeGroup[$loc] $localeGuid[$loc] "${Name}_$loc" @("Locale-$loc", 'Preload')
    Write-Host ("  addressables {0,-8} {1}" -f $loc, $(if ($ok) { 'registered' } else { 'already present' }))
}
$rel = "Assets/_Projects/Data/Localization/Tables/$Name/$Name Shared Data.asset"
$ok = Add-GroupEntry $sharedGroup $sharedGuid $rel @()
Write-Host ("  addressables shared   {0}" -f $(if ($ok) { 'registered' } else { 'already present' }))

Write-Host ''
Write-Host ("collection '{0}' created. Shared GUID: {1}" -f $Name, $sharedGuid)
Write-Host 'Open Unity once so it imports the assets, then add entries with Add-LocEntries.ps1.'
