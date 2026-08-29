<#
    Audit-SoText.ps1   (Phase 5)

    Inventory of Korean text stored in ScriptableObject assets - the text that
    Phase 3 deliberately left alone because it lives in data, not in prefabs or
    code. This is the Phase 5 equivalent of ui-ledger.csv.

    Every row is one localizable field value:
        Type | Asset | StaticID | Field | Index | Text

    `Index` is the position inside a list field (blank for scalars), so a pool of
    dialogue lines yields one row per line.

    Field selection is explicit (see $SPEC below) rather than "anything Korean":
    a lot of Korean in these assets is not display text - the profanity list in
    NicknameConfig, regex patterns, editor-only labels.

    Output: out/so-ledger.csv + a summary on stdout.

    IMPORTANT: keep this file pure ASCII (comments included). Windows PowerShell
    5.1 reads BOM-less UTF-8 as ANSI, which silently corrupts non-ASCII literals.
#>
[CmdletBinding()]
param(
    [string]$Root   = '.',
    [string]$OutDir = 'Tools\Localization\out'
)

$ErrorActionPreference = 'Stop'

# Folder under Assets\_Projects\Data -> fields whose values are shown to players.
# `List` fields collect every '- value' line that follows the field name.
$SPEC = @(
    @{ Dir = 'Weapons';       Type = 'Weapon';       Scalars = @('weaponName','description') }
    @{ Dir = 'WeeklyQuests';  Type = 'Quest';        Scalars = @('questTitle','description','requirementText') }
    @{ Dir = 'Dungeons';      Type = 'Dungeon';      Scalars = @('dungeonName') }
    @{ Dir = 'Materials';     Type = 'Material';     Scalars = @('materialName','description') }
    @{ Dir = 'ActiveItems';   Type = 'ActiveItem';   Scalars = @('itemName','description') }
    @{ Dir = 'DungeonEvents'; Type = 'DungeonEvent'; Scalars = @('summary','description') }
    @{ Dir = 'VisitorEvents'; Type = 'VisitorEvent'; Scalars = @('eventName','description') }
    @{ Dir = 'Adventurers';   Type = 'Adventurer';   Scalars = @('adventurerName') }
    @{ Dir = 'Dialogue';      Type = 'Dialogue';     Scalars = @('dialogueName','defaultSpeakerName','speakerName','dialogueText','choiceText') }
    # Config has no flat Scalars/Lists - every one of its seven assets nests text
    # inside structs, so it goes through Read-ConfigRows / $CONFIG_SPEC below.
    @{ Dir = 'Config';        Type = 'Config' }
)

# Assets that hold Korean which is NOT display text.
$SKIP_ASSETS = @{
    'NicknameConfig.asset' = 'Korean profanity list + a regex pattern - Phase 6, not display text'
}

# "Type/field" pairs that are inside $SPEC but must not go into the Data table:
# either nothing renders them, or something else already localizes them. Listed
# here rather than deleted from $SPEC so the summary keeps stating the reason.
$SKIP_FIELDS = @{
    'Adventurer/adventurerName' = 'named adventurers resolve through the Names table (nameKey = Name_<StaticID>); the SO field is only the editor/save fallback copy'
    'VisitorEvent/description'  = 'editor annotation - the value is literally "<eventName> event" and has no runtime call site'
    'Dialogue/dialogueName'     = 'editor label for the asset itself - no runtime call site (only the field declaration references it)'
    'DungeonEvent/summary'      = 'no runtime read anywhere; the field holds the monster English name used as an Animator controller prefix, and the non-battle values are terse editor labels that duplicate description'
    'Quest/requirementText'     = 'baked by WeeklyQuestGenerator and rebuilt at runtime from 5 templates (UITranslator.QuestRequirementText) whose parts are already localized - translating the baked string would go stale the next time quests are regenerated'
    'Quest/description'         = 'baked by WeeklyQuestGenerator (header + the requirement list joined) and displayed nowhere'
    'Config/label'              = 'dead serialized data - MoodLabelEntry has no label field in C# any more, and MoodLabelCalculator returns only colours. Unity drops these lines the next time AdventureInfoConfig.asset is saved'
}

# ---------------------------------------------------------------- Config ----
#
# Config assets keep their text inside structs, so the flat scan collapses 374
# values onto 121 (Asset, Field, Index) triples - all seven assets collide. Same
# failure mode as Dialogue: several strings sharing one key means only one of
# them can ever show, and with fallback on the others print Korean, so nothing
# on screen looks broken.
#
# Numbering the structs by position is not a fix either. Reordering steps[] in
# the inspector would silently repoint every translation. So a struct list is
# addressed by the stable id it already carries, and the id replaces its array
# index in the key:
#
#     Key = Config_{Short}_{Index}      (Index is built below and IS the suffix)
#
#     TutorialConfig.steps[]              -> TutorialLineKey name  (key: 100 -> WeaponShopIntro)
#     BlacksmithConfig.blacksmithDataArray[] -> BlacksmithData.StaticID
#     LegacyConfig.upgrades[]             -> UpgradeKey name
#     AdventureInfoConfig.bonusVisuals[]  -> BonusType name
#
# PriceTierConfig.stepNotices[] and WeaponShopConfig.merchantGreetings[] carry no
# id, but there the position IS the meaning (week tier, fixed length tied to
# tierMaxWeeks), so the index is used and named after the field.
#
# StaticID is blank for Config assets (they do not derive from BaseData), so the
# short asset name goes in that column instead - check 9 rejects an empty one.
#
# IdEnum maps are read out of the C# sources at run time (Get-EnumMap) rather
# than copied here: a new enum member then needs no edit to this file, and an
# asset value with no matching member fails loudly instead of inventing a name.
$CONFIG_SPEC = @{
    'SeerConfig' = @{
        Short   = 'Seer'
        Lists   = @('greetingLines','commentBadLines','commentMinorLines','commentGoodLines','commentGreatLines',
                    'easterEggLines1','easterEggLines2','easterEggLines3',
                    'easterEggConsultPrefaceLines','easterEggFollowUpLines',
                    'revisitLines1','revisitLines2','revisitLines3')
        # AbstractLuckLines structs - 5 abstract levels x 4 luck levels x 5 lines.
        # These 100 values are exactly what the flat scan merged onto 20 keys.
        Structs = @{
            'descGreat'  = @('bad','minor','good','great')
            'descGood'   = @('bad','minor','good','great')
            'descNormal' = @('bad','minor','good','great')
            'descHarsh'  = @('bad','minor','good','great')
            'descGrim'   = @('bad','minor','good','great')
        }
    }
    'TutorialConfig' = @{
        Short = 'Tutorial'
        StructLists = @{
            'steps' = @{ Id = 'key'; IdEnum = 'TutorialLineKey'; IdSource = 'Data\Config\TutorialConfig.cs'
                         Lists = @('lines'); Scalars = @() }
        }
    }
    'BlacksmithConfig' = @{
        Short = 'Blacksmith'
        StructLists = @{
            'blacksmithDataArray' = @{ Id = 'StaticID'; IdEnum = $null
                                       Lists = @('greetingsOnSuccess'); Scalars = @('blacksmithName','greetingOnFailure') }
        }
    }
    'PriceTierConfig' = @{
        Short = 'PriceTier'
        StructLists = @{
            'stepNotices' = @{ Id = $null; IdEnum = $null
                               Lists = @('noticeLines','todayLines'); Scalars = @() }
        }
    }
    'LegacyConfig' = @{
        Short = 'Legacy'
        StructLists = @{
            'upgrades' = @{ Id = 'key'; IdEnum = 'UpgradeKey'; IdSource = 'Data\Config\LegacyConfig.cs'
                            Lists = @(); Scalars = @('description') }
        }
    }
    'WeaponShopConfig' = @{
        Short = 'WeaponShop'
        StructLists = @{
            'merchantGreetings' = @{ Id = $null; IdEnum = $null
                                     Lists = @('lines'); Scalars = @() }
        }
    }
    'AdventureInfoConfig' = @{
        Short = 'AdventureInfo'
        StructLists = @{
            'bonusVisuals' = @{ Id = 'bonusType'; IdEnum = 'BonusType'; IdSource = 'Data\Config\AdventureInfoConfig.cs'
                                Lists = @(); Scalars = @('displayName') }
            # Listed only so $SKIP_FIELDS['Config/label'] keeps counting and
            # restating why those 20 lines are dead. Drop this entry once Unity
            # has re-saved the asset and the lines are gone.
            'moodLabels'   = @{ Id = $null; IdEnum = $null
                                Lists = @(); Scalars = @('label') }
        }
    }
}

function Has-Hangul([string]$s) {
    if ($s -match '\\u[A-D][0-9A-Fa-f]{3}') {
        foreach ($m in [regex]::Matches($s, '\\u([0-9A-Fa-f]{4})')) {
            $cp = [int]('0x' + $m.Groups[1].Value)
            if ($cp -ge 0xAC00 -and $cp -le 0xD7A3) { return $true }
        }
    }
    foreach ($ch in $s.ToCharArray()) {
        $cp = [int]$ch
        if (($cp -ge 0xAC00 -and $cp -le 0xD7A3) -or ($cp -ge 0x3130 -and $cp -le 0x318F)) { return $true }
    }
    return $false
}

# A long value is folded across several lines. Reading only the first line
# truncates it - fine for counting, wrong for a translation export.
function Read-Folded([string[]]$lines, [int]$start, [string]$first) {
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

function Decode-Unicode([string]$s) {
    if ($null -eq $s) { return '' }
    [regex]::Replace($s, '\\u([0-9A-Fa-f]{4})', {
        param($m) [string][char][int]('0x' + $m.Groups[1].Value)
    })
}

$rows        = New-Object System.Collections.ArrayList
$skipped     = New-Object System.Collections.ArrayList
$skipFieldNo = @{}   # "Type/field" -> how many values were skipped

# Dialogue nests its text two levels deep (nodes[] -> choices[]), so the flat
# scalar scan hands every node of one asset the same Field with a blank Index -
# 9 groups of them, up to 5 strings sharing a key. Keys must carry the real
# node/choice position instead.
#
# Numbering by order of appearance is NOT good enough: a node whose text is
# empty or has no Korean never reaches the ledger, which shifts every later
# index by one and makes a different node's line show up at runtime. With
# fallback on, that reads as normal text - nothing flags it. So parse the
# structure and carry the true indices.
#
# Layout (indent is load-bearing):
#     staticID / dialogueName / defaultSpeakerName   2 spaces
#     nodes:
#     - nodeType: ...                                2 spaces + dash = new node
#       dialogueText / speakerName                   4 spaces
#       choices:
#       - choiceText: ...                            4 spaces + dash = new choice
#         nextNodeIndex ...                          6 spaces
# 'choices' is always the last field of a node, so "inside choices" stays true
# until the next node starts.
function Read-DialogueRows([string[]]$lines, [string]$staticId, [string]$assetName, [string]$relPath) {
    $out       = New-Object System.Collections.ArrayList
    $nodeIdx   = -1
    $choiceIdx = -1
    $inChoices = $false

    function New-Row($field, $index, $text) {
        [pscustomobject]@{
            Type = 'Dialogue'; Asset = $assetName; StaticID = $staticId
            Field = $field; Index = $index; Text = (Decode-Unicode $text).Trim('"')
            Path = $relPath
        }
    }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]

        if ($l -match '^  dialogueName:\s*(\S.*)$') {
            $r = Read-Folded $lines $i $matches[1].Trim(); $i = $r.End
            if (Has-Hangul $r.Value) { $script:skipFieldNo['Dialogue/dialogueName'] = [int]$script:skipFieldNo['Dialogue/dialogueName'] + 1 }
            continue
        }
        if ($l -match '^  defaultSpeakerName:\s*(\S.*)$') {
            $r = Read-Folded $lines $i $matches[1].Trim(); $i = $r.End
            if (Has-Hangul $r.Value) { [void]$out.Add((New-Row 'defaultSpeakerName' '' $r.Value)) }
            continue
        }
        if ($l -match '^  - nodeType:') {
            $nodeIdx++; $inChoices = $false; $choiceIdx = -1
            continue
        }
        if ($l -match '^    choices:\s*$')      { $inChoices = $true;  $choiceIdx = -1; continue }
        if ($l -match '^    choices:\s*\[\]')   { $inChoices = $false; continue }

        if ($inChoices -and $l -match '^    - choiceText:\s*(\S.*)$') {
            $choiceIdx++
            $r = Read-Folded $lines $i $matches[1].Trim(); $i = $r.End
            if (Has-Hangul $r.Value) { [void]$out.Add((New-Row 'choiceText' ("{0}_{1}" -f $nodeIdx, $choiceIdx) $r.Value)) }
            continue
        }
        if (-not $inChoices -and $l -match '^    (dialogueText|speakerName):\s*(\S.*)$') {
            $name = $matches[1]
            $r = Read-Folded $lines $i $matches[2].Trim(); $i = $r.End
            if (Has-Hangul $r.Value) { [void]$out.Add((New-Row $name $nodeIdx $r.Value)) }
            continue
        }
    }
    return $out
}

# Enum member -> name, read straight out of the C# so this file never carries a
# second copy of the mapping. Handles explicit values and implicit increments,
# and throws on anything it cannot parse rather than guessing.
$enumCache = @{}
function Get-EnumMap([string]$relCsPath, [string]$enumName) {
    $ck = $relCsPath + '|' + $enumName
    if ($enumCache.ContainsKey($ck)) { return $enumCache[$ck] }

    $full = Join-Path $script:Root ("Assets\_Projects\Scripts\" + $relCsPath)
    if (-not (Test-Path $full)) { throw "Get-EnumMap: $full not found (enum $enumName)" }
    $txt = [System.IO.File]::ReadAllText($full, [System.Text.Encoding]::UTF8)

    $m = [regex]::Match($txt, ('enum\s+' + [regex]::Escape($enumName) + '\s*\{([^}]*)\}'))
    if (-not $m.Success) { throw "Get-EnumMap: enum $enumName not found in $relCsPath" }

    $body = $m.Groups[1].Value
    $body = [regex]::Replace($body, '/\*.*?\*/', '', 'Singleline')
    $body = [regex]::Replace($body, '//[^\r\n]*', '')

    $map  = @{}
    $next = 0
    foreach ($part in ($body -split ',')) {
        $p = $part.Trim()
        if ($p -eq '') { continue }
        if ($p -match '^(\w+)\s*=\s*(-?\d+)$') {
            $v = [int]$matches[2]; $map[$v] = $matches[1]; $next = $v + 1
        } elseif ($p -match '^(\w+)$') {
            $map[$next] = $p; $next++
        } else {
            throw "Get-EnumMap: cannot parse member '$p' of $enumName in $relCsPath"
        }
    }
    $enumCache[$ck] = $map
    return $map
}

# How many values a Config asset *should* yield: every line that starts a value
# and contains Korean. Folded continuation lines do not start with '- ' or
# 'field:', so they are not counted twice. Compared against what the structured
# parser emitted - a mismatch means a field was added to the asset without being
# added to $CONFIG_SPEC, which would otherwise vanish without a sound.
function Measure-ConfigValues([string[]]$lines) {
    $n = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '^\s+-?\s*\w+:\s*(\S.*)$') {
            $r = Read-Folded $lines $i $matches[1].Trim(); $i = $r.End
            if (Has-Hangul $r.Value) { $n++ }
            continue
        }
        if ($l -match '^\s+-\s+(\S.*)$') {
            $r = Read-Folded $lines $i $matches[1].Trim(); $i = $r.End
            if (Has-Hangul $r.Value) { $n++ }
            continue
        }
    }
    return $n
}

# Config assets: root fields at indent 2, struct-list elements as "  - first:" with
# the rest of the struct at indent 4, and leaf string lists as "- value" at the
# same indent as their header. Index carries the whole key suffix (see $CONFIG_SPEC).
function Read-ConfigRows([string[]]$lines, [string]$assetName, [string]$relPath) {
    $out  = New-Object System.Collections.ArrayList
    $cs = $CONFIG_SPEC[$assetName]
    if ($null -eq $cs) { return $out }

    function New-CfgRow($field, $index, $text) {
        [pscustomobject]@{
            Type = 'Config'; Asset = $assetName; StaticID = $cs.Short
            Field = $field; Index = $index; Text = (Decode-Unicode $text).Trim('"')
            Path = $relPath
        }
    }
    function Add-CfgValue($field, $index, $text) {
        $fk = 'Config/' + $field
        if ($SKIP_FIELDS.ContainsKey($fk)) {
            $script:skipFieldNo[$fk] = [int]$script:skipFieldNo[$fk] + 1
            return
        }
        [void]$out.Add((New-CfgRow $field $index $text))
    }

    $mode      = ''      # '' | list | struct | structlist
    $sec      = ''      # current root field name
    $sl        = $null   # struct-list spec while $mode = structlist
    $structKey = ''      # id (or fieldName+n) of the current struct-list element
    $structN   = -1
    $leaf      = ''      # current leaf list field name
    $leafIdx   = 0

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]

        # -- root field header: "  name:" with nothing after it
        if ($l -match '^  (\w+):\s*$') {
            $sec = $matches[1]; $sl = $null; $structKey = ''; $structN = -1; $leaf = ''; $leafIdx = 0
            if     ($cs.Lists       -and $cs.Lists -contains $sec)          { $mode = 'list' }
            elseif ($cs.Structs     -and $cs.Structs.ContainsKey($sec))     { $mode = 'struct' }
            elseif ($cs.StructLists -and $cs.StructLists.ContainsKey($sec)) { $mode = 'structlist'; $sl = $cs.StructLists[$sec] }
            else                                                                 { $mode = '' }
            continue
        }
        # -- root scalar with a value: ends whatever section was open
        if ($l -match '^  \w+:\s*\S') { $mode = ''; $sl = $null; continue }

        # -- struct-list element starts: "  - firstField: value"
        if ($l -match '^  -\s+(\w+):\s*(.*)$') {
            if ($mode -ne 'structlist') { $mode = ''; continue }
            $structN++
            $idField = $matches[1]; $idRaw = $matches[2].Trim()
            $leaf = ''; $leafIdx = 0

            if ($null -eq $sl.Id) {
                $structKey = "$sec$structN"
            } else {
                if ($idField -ne $sl.Id) {
                    throw ("{0}: {1}[{2}] starts with '{3}' but the key field is '{4}' - the id must be the struct's first serialized field" -f $assetName, $sec, $structN, $idField, $sl.Id)
                }
                if ($null -eq $sl.IdEnum) {
                    $structKey = $idRaw
                } else {
                    $map = Get-EnumMap $sl.IdSource $sl.IdEnum
                    if (-not ($idRaw -match '^-?\d+$')) { throw "$assetName`: $sec[$structN].$idField = '$idRaw' is not an integer" }
                    $v = [int]$idRaw
                    if (-not $map.ContainsKey($v)) { throw "$assetName`: $sec[$structN].$idField = $v has no member in enum $($sl.IdEnum)" }
                    $structKey = $map[$v]
                }
            }
            # When the struct's only/first field is a list, the dash line IS that
            # list's header (WeaponShopConfig: "  - lines:"), so the items that
            # follow belong to it.
            if ($idRaw -eq '' -and ($sl.Lists -contains $idField)) {
                $leaf = $idField; $leafIdx = 0
                continue
            }
            # the id line itself can also be a translatable scalar (unlikely, but be exact)
            if ($sl.Scalars -contains $idField) {
                $r = Read-Folded $lines $i $idRaw; $i = $r.End
                if (Has-Hangul $r.Value) { Add-CfgValue $idField ("{0}_{1}" -f $structKey, $idField) $r.Value }
            }
            continue
        }

        # -- root scalar-string list item: "  - value"
        if ($l -match '^  -\s+(\S.*)$') {
            if ($mode -ne 'list') { continue }
            $r = Read-Folded $lines $i $matches[1].Trim(); $i = $r.End
            if (Has-Hangul $r.Value) { Add-CfgValue $sec ("{0}_{1}" -f $sec, $leafIdx) $r.Value }
            $leafIdx++
            continue
        }

        # -- inside a struct or a struct-list element (indent 4)
        if ($l -match '^    (\w+):\s*$') {
            $leaf = $matches[1]; $leafIdx = 0
            continue
        }
        if ($l -match '^    (\w+):\s*(\S.*)$') {
            $name = $matches[1]
            $r = Read-Folded $lines $i $matches[2].Trim(); $i = $r.End
            $leaf = ''
            if ($mode -eq 'structlist' -and ($sl.Scalars -contains $name) -and (Has-Hangul $r.Value)) {
                Add-CfgValue $name ("{0}_{1}" -f $structKey, $name) $r.Value
            }
            continue
        }
        if ($l -match '^    -\s+(\S.*)$') {
            $r = Read-Folded $lines $i $matches[1].Trim(); $i = $r.End
            $take = $false
            $idx  = ''
            if ($mode -eq 'struct'     -and ($cs.Structs[$sec] -contains $leaf)) {
                $take = $true; $idx = "{0}_{1}_{2}" -f $sec, $leaf, $leafIdx
            } elseif ($mode -eq 'structlist' -and ($sl.Lists -contains $leaf)) {
                $take = $true; $idx = "{0}_{1}_{2}" -f $structKey, $leaf, $leafIdx
            }
            if ($take -and (Has-Hangul $r.Value)) { Add-CfgValue $leaf $idx $r.Value }
            $leafIdx++
            continue
        }
    }
    return $out
}

foreach ($spec in $SPEC) {
    $dir = Join-Path $Root ("Assets\_Projects\Data\" + $spec.Dir)
    if (-not (Test-Path $dir)) { continue }

    Get-ChildItem $dir -Recurse -Filter '*.asset' | ForEach-Object {
        $file = $_
        if ($SKIP_ASSETS.ContainsKey($file.Name)) {
            [void]$skipped.Add([pscustomobject]@{ Asset = $file.Name; Reason = $SKIP_ASSETS[$file.Name] })
            return
        }

        $lines    = [System.IO.File]::ReadAllLines($file.FullName, [System.Text.Encoding]::UTF8)
        $staticId = ''
        foreach ($l in $lines) { if ($l -match '^\s+staticID:\s*(\S+)') { $staticId = $matches[1]; break } }

        if ($spec.Type -eq 'Dialogue') {
            $rel = $file.FullName.Substring($file.FullName.IndexOf('Assets'))
            foreach ($row in (Read-DialogueRows $lines $staticId $file.BaseName $rel)) { [void]$rows.Add($row) }
            return
        }

        if ($spec.Type -eq 'Config') {
            $rel      = $file.FullName.Substring($file.FullName.IndexOf('Assets'))
            $expected = Measure-ConfigValues $lines
            if (-not $CONFIG_SPEC.ContainsKey($file.BaseName)) {
                # An asset nobody has described yet is fine only while it holds no
                # Korean. The moment it does, say so instead of dropping it.
                if ($expected -gt 0) {
                    throw ("{0} holds {1} Korean value(s) but has no `$CONFIG_SPEC entry - add one (see the Config block at the top)" -f $file.Name, $expected)
                }
                return
            }
            $before = [int]($skipFieldNo.Values | Measure-Object -Sum).Sum
            $got    = Read-ConfigRows $lines $file.BaseName $rel
            $after  = [int]($skipFieldNo.Values | Measure-Object -Sum).Sum
            $seen   = $got.Count + ($after - $before)
            if ($seen -ne $expected) {
                throw ("{0}: structured parse saw {1} Korean value(s) but the asset has {2} - `$CONFIG_SPEC is out of date with the asset" -f $file.Name, $seen, $expected)
            }
            foreach ($row in $got) { [void]$rows.Add($row) }
            return
        }

        $curList = $null; $listIdx = 0
        for ($li = 0; $li -lt $lines.Count; $li++) {
            $l = $lines[$li]
            # scalar field. The optional '- ' prefix catches the first field of a
            # struct inside a list (e.g. "  - description: ..." in LegacyConfig).
            if ($l -match '^\s+-?\s*(\w+):\s*(\S.*)$') {
                $name = $matches[1]
                $r = Read-Folded $lines $li ($matches[2].Trim())
                $val = $r.Value; $li = $r.End
                $curList = $null
                if ($spec.Scalars -contains $name -and (Has-Hangul $val)) {
                    $fk = $spec.Type + '/' + $name
                    if ($SKIP_FIELDS.ContainsKey($fk)) {
                        $skipFieldNo[$fk] = [int]$skipFieldNo[$fk] + 1
                        continue
                    }
                    [void]$rows.Add([pscustomobject]@{
                        Type = $spec.Type; Asset = $file.BaseName; StaticID = $staticId
                        Field = $name; Index = ''; Text = (Decode-Unicode $val).Trim('"')
                        Path = ($file.FullName.Substring($file.FullName.IndexOf('Assets')))
                    })
                }
                continue
            }
            # list header
            if ($l -match '^\s+(\w+):\s*$') {
                $name = $matches[1]
                $curList = $(if ($spec.Lists -and ($spec.Lists -contains $name)) { $name } else { $null })
                $listIdx = 0
                continue
            }
            # list item
            if ($curList -and $l -match '^\s+-\s+(\S.*)$') {
                $r = Read-Folded $lines $li ($matches[1].Trim())
                $val = $r.Value; $li = $r.End
                if (Has-Hangul $val) {
                    [void]$rows.Add([pscustomobject]@{
                        Type = $spec.Type; Asset = $file.BaseName; StaticID = $staticId
                        Field = $curList; Index = $listIdx; Text = (Decode-Unicode $val).Trim('"')
                        Path = ($file.FullName.Substring($file.FullName.IndexOf('Assets')))
                    })
                }
                $listIdx++
                continue
            }
        }
    }
}

if (-not (Test-Path $OutDir)) { [void](New-Item -ItemType Directory -Path $OutDir -Force) }
$csv = Join-Path $OutDir 'so-ledger.csv'
$rows | Sort-Object Type, Asset, Field, Index | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8

Write-Host ''
Write-Host ('Ledger written: {0}  ({1} rows)' -f $csv, $rows.Count)
Write-Host ''
$rows | Group-Object Type | Sort-Object Count -Descending | ForEach-Object {
    Write-Host ('  {0,-14} {1,5}' -f $_.Name, $_.Count)
}
Write-Host ''
Write-Host ('  {0,-14} {1,5}' -f 'TOTAL', $rows.Count)
if ($skipped.Count -gt 0) {
    Write-Host ''
    Write-Host 'Skipped assets:'
    $skipped | ForEach-Object { Write-Host ('  {0} - {1}' -f $_.Asset, $_.Reason) }
}
if ($skipFieldNo.Count -gt 0) {
    Write-Host ''
    Write-Host 'Skipped fields:'
    foreach ($k in ($skipFieldNo.Keys | Sort-Object)) {
        Write-Host ('  {0} ({1}) - {2}' -f $k, $skipFieldNo[$k], $SKIP_FIELDS[$k])
    }
}
