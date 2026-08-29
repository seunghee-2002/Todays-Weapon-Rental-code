"""
Audit-Consistency.py   (Localization QA)

Everything about translation consistency that a machine can decide on its own,
run over every key in every collection. Judgement calls (tone, naturalness,
context) are deliberately NOT here - this tool's job is to hand a human or a
reviewer a short, evidence-backed list instead of 8,432 strings.

    python Tools/Localization/Audit-Consistency.py [outdir]
    # default outdir: Tools/Localization/out/qa

Checks
  A  shape      - {placeholder} / rich-text tag / backslash-escape / brace counts
                  must match ko in every locale. A mismatch is a real defect:
                  a dropped {0} silently deletes a number from the screen.
  B  forward    - the same ko string translated two different ways.
  C  reverse    - the same en string coming from two different ko strings.
  D  length     - target/source length ratios far outside the local norm, which
                  is where UI overflow comes from.
  E  blank      - empty or whitespace-only values.
  F  smart      - Smart String syntax present but the entry is not flagged Smart
                  (it would render literally, e.g. "3 {days:plural:day|days}"),
                  or the flag set differs between locales.

Why exact-string matching for B/C: it produces zero false positives. Two entries
with byte-identical Korean SHOULD read identically in every language unless
somebody deliberately varied them, so every hit is worth a look. Fuzzy term
matching would drown the report in noise.

Output: <check>.csv per check + summary.txt. Nothing is written to the tables.
"""
import collections
import csv
import io
import os
import re
import sys

TABLES = 'Assets/_Projects/Data/Localization/Tables'
LOCALES = ('ko-KR', 'en', 'ja', 'zh-Hans')
TARGETS = ('en', 'ja', 'zh-Hans')

UESC = re.compile(r'\\u([0-9A-Fa-f]{4})')
# {name} and also the Smart String form {name:plural:a|b} - the argument is the
# same, so both normalise to {name} before comparison.
PLACEHOLDER = re.compile(r'\{([A-Za-z_0-9]+)(?::[^{}]*)?\}')
SMART_SYNTAX = re.compile(r'\{[A-Za-z_0-9]+:[^{}]*\}')
RICHTAG = re.compile(r'</?[a-zA-Z][a-zA-Z0-9]*(?:=[^>]*)?>')
ESCAPE = re.compile(r'\\+[a-zA-Z"]')

# Korean-only grammar slots. UITranslator.ObjectParticle returns "" outside
# ko-KR by design, so a translation that drops them is correct, not broken.
KO_ONLY_PLACEHOLDERS = {'particle'}


def dec(s):
    return UESC.sub(lambda m: chr(int(m.group(1), 16)), s)


def placeholders(s):
    # a SET, not a list: English pluralisation legitimately references the same
    # argument twice ("{days} {days:plural:day|days}"). What must not happen is
    # an argument appearing that ko never supplies, or a ko argument vanishing.
    return sorted({p for p in PLACEHOLDER.findall(s) if p not in KO_ONLY_PLACEHOLDERS})


def shape(s):
    # brace counts are compared on the placeholder-stripped remainder, so Smart
    # syntax and the dropped Korean particle do not register as a mismatch while
    # a genuinely stray '{' still does.
    rest = PLACEHOLDER.sub('', s)
    return (tuple(placeholders(s)),
            tuple(sorted(RICHTAG.findall(s))),
            tuple(sorted(ESCAPE.findall(s))),
            (rest.count('{'), rest.count('}')))


SHAPE_NAMES = ('placeholder', 'rich-text tag', 'backslash escape', 'brace count')


def load():
    """[(collection, key, {locale: value})] for every entry in every collection."""
    rows = []
    for col in sorted(os.listdir(TABLES)):
        d = os.path.join(TABLES, col)
        if not os.path.isdir(d):
            continue
        shared = os.path.join(d, '%s Shared Data.asset' % col)
        if not os.path.exists(shared):
            continue
        text = io.open(shared, encoding='utf-8').read()
        id2key = dict(re.findall(r'- m_Id: (\d+)\s*\n\s*m_Key: (\S+)', text))

        vals = {}
        smart = {}
        for loc in LOCALES:
            p = os.path.join(d, '%s_%s.asset' % (col, loc))
            if not os.path.exists(p):
                continue
            t = io.open(p, encoding='utf-8').read()
            for i, v in re.findall(r'- m_Id: (\d+)\s*\n\s*m_Localized: (.*)', t):
                vals.setdefault(i, {})[loc] = dec(v.strip()).strip('"')
            # "m_Items: []" = not smart; "m_Items:\n      - rid: N" = smart
            for i, items in re.findall(
                    r'- m_Id: (\d+)\s*\n\s*m_Localized:.*\n\s*m_Metadata:\s*\n\s*m_Items:(.*)', t):
                smart.setdefault(i, {})[loc] = not items.strip().startswith('[]')

        for i, key in sorted(id2key.items(), key=lambda x: int(x[0])):
            rows.append((col, key, vals.get(i, {}), smart.get(i, {})))
    return rows


def w(outdir, name, header, records):
    p = os.path.join(outdir, name)
    with io.open(p, 'w', encoding='utf-8-sig', newline='') as f:
        wr = csv.writer(f)
        wr.writerow(header)
        for r in records:
            wr.writerow(r)
    return len(records)


def main():
    outdir = sys.argv[1] if len(sys.argv) > 1 else 'Tools/Localization/out/qa'
    if not os.path.isdir(outdir):
        os.makedirs(outdir)

    rows = load()
    summary = []
    summary.append('entries scanned: %d across %d collections'
                   % (len(rows), len(set(r[0] for r in rows))))

    # ---- full dump ---------------------------------------------------------
    # One row per key with all four locales side by side. This is the file to
    # read when reviewing wording by hand or building a glossary - the .asset
    # files store \uXXXX escapes and cannot be read directly.
    w(outdir, 'all.csv', ['Collection', 'Key', 'ko', 'en', 'ja', 'zhHans'],
      [(col, key, v.get('ko-KR', ''), v.get('en', ''), v.get('ja', ''), v.get('zh-Hans', ''))
       for col, key, v, sm in rows])

    # ---- E blank -----------------------------------------------------------
    blank = []
    for col, key, v, sm in rows:
        for loc in LOCALES:
            if loc not in v or not v[loc].strip():
                blank.append((col, key, loc, v.get('ko-KR', '')))
    summary.append('E blank/missing values: %d' % w(outdir, 'E_blank.csv',
                   ['Collection', 'Key', 'Locale', 'ko'], blank))

    # ---- A shape -----------------------------------------------------------
    bad = []
    for col, key, v, sm in rows:
        ko = v.get('ko-KR')
        if ko is None:
            continue
        want = shape(ko)
        for loc in TARGETS:
            if loc not in v:
                continue
            got = shape(v[loc])
            for nm, a, b in zip(SHAPE_NAMES, want, got):
                if a != b:
                    bad.append((col, key, loc, nm, str(a), str(b), ko, v[loc]))
    summary.append('A shape mismatches: %d' % w(outdir, 'A_shape.csv',
                   ['Collection', 'Key', 'Locale', 'Kind', 'ko_has', 'translation_has',
                    'ko', 'translation'], bad))

    # ---- B forward: same ko, different translation -------------------------
    by_ko = collections.defaultdict(list)
    for col, key, v, sm in rows:
        ko = (v.get('ko-KR') or '').strip()
        if ko:
            by_ko[ko].append((col, key, v))
    fwd = []
    for ko, group in sorted(by_ko.items()):
        if len(group) < 2:
            continue
        for loc in TARGETS:
            variants = collections.OrderedDict()
            for col, key, v in group:
                variants.setdefault(v.get(loc, ''), []).append('%s/%s' % (col, key))
            if len(variants) > 1:
                for val, keys in variants.items():
                    fwd.append((loc, ko, val, len(keys), '; '.join(keys[:6])))
    summary.append('B same ko -> different translation: %d row(s)'
                   % w(outdir, 'B_forward.csv',
                       ['Locale', 'ko', 'translation', 'key_count', 'keys'], fwd))

    # ---- C reverse: same en, different ko ----------------------------------
    by_en = collections.defaultdict(set)
    detail = collections.defaultdict(list)
    for col, key, v, sm in rows:
        en = (v.get('en') or '').strip()
        ko = (v.get('ko-KR') or '').strip()
        if en and ko:
            by_en[en].add(ko)
            detail[en].append('%s/%s' % (col, key))
    rev = []
    for en, kos in sorted(by_en.items()):
        if len(kos) > 1:
            rev.append((en, len(kos), ' | '.join(sorted(kos)), '; '.join(detail[en][:6])))
    summary.append('C same en <- different ko: %d' % w(outdir, 'C_reverse.csv',
                   ['en', 'ko_variants', 'ko_list', 'keys'], rev))

    # ---- D length ----------------------------------------------------------
    # Ratio against ko, judged per locale against that locale's own median, so
    # the normal en-is-longer effect does not flood the report.
    ratios = collections.defaultdict(list)
    for col, key, v, sm in rows:
        ko = (v.get('ko-KR') or '').strip()
        if len(ko) < 2:
            continue
        for loc in TARGETS:
            t = (v.get(loc) or '').strip()
            if t:
                ratios[loc].append((len(t) / float(len(ko)), col, key, ko, t))
    long_rows = []
    for loc, items in ratios.items():
        vals = sorted(x[0] for x in items)
        med = vals[len(vals) // 2]
        for ratio, col, key, ko, t in items:
            # only flag when it is both relatively and absolutely long: a short
            # UI label blowing up is what actually breaks a layout.
            if ratio > med * 2.2 and len(t) >= 12:
                long_rows.append((loc, col, key, round(ratio, 2), round(med, 2),
                                  len(ko), len(t), ko, t))
    long_rows.sort(key=lambda r: -r[3])
    summary.append('D length outliers (ratio > 2.2x locale median, >=12 chars): %d'
                   % w(outdir, 'D_length.csv',
                       ['Locale', 'Collection', 'Key', 'ratio', 'locale_median',
                        'ko_len', 'tr_len', 'ko', 'translation'], long_rows))

    # ---- F smart-string flag ----------------------------------------------
    # Smart syntax without the flag renders literally on screen; the flag
    # differing between locales means one language silently loses pluralisation.
    smartbad = []
    for col, key, v, sm in rows:
        for loc in LOCALES:
            if loc not in v:
                continue
            uses = bool(SMART_SYNTAX.search(v[loc]))
            flagged = sm.get(loc)
            if uses and flagged is False:
                smartbad.append((col, key, loc, 'syntax but NOT flagged smart',
                                 v[loc]))
        flags = {loc: sm[loc] for loc in LOCALES if loc in sm}
        if flags and len(set(flags.values())) > 1:
            on = ','.join(sorted(l for l, f in flags.items() if f))
            off = ','.join(sorted(l for l, f in flags.items() if not f))
            smartbad.append((col, key, '-', 'flag differs: on=[%s] off=[%s]' % (on, off),
                             v.get('ko-KR', '')))
    summary.append('F smart-string problems: %d' % w(outdir, 'F_smart.csv',
                   ['Collection', 'Key', 'Locale', 'Problem', 'value'], smartbad))

    with io.open(os.path.join(outdir, 'summary.txt'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(summary) + '\n')
    print('\n'.join(summary))
    print('\nwritten to %s' % outdir)


if __name__ == '__main__':
    main()
