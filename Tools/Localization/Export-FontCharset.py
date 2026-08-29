"""
Export-FontCharset.py

Writes the exact set of characters the game can display, per locale, so the ja/zh
font atlases can be Static-baked from a real list instead of a guessed range.

    python Tools/Localization/Export-FontCharset.py [outdir]
    # default outdir: Tools/Localization/out/charset

Output per locale: `<locale>.txt`   - the characters, sorted, one long line.
                   `<locale>.range` - the same set as a TMP "Custom Range" string
                                      (e.g. 32-126,44032-55203), which the Font
                                      Asset Creator also accepts.

Paste `<locale>.txt` into Font Asset Creator > Character Set: Custom Characters.

WHY A LIST AND NOT "BAKE THE COMMON KANJI":
  The game's own text is finite and enumerable - it all lives in the localization
  tables plus the Korean still sitting in ScriptableObjects. Measured, that is
  ~1,100 chars for ja and ~1,540 for zh, about 6.4 MB of atlas combined. Baking
  the standard "common" lists instead (Jouyou 2,136 + Tongyong level 1 3,500)
  costs ~24 MB - nearly 4x - AND still fails at the one thing it was meant to
  fix, because player nicknames routinely use name kanji outside those lists.
  So: Static-bake this exact list, and keep ONE dynamic fallback font at the end
  of the fallback chain for nicknames. See 다국어_진행상황.md, Phase 6.

  Nicknames are deliberately excluded from this export - they are unbounded, and
  that is precisely why the dynamic fallback has to stay.
"""
import collections
import csv
import io
import os
import re
import sys

TABLES = 'Assets/_Projects/Data/Localization/Tables'
LEDGER = 'Tools/Localization/out/so-ledger.csv'
LOCALES = ('ko-KR', 'en', 'ja', 'zh-Hans')

UESC = re.compile(r'\\u([0-9A-Fa-f]{4})')


def decode(s):
    return UESC.sub(lambda m: chr(int(m.group(1), 16)), s)


def to_ranges(chars):
    """Collapse a character set into TMP's 'a-b,c,d-e' decimal range syntax."""
    cps = sorted(ord(c) for c in chars)
    out, start, prev = [], None, None
    for cp in cps:
        if start is None:
            start = prev = cp
        elif cp == prev + 1:
            prev = cp
        else:
            out.append((start, prev))
            start = prev = cp
    if start is not None:
        out.append((start, prev))
    return ','.join(str(a) if a == b else '%d-%d' % (a, b) for a, b in out)


def main():
    outdir = sys.argv[1] if len(sys.argv) > 1 else 'Tools/Localization/out/charset'

    per = collections.defaultdict(set)

    # 1. every translated value, per locale
    for col in sorted(os.listdir(TABLES)):
        d = os.path.join(TABLES, col)
        if not os.path.isdir(d):
            continue
        for loc in LOCALES:
            p = os.path.join(d, '%s_%s.asset' % (col, loc))
            if not os.path.exists(p):
                continue
            text = io.open(p, encoding='utf-8', errors='replace').read()
            for v in re.findall(r'^    m_Localized: (.*)$', text, re.M):
                per[loc] |= set(decode(v.strip().strip('"')))

    # 2. Korean left in ScriptableObjects - DataLocalizer serves it verbatim when
    #    the locale is ko-KR, so it never passes through a table.
    if os.path.exists(LEDGER):
        for r in csv.DictReader(io.open(LEDGER, encoding='utf-8-sig')):
            per['ko-KR'] |= set(r['Text'])
    else:
        print('WARNING: %s missing - run Audit-SoText.ps1 first, ko-KR will be short' % LEDGER)

    # control characters are not glyphs
    for loc in per:
        per[loc] = {c for c in per[loc] if ord(c) >= 32 and c != ''}

    if not os.path.isdir(outdir):
        os.makedirs(outdir)

    print('%-9s %7s  %s' % ('locale', 'glyphs', 'files'))
    for loc in LOCALES:
        chars = ''.join(sorted(per[loc]))
        io.open(os.path.join(outdir, loc + '.txt'), 'w', encoding='utf-8').write(chars)
        io.open(os.path.join(outdir, loc + '.range'), 'w', encoding='utf-8').write(to_ranges(per[loc]))
        print('%-9s %7d  %s.txt / %s.range' % (loc, len(chars), loc, loc))

    # the two atlases that actually need rebuilding
    cjk = per['ja'] | per['zh-Hans']
    chars = ''.join(sorted(cjk))
    io.open(os.path.join(outdir, 'ja+zh-Hans.txt'), 'w', encoding='utf-8').write(chars)
    io.open(os.path.join(outdir, 'ja+zh-Hans.range'), 'w', encoding='utf-8').write(to_ranges(cjk))
    print('%-9s %7d  ja+zh-Hans.txt / .range   <- use this for the CJK atlases' % ('ja+zh', len(cjk)))
    print()
    print('Written to %s' % outdir)
    print('Reminder: keep one DYNAMIC fallback font at the end of the fallback chain,')
    print('or player-typed nicknames outside this set will render as tofu.')


if __name__ == '__main__':
    main()
