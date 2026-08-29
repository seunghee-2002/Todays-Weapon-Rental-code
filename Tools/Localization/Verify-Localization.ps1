<#
    Verify-Localization.ps1

    Integrity checks that must pass before any batch is reported as done.
    Everything here catches a failure mode that the fallback would otherwise
    hide: with UseFallback on, a missing translation silently renders Korean.

      1  table id consistency  - shared data and all 4 locale tables agree
      2  empty translations    - id present but value blank
      3  LSE reference health  - unknown table / unknown key / bad target
      4  code key existence    - every key the code looks up really exists
      5  prefab YAML health    - duplicate anchors, components not registered
      6  data-stored keys      - fields that hold a table key, not literal text
      7  untranslated values   - Korean left sitting in en / ja / zh-Hans
      8  collection wiring     - editor collection asset + addressables entries
      9  SO display keys       - Phase 5 keys built from StaticID still resolve

    Exit code is non-zero when any check fails, so this can gate a batch.

    IMPORTANT: keep this file pure ASCII (comments included). Windows PowerShell
    5.1 reads BOM-less UTF-8 as ANSI, which silently corrupts non-ASCII literals.
#>
[CmdletBinding()]
param(
    [string]$Root = '.'
)

$ErrorActionPreference = 'Stop'

$TMP_GUID = 'f4688fdb7df04437aeb418b961361dc5'
$LSE_GUID = '56eb0353ae6e5124bb35b17aff880f16'
$LOC_DIR  = Join-Path $Root 'Assets\_Projects\Data\Localization'
$failures = 0

# Some serialized fields hold a table KEY rather than the text itself, so the
# inspector can keep listing entries while the code resolves them at runtime
# (the adventurer name pools and BGM group titles work this way). A typo here
# shows up as "No translation found" on screen, so check them mechanically.
$KEY_FIELDS = @(
    @{ Path = 'Assets\Scenes\MainMenuScene.unity'; Field = 'displayName'; Table = 'UI_Screens' }
)

function Fail([string]$msg) { $script:failures++; Write-Host ('  FAIL ' + $msg) }
function Ok([string]$msg)   { Write-Host ('  ok   ' + $msg) }

# ------------------------------------------- 1 & 2  table id consistency ----

Write-Host ''
Write-Host '[1] Table id consistency + empty translations'

$collections = Get-ChildItem (Join-Path $LOC_DIR 'Tables') -Directory -ErrorAction SilentlyContinue
foreach ($col in $collections) {
    $name   = $col.Name
    $shared = Join-Path $col.FullName ("$name Shared Data.asset")
    if (-not (Test-Path $shared)) { Fail "$name : shared data asset missing"; continue }

    $sharedIds = @(Select-String -Path $shared -Pattern '^  - m_Id: (\d+)' -AllMatches |
                   ForEach-Object { $_.Matches[0].Groups[1].Value })

    $locales = @('ko-KR','en','ja','zh-Hans')
    $problem = $false
    foreach ($loc in $locales) {
        $lf = Join-Path $col.FullName ("${name}_$loc.asset")
        if (-not (Test-Path $lf)) { Fail "$name : locale table ${loc} missing"; $problem = $true; continue }

        $ids = @(Select-String -Path $lf -Pattern '^  - m_Id: (\d+)' -AllMatches |
                 ForEach-Object { $_.Matches[0].Groups[1].Value })
        $diff = Compare-Object $sharedIds $ids
        if ($null -ne $diff) {
            $problem = $true
            $only = ($diff | Where-Object { $_.SideIndicator -eq '<=' }).InputObject
            $extra= ($diff | Where-Object { $_.SideIndicator -eq '=>' }).InputObject
            Fail ("{0}/{1}: id mismatch (missing {2}, extra {3})" -f $name, $loc, (@($only).Count), (@($extra).Count))
            if (@($only).Count -gt 0)  { Write-Host ('         missing ids: ' + ((@($only)  | Select-Object -First 20) -join ', ')) }
            if (@($extra).Count -gt 0) { Write-Host ('         extra ids  : ' + ((@($extra) | Select-Object -First 20) -join ', ')) }
        }

        # empty values: "m_Localized:" with nothing after it
        $blank = @(Select-String -Path $lf -Pattern '^    m_Localized:\s*$')
        if ($blank.Count -gt 0) {
            $problem = $true
            Fail ("{0}/{1}: {2} empty translation value(s), first at line {3}" -f $name, $loc, $blank.Count, $blank[0].LineNumber)
        }
    }
    if (-not $problem) { Ok ("{0}: {1} keys x 4 locales" -f $name, $sharedIds.Count) }
}

# ---------------------------------------------- 3  LSE reference health ----

Write-Host ''
Write-Host '[3] LocalizeStringEvent reference health'

$tables = @{}
Get-ChildItem $LOC_DIR -Recurse -Filter '*Shared Data.asset' | ForEach-Object {
    $meta = $_.FullName + '.meta'
    if (-not (Test-Path $meta)) { return }
    $g = (Select-String -Path $meta -Pattern '^guid: (\w+)').Matches[0].Groups[1].Value
    $ids = @{}
    Select-String -Path $_.FullName -Pattern '^  - m_Id: (\d+)' -AllMatches |
        ForEach-Object { $ids[$_.Matches[0].Groups[1].Value] = $true }
    $tables[$g] = @{ Name = $_.BaseName; Ids = $ids }
}

$lseTotal = 0; $lseBad = 0
Get-ChildItem (Join-Path $Root 'Assets\_Projects\Prefabs'), (Join-Path $Root 'Assets\Scenes') -Recurse -Include *.prefab,*.unity | ForEach-Object {
    $f = $_
    $lines = [System.IO.File]::ReadAllLines($f.FullName)

    $tmpGo = @{}
    $cur = $null; $go = $null; $scr = $null
    foreach ($l in $lines) {
        if ($l -match '^--- !u!') {
            if ($scr -eq $TMP_GUID -and $go) { $tmpGo[$cur] = $go }
            $cur = $(if ($l -match '^--- !u!114 &(\d+)') { $matches[1] } else { $null }); $go = $null; $scr = $null
        }
        elseif ($l -match '^  m_GameObject: \{fileID: (\d+)\}') { $go = $matches[1] }
        elseif ($l -match '^  m_Script:.*guid: (\w+)')          { $scr = $matches[1] }
    }
    if ($scr -eq $TMP_GUID -and $go) { $tmpGo[$cur] = $go }

    $cur = $null; $go = $null; $scr = $null; $tbl = $null; $key = $null; $tgt = $null
    $check = {
        if ($scr -eq $LSE_GUID) {
            $script:lseTotal++
            $p = @()
            if (-not $tbl -or -not $tables.ContainsKey($tbl))       { $p += 'unknown table' }
            elseif (-not $tables[$tbl].Ids.ContainsKey($key))       { $p += "keyId $key not in $($tables[$tbl].Name)" }
            if (-not $tgt -or -not $tmpGo.ContainsKey($tgt))        { $p += 'target is not a TMP in this file' }
            elseif ($tmpGo[$tgt] -ne $go)                           { $p += 'target sits on a different GameObject' }
            if ($p.Count) { $script:lseBad++; Write-Host ('  FAIL {0} anchor={1}: {2}' -f $f.Name, $cur, ($p -join ', ')) }
        }
    }
    foreach ($l in $lines) {
        if ($l -match '^--- !u!') {
            & $check
            $cur = $(if ($l -match '^--- !u!114 &(\d+)') { $matches[1] } else { $null })
            $go = $null; $scr = $null; $tbl = $null; $key = $null; $tgt = $null
        }
        elseif ($l -match '^  m_GameObject: \{fileID: (\d+)\}')   { $go  = $matches[1] }
        elseif ($l -match '^  m_Script:.*guid: (\w+)')            { $scr = $matches[1] }
        elseif ($l -match 'm_TableCollectionName: GUID:(\w+)')    { $tbl = $matches[1] }
        elseif ($l -match '^      m_KeyId: (\d+)')                { $key = $matches[1] }
        elseif ($l -match '- m_Target: \{fileID: (\d+)\}')        { $tgt = $matches[1] }
    }
    & $check
}
if ($lseBad -gt 0) { $failures += $lseBad } else { Ok ("{0} LocalizeStringEvent components, all valid" -f $lseTotal) }

# ---------------------------------------------- 4  code key existence ----

Write-Host ''
Write-Host '[4] Keys referenced from code exist in the tables'

$keyNames = @{}
Get-ChildItem $LOC_DIR -Recurse -Filter '*Shared Data.asset' | ForEach-Object {
    Select-String -Path $_.FullName -Pattern '^    m_Key: (\S+)' -AllMatches |
        ForEach-Object { $keyNames[$_.Matches[0].Groups[1].Value] = $true }
}

# Four call shapes appear in this codebase, and a check that only knows the
# first one silently passes on everything else:
#   1  GetLocalizedString("UI_Screens", "Key")
#   2  GetLocalizedString("UI_Common", cond ? "KeyA" : "KeyB")
#   3  L("Key") / M("Key") / LCommon("Key") / LArg("Key", ...) - per-file helpers
#   4  L(cond ? "KeyA" : "KeyB")
$patterns = @(
    [regex]'GetLocalizedString\(\s*"\w+"\s*,\s*"(\w+)"',
    [regex]'GetLocalizedString\(\s*"\w+"\s*,[^;)]{0,120}?\?\s*"(\w+)"\s*:\s*"(\w+)"',
    [regex]'(?<![\w.])(?:L|M|LCommon|LArg|LScreens|LCommonKey|WeekMessage)\s*\(\s*"(\w+)"',
    [regex]'(?<![\w.])(?:L|M|LCommon)\s*\([^;)]{0,120}?\?\s*"(\w+)"\s*:\s*"(\w+)"'
)
$checked = 0; $missing = 0
Get-ChildItem (Join-Path $Root 'Assets\_Projects\Scripts') -Recurse -Filter '*.cs' | ForEach-Object {
    $file = $_
    $txt = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($rx in $patterns) {
        foreach ($m in $rx.Matches($txt)) {
            for ($g = 1; $g -lt $m.Groups.Count; $g++) {
                $k = $m.Groups[$g].Value
                if (-not $k) { continue }
                $script:checked++
                if (-not $keyNames.ContainsKey($k)) {
                    $script:missing++
                    Write-Host ('  FAIL missing key "{0}" referenced by {1}' -f $k, $file.Name)
                }
            }
        }
    }
}
if ($missing -gt 0) { $failures += $missing } else { Ok ("{0} key references, none missing" -f $checked) }

# ---------------------------------------------- 5  prefab YAML health ----

Write-Host ''
Write-Host '[5] Prefab/scene YAML health (anchor range, duplicate anchors, unregistered components)'

# An anchor above Int64.MaxValue looks fine in the file but Unity clamps it on import,
# so several of them turn into ONE id and the asset dies with "Duplicate identifier".
# The duplicate check below cannot see that - the text is still unique - so test the
# range separately. This is the whole reason ConfirmPopup.prefab stopped loading.
$INT64_MAX = [decimal]9223372036854775807

$yamlBad = 0
Get-ChildItem (Join-Path $Root 'Assets\_Projects\Prefabs'), (Join-Path $Root 'Assets\Scenes') -Recurse -Include *.prefab,*.unity | ForEach-Object {
    $lines = [System.IO.File]::ReadAllLines($_.FullName)
    $anchors = @{}; $dup = @(); $oversized = @(); $lse = @(); $cur = $null
    foreach ($l in $lines) {
        if ($l -match '^--- !u!\d+ &(\d+)') {
            if ($anchors.ContainsKey($matches[1])) { $dup += $matches[1] }
            if ([decimal]$matches[1] -gt $INT64_MAX) { $oversized += $matches[1] }
            $anchors[$matches[1]] = $true
            $cur = $(if ($l -match '^--- !u!114 &(\d+)') { $matches[1] } else { $null })
        }
        elseif ($l -match ('guid: ' + $LSE_GUID)) { if ($cur) { $lse += $cur } }
    }
    $reg = @{}
    foreach ($l in $lines) { if ($l -match '^  - component: \{fileID: (\d+)\}') { $reg[$matches[1]] = $true } }
    $unreg = @($lse | Where-Object { -not $reg.ContainsKey($_) })
    if ($oversized.Count -gt 0) { $script:yamlBad++; Write-Host ('  FAIL {0}: anchors exceed Int64 (Unity will clamp and collide): {1}' -f $_.Name, ($oversized -join ', ')) }
    if ($dup.Count -gt 0)   { $script:yamlBad++; Write-Host ('  FAIL {0}: duplicate anchors {1}' -f $_.Name, ($dup -join ', ')) }
    if ($unreg.Count -gt 0) { $script:yamlBad++; Write-Host ('  FAIL {0}: LSE not registered on any GameObject: {1}' -f $_.Name, ($unreg -join ', ')) }
}
if ($yamlBad -gt 0) { $failures += $yamlBad } else { Ok 'anchors in range, no duplicates, no orphan components' }

# ---------------------------------------------- 6  data-stored key names ----

Write-Host ''
Write-Host '[6] Keys stored in data files exist in their table'

$keysByTable = @{}
Get-ChildItem $LOC_DIR -Recurse -Filter '*Shared Data.asset' | ForEach-Object {
    $name = $_.BaseName -replace ' Shared Data$',''
    $set = @{}
    Select-String -Path $_.FullName -Pattern '^    m_Key: (\S+)' -AllMatches |
        ForEach-Object { $set[$_.Matches[0].Groups[1].Value] = $true }
    $keysByTable[$name] = $set
}

$dataBad = 0; $dataChecked = 0
foreach ($spec in $KEY_FIELDS) {
    $fp = Join-Path $Root $spec.Path
    if (-not (Test-Path $fp)) { Fail ("{0}: file not found" -f $spec.Path); continue }
    if (-not $keysByTable.ContainsKey($spec.Table)) { Fail ("unknown table '{0}'" -f $spec.Table); continue }
    foreach ($m in (Select-String -Path $fp -Pattern ("^\s*" + [regex]::Escape($spec.Field) + ":\s*(\S+)\s*$") -AllMatches)) {
        $v = $m.Matches[0].Groups[1].Value
        $dataChecked++
        if (-not $keysByTable[$spec.Table].ContainsKey($v)) {
            $dataBad++
            Write-Host ('  FAIL {0} line {1}: {2} "{3}" is not a key in {4}' -f
                        (Split-Path $spec.Path -Leaf), $m.LineNumber, $spec.Field, $v, $spec.Table)
        }
    }
}
if ($dataBad -gt 0) { $failures += $dataBad } else { Ok ("{0} data-stored key(s), all resolve" -f $dataChecked) }

# ------------------------------------------- 7  untranslated locale values ----

Write-Host ''
Write-Host '[7] en / ja / zh-Hans values that still contain Korean'

function Has-Hangul([string]$s) {
    foreach ($ch in $s.ToCharArray()) {
        $cp = [int]$ch
        if (($cp -ge 0xAC00 -and $cp -le 0xD7A3) -or
            ($cp -ge 0x1100 -and $cp -le 0x11FF) -or
            ($cp -ge 0x3130 -and $cp -le 0x318F)) { return $true }
    }
    return $false
}

$untranslated = 0
foreach ($col in $collections) {
    foreach ($loc in @('en','ja','zh-Hans')) {
        $lf = Join-Path $col.FullName ("$($col.Name)_$loc.asset")
        if (-not (Test-Path $lf)) { continue }
        $lines = [System.IO.File]::ReadAllLines($lf, [System.Text.Encoding]::UTF8)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^  - m_Id: (\d+)\s*$' -and $lines[$i + 1] -match '^    m_Localized: (.*)$') {
                if (Has-Hangul $matches[1]) {
                    $untranslated++
                    if ($untranslated -le 20) {
                        Write-Host ('  FAIL {0}/{1} line {2}: {3}' -f $col.Name, $loc, ($i + 2), $lines[$i + 1].Trim())
                    }
                }
            }
        }
    }
}
if ($untranslated -gt 0) {
    $failures += $untranslated
    if ($untranslated -gt 20) { Write-Host ('  ... and {0} more' -f ($untranslated - 20)) }
} else {
    Ok 'no Korean left in the translated locales'
}

# --------------------------------------------- 8  collection asset wiring ----

# A collection has two halves that fail independently and silently:
#
#   editor   "<Name>.asset" (StringTableCollection) is what the Localization
#            Tables window enumerates. Editor-only, never an Addressables
#            entry. Missing -> tables translate fine at runtime but nobody can
#            open them to edit. This is how Data/Names/Dialogue shipped at
#            first, and only Data got noticed.
#
#   runtime  the 4 locale tables and Shared Data must be Addressables entries.
#            Missing -> works in the editor, absent from the device build.
#
# Neither shows up in checks 1-7, so both are asserted here.

Write-Host ''
Write-Host '[8] Collection asset wiring (editor window + addressables)'

$COLLECTION_SCRIPT_GUID = '5be51871efa6c3e4eae1703925c8f5ac'
$groupDir = Join-Path $Root 'Assets\AddressableAssetsData\AssetGroups'
$localeGroupFile = @{
    'ko-KR'   = 'Localization-String-Tables-Korean (South Korea) (ko-KR).asset'
    'en'      = 'Localization-String-Tables-English (en).asset'
    'ja'      = 'Localization-String-Tables-Japanese (ja).asset'
    'zh-Hans' = 'Localization-String-Tables-Chinese (Simplified) (zh-Hans).asset'
}

function Get-AssetGuid([string]$assetPath) {
    $m = $assetPath + '.meta'
    if (-not (Test-Path $m)) { return $null }
    $hit = Select-String -Path $m -Pattern '^guid: ([0-9a-f]{32})$' | Select-Object -First 1
    if (-not $hit) { return $null }
    return $hit.Matches[0].Groups[1].Value
}

# guids registered in each addressables group, read once
$registered = @{}
foreach ($k in @($localeGroupFile.Keys) + @('shared')) {
    $p = Join-Path $groupDir $(if ($k -eq 'shared') { 'Localization-Assets-Shared.asset' } else { $localeGroupFile[$k] })
    if (-not (Test-Path $p)) { Fail "addressables group missing: $p"; $registered[$k] = @(); continue }
    $registered[$k] = @(Select-String -Path $p -Pattern '^  - m_GUID: ([0-9a-f]{32})' -AllMatches |
                        ForEach-Object { $_.Matches[0].Groups[1].Value })
}

foreach ($col in $collections) {
    $name = $col.Name
    $problem = $false

    $collectionPath = Join-Path $col.FullName "$name.asset"
    if (-not (Test-Path $collectionPath)) {
        Fail "$name : ${name}.asset (StringTableCollection) missing - the Localization Tables window will not list this collection"
        $problem = $true
    } else {
        $body = [System.IO.File]::ReadAllText($collectionPath, [System.Text.Encoding]::UTF8)
        if ($body -notmatch [regex]::Escape("guid: $COLLECTION_SCRIPT_GUID")) {
            Fail "$name : ${name}.asset is not a StringTableCollection (m_Script guid mismatch)"; $problem = $true
        }
        if ($body -notmatch ('(?m)^  m_Name: ' + [regex]::Escape($name) + '\s*$')) {
            Fail "$name : ${name}.asset m_Name does not match the folder"; $problem = $true
        }

        $sharedGuid = Get-AssetGuid (Join-Path $col.FullName "$name Shared Data.asset")
        if ($sharedGuid -and $body -notmatch ('m_SharedTableData: \{fileID: 11400000, guid: ' + $sharedGuid)) {
            Fail "$name : ${name}.asset points at the wrong Shared Data"; $problem = $true
        }
        foreach ($loc in @('ko-KR','en','ja','zh-Hans')) {
            $g = Get-AssetGuid (Join-Path $col.FullName "${name}_$loc.asset")
            if ($g -and $body -notmatch [regex]::Escape($g)) {
                Fail "$name : ${name}.asset m_Tables is missing $loc"; $problem = $true
            }
        }
    }

    # addressables: locale tables in their locale group, shared data in the shared group
    foreach ($loc in @('ko-KR','en','ja','zh-Hans')) {
        $g = Get-AssetGuid (Join-Path $col.FullName "${name}_$loc.asset")
        if ($g -and $registered[$loc] -notcontains $g) {
            Fail "$name/${loc}: not registered in Addressables - loads in the editor, missing from a build"
            $problem = $true
        }
    }
    $sg = Get-AssetGuid (Join-Path $col.FullName "$name Shared Data.asset")
    if ($sg -and $registered['shared'] -notcontains $sg) {
        Fail "$name : Shared Data not registered in Localization-Assets-Shared"; $problem = $true
    }

    if (-not $problem) { Ok ("{0}: collection asset ok, 4 tables + shared registered" -f $name) }
}

# ------------------------------------------- 9  SO display key coverage ----

# Phase 5 keys are built at runtime from the SO's StaticID
# (DataLocalizer.Key -> "Dungeon_DGN_COM_001_Name"). Nothing in C# spells them
# out, so check 4 cannot see them. Rename a StaticID and every language quietly
# falls back to the Korean field - no error, no missing-translation marker.
# So: every ledger row of a migrated type must resolve to a real key.

Write-Host ''
Write-Host '[9] SO text keys (Phase 5 types) resolve in the Data table'

# Types whose call sites now go through DataLocalizer. The value is the set of
# fields that were migrated; the key itself is built by Get-SoKey below.
$MIGRATED = @{
    'Weapon'       = @('weaponName','description')
    'Dungeon'      = @('dungeonName')
    'Material'     = @('materialName','description')
    'ActiveItem'   = @('itemName','description')
    'VisitorEvent' = @('eventName')
    'DungeonEvent' = @('description')
    'Quest'        = @('questTitle')
    'Dialogue'     = @('defaultSpeakerName','dialogueText','speakerName','choiceText')
    # Config is the one type gated by ASSET, not by field - see the loop below.
    # Its leaf field names repeat across assets ('lines' is both
    # TutorialConfig.steps[].lines and WeaponShopConfig.merchantGreetings[].lines),
    # so a field list would drag unfinished assets into the check. These are the
    # short asset names, i.e. what Audit-SoText.ps1 puts in the StaticID column.
    'Config'       = @('Seer','Tutorial','Blacksmith','PriceTier','Legacy','WeaponShop','AdventureInfo')
}

# Mirror of DataLocalizer's key rules. These two must stay in step by hand -
# nothing can prove the C# and this agree, so keep both rules small and obvious.
# (A divergence shows up as Korean on screen, which is why the F11 pass at the
# end of a batch is not optional.)
function Get-SoKey([string]$type, [string]$staticId, [string]$field, [string]$index) {
    # Config first: its assets have no StaticID (the short asset name is put in
    # that column instead) and the same leaf field name repeats across assets -
    # LegacyConfig's 'description' must not fall through to the generic _Desc
    # rule below. Audit-SoText.ps1's Read-ConfigRows already built the whole
    # stable path into Index, so the key is just the two pieces joined.
    if ($type -eq 'Config') { return "Config_${staticId}_${index}" }

    switch ($field) {
        'weaponName'         { return "Weapon_${staticId}_Name" }
        'dungeonName'        { return "Dungeon_${staticId}_Name" }
        'materialName'       { return "Material_${staticId}_Name" }
        'itemName'           { return "ActiveItem_${staticId}_Name" }
        'eventName'          { return "VisitorEvent_${staticId}_Name" }
        'questTitle'         { return "Quest_${staticId}_Title" }
        'description'        { return "${type}_${staticId}_Desc" }
        'defaultSpeakerName' { return "Dialogue_${staticId}_DefaultSpeaker" }
        'dialogueText'       { return "Dialogue_${staticId}_Node${index}_Text" }
        'speakerName'        { return "Dialogue_${staticId}_Node${index}_Speaker" }
        'choiceText'         {
            $p = $index -split '_'
            if ($p.Count -ne 2) { return $null }
            return "Dialogue_${staticId}_Node$($p[0])_Choice$($p[1])"
        }
    }
    return $null
}

$ledgerPath = Join-Path $Root 'Tools\Localization\out\so-ledger.csv'
if (-not (Test-Path $ledgerPath)) {
    Fail "so-ledger.csv not found - run Audit-SoText.ps1 first, this check needs it"
} else {
    $dataShared = Join-Path $LOC_DIR 'Tables\Data\Data Shared Data.asset'
    $dataKeys = @{}
    if (Test-Path $dataShared) {
        Select-String -Path $dataShared -Pattern '^    m_Key: (\S+)' -AllMatches |
            ForEach-Object { $dataKeys[$_.Matches[0].Groups[1].Value] = $true }
    }

    $checked = 0
    $missing = 0
    $seenKeys = @{}
    foreach ($row in (Import-Csv $ledgerPath)) {
        $allowed = $MIGRATED[$row.Type]
        if ($null -eq $allowed) { continue }
        # Config is sliced per asset (StaticID holds the short asset name);
        # every other type is sliced per field.
        $slice = $(if ($row.Type -eq 'Config') { $row.StaticID } else { $row.Field })
        if ($allowed -notcontains $slice) { continue }
        $checked++
        if ([string]::IsNullOrWhiteSpace($row.StaticID)) {
            $missing++
            if ($missing -le 15) { Fail ("{0}/{1}: StaticID is empty, so its key cannot be built" -f $row.Type, $row.Asset) }
            continue
        }
        $key = Get-SoKey $row.Type $row.StaticID $row.Field $row.Index
        if ($null -eq $key) {
            $missing++
            if ($missing -le 15) { Fail ("{0}: cannot build a key for {1} (index '{2}')" -f $row.Asset, $row.Field, $row.Index) }
            continue
        }
        # Two ledger values landing on one key means one of them can never show.
        if ($seenKeys.ContainsKey($key)) {
            $missing++
            if ($missing -le 15) { Fail ("{0}: key collision - {1} is produced by two different values" -f $row.Asset, $key) }
            continue
        }
        $seenKeys[$key] = $true
        if (-not $dataKeys.ContainsKey($key)) {
            $missing++
            if ($missing -le 15) { Fail ("{0}: no Data entry for {1} ({2})" -f $row.Asset, $key, $row.Field) }
        }
    }
    if ($missing -gt 0) {
        $failures += $missing
        if ($missing -gt 15) { Write-Host ('  ... and {0} more' -f ($missing - 15)) }
    } else {
        Ok ("{0} SO value(s) across {1} migrated type(s), all keys resolve" -f $checked, $MIGRATED.Count)
    }
}

# ------------------------------------------------------------- summary ----

Write-Host ''
if ($failures -eq 0) {
    Write-Host 'ALL CHECKS PASSED'
    exit 0
} else {
    Write-Host ("{0} PROBLEM(S) FOUND" -f $failures)
    exit 1
}
