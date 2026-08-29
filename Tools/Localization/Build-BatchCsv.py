"""
Build-BatchCsv.py

Turns a translation file into an Add-LocEntries.ps1 input CSV for a Phase 5 batch.

    python Tools/Localization/Build-BatchCsv.py <Type> <translations.tsv> <firstId> <out.csv>
                                                [--asset <AssetName>]

The point of this script is that **ko is never retyped**. The translator file
carries only en/ja/zhHans; the Korean is taken from out/so-ledger.csv. If ko
drifts by one character the entry silently stops matching the SO, and with
fallback on nobody can see it on screen - so the join is checked both ways and
any mismatch is a hard error, never a quiet skip.

Two key shapes, because the SOs have two shapes:

  flat types (Weapon, Dungeon, Material, ActiveItem, VisitorEvent, DungeonEvent,
  Quest) - one text per (StaticID, Field), so the key is
  {Type}_{StaticID}_{suffix} and the TSV is keyed by (StaticID, Field):

      StaticID<TAB>Field<TAB>en<TAB>ja<TAB>zhHans

  Config - no StaticID at all, and the text sits inside structs, so the ledger's
  Index column carries the whole stable path (built by Audit-SoText.ps1's
  Read-ConfigRows) and the key is simply Config_{Short}_{Index}. The TSV is
  keyed by that finished key, which also cross-checks the two against each
  other:

      Key<TAB>en<TAB>ja<TAB>zhHans

  --asset restricts the ledger slice to one .asset, so a Config batch can be
  done one asset at a time instead of all 374 rows at once.

Checks performed before anything is written:
  - every ledger row in the slice has a translation           (missing -> error)
  - every translation row matches a ledger row                (typo    -> error)
  - {PLACEHOLDER} multiset identical to ko in all 3 locales   ({0} and {current} both)
  - rich-text tag multiset identical to ko                    (<b>, <color=...>, ...)
  - backslash-escape multiset identical to ko                 (\\n vs \\\\n really differ)
  - no blank values, no duplicate keys

Why those three multiset checks and not counts: SeerConfig stores a literal
two-character \\n that SeerConfig.PickRandom converts to a newline, while
DialogueData stores a real newline - a plain "same number of \\n" test passes
when one of them is written the other way round. Same for tags: TutorialConfig
lines carry 37 <b> pairs that a <color= only check never looks at.

Python (not PowerShell) on purpose: Windows PowerShell 5.1 reads BOM-less UTF-8
as ANSI, which mangles CJK. The output is written UTF-8 *with* BOM because that
is what Add-LocEntries.ps1 expects.

Dialogue (node/choice indices) is not handled here; it was built separately.
"""
import collections
import csv
import io
import re
import sys

LEDGER = 'Tools/Localization/out/so-ledger.csv'

# SO field name -> key suffix, for the flat types. Mirrors DataLocalizer; keep
# the two in step.
SUFFIX = {
    'weaponName': 'Name', 'dungeonName': 'Name', 'materialName': 'Name',
    'itemName': 'Name', 'eventName': 'Name', 'questTitle': 'Title',
    'description': 'Desc', 'requirementText': 'Req',
}

# {0}, {current}, {ADVENTURER_NAME} - digits included, or Seer's cost slot is
# never checked.
PLACEHOLDER = re.compile(r'\{[A-Za-z_0-9]+\}')
# <b>, </b>, <color=#ff0000>, <size=120%>. The attribute group is NON-capturing
# on purpose: re.findall returns the groups when there are any, so a capturing
# group here would compare only the '=value' part and rate <b> equal to <i>.
RICHTAG = re.compile(r'</?[a-zA-Z][a-zA-Z0-9]*(?:=[^>]*)?>')
# \n, \t, \" - what the YAML actually carries, backslash count included.
ESCAPE = re.compile(r'\\+[a-zA-Z"]')


def shape(s):
    """The parts of a string a translation must reproduce exactly."""
    return (
        tuple(sorted(PLACEHOLDER.findall(s))),
        tuple(sorted(RICHTAG.findall(s))),
        tuple(sorted(ESCAPE.findall(s))),
        # Raw brace counts, on top of the placeholder set above. A lone '{' is
        # not a placeholder and so slips past that check, but SeerManager runs
        # string.Format over the greeting - a stray brace is a FormatException
        # at runtime, in one locale only.
        (s.count('{'), s.count('}')),
    )


SHAPE_NAMES = ('placeholder', 'rich-text tag', 'backslash escape', 'brace count')


def main():
    argv = sys.argv[1:]
    asset = None
    if '--asset' in argv:
        i = argv.index('--asset')
        asset = argv[i + 1]
        del argv[i:i + 2]
    if len(argv) != 4:
        sys.exit(__doc__)
    so_type, trans_path, first_id, out_path = argv[0], argv[1], int(argv[2]), argv[3]

    ledger = [r for r in csv.DictReader(io.open(LEDGER, encoding='utf-8-sig'))
              if r['Type'] == so_type and (asset is None or r['Asset'] == asset)]
    if not ledger:
        sys.exit('no ledger rows of type %r%s - run Audit-SoText.ps1 first'
                 % (so_type, '' if asset is None else ' in asset %r' % asset))

    is_config = so_type == 'Config'
    if is_config:
        def key_of(r):
            return 'Config_%s_%s' % (r['StaticID'], r['Index'])
        join_col, join_desc = 'Key', 'Key'
        ledger.sort(key=lambda r: (r['Asset'], r['Index']))
    else:
        def key_of(r):
            return '%s_%s_%s' % (so_type, r['StaticID'], SUFFIX[r['Field']])
        join_col, join_desc = None, '(StaticID, Field)'
        ledger.sort(key=lambda r: (r['StaticID'], SUFFIX[r['Field']] != 'Name'))

    def join_of(r):
        return key_of(r) if is_config else (r['StaticID'], r['Field'])

    trans = {}
    reader = csv.DictReader(io.open(trans_path, encoding='utf-8'), delimiter='\t')
    for r in reader:
        if is_config:
            if join_col not in r:
                sys.exit('%s: Config translation files need a %r column, got %r'
                         % (trans_path, join_col, list(r.keys())))
            k = r[join_col].strip()
        else:
            k = (r['StaticID'], r['Field'])
        if k in trans:
            sys.exit('duplicate translation row: %s' % (k,))
        trans[k] = r

    errors, seen = [], set()
    for r in ledger:
        k = join_of(r)
        seen.add(k)
        if k not in trans:
            errors.append('no translation for %s %s' % (join_desc, k))
    for k in trans:
        if k not in seen:
            errors.append('translation has no ledger row (typo in %s?): %s' % (join_desc, k))
    fail(errors)

    rows = []
    for i, r in enumerate(ledger):
        tr = trans[join_of(r)]
        ko = r['Text']
        key = key_of(r)
        row = {'Collection': 'Data', 'Id': first_id + i, 'Key': key,
               'Smart': '0', 'ko': ko,
               'en': tr['en'], 'ja': tr['ja'], 'zhHans': tr['zhHans']}
        want = shape(ko)
        for loc in ('en', 'ja', 'zhHans'):
            if not row[loc].strip():
                errors.append('%s [%s]: blank' % (key, loc))
                continue
            got = shape(row[loc])
            for name, a, b in zip(SHAPE_NAMES, want, got):
                if a != b:
                    errors.append('%s [%s]: %s mismatch - ko has %s, translation has %s'
                                  % (key, loc, name, list(a), list(b)))
        rows.append(row)

    keys = collections.Counter(r['Key'] for r in rows)
    for k, n in keys.items():
        if n > 1:
            errors.append('duplicate key: %s (%d rows)' % (k, n))
    fail(errors)

    with io.open(out_path, 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['Collection', 'Id', 'Key', 'Smart',
                                          'ko', 'en', 'ja', 'zhHans'])
        w.writeheader()
        for r in rows:
            w.writerow(r)

    print('%s%s: %d rows -> %s   ids %d..%d'
          % (so_type, '' if asset is None else '/' + asset,
             len(rows), out_path, first_id, first_id + len(rows) - 1))
    print('ko cross-checked against ledger; placeholders / tags / escapes match; keys unique')


def fail(errors):
    if not errors:
        return
    for e in errors[:40]:
        print('  ERROR', e)
    if len(errors) > 40:
        print('  ... and %d more' % (len(errors) - 40))
    sys.exit('%d problem(s) - nothing written' % len(errors))


if __name__ == '__main__':
    main()
