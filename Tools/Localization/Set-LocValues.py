"""
Set-LocValues.py

Rewrites the value of EXISTING localization entries. Add-LocEntries.ps1 creates
keys and Remove-LocEntries.ps1 deletes them; this is the missing third verb, for
when a translation itself has to change (terminology fixes, QA follow-ups).

    python Tools/Localization/Set-LocValues.py <edits.csv> [--dry-run]

Input CSV (UTF-8, with or without BOM):

    Collection,Key,Locale,Value
    UI_Common,Common_Disassemble,en,Salvage

Locale is one of ko-KR / en / ja / zh-Hans. Value is written the way
Add-LocEntries.ps1 writes one: a double-quoted YAML scalar, so a newline inside a
value is the two characters \\n and a literal quote is \\".

Safety:
  - every (Collection, Key, Locale) must resolve, and the key's current value must
    be found in the locale asset - otherwise NOTHING is written
  - a no-op edit (new value identical to current) is reported, not silently kept
  - CRLF and the missing BOM are preserved: Unity writes these files without a
    BOM, and rewriting them wholesale turns a 3-line change into a 4,000-line diff
  - ko-KR is writable but warned about: ko is the source language, and for the
    Data collection the SO is the real source, so changing ko here only edits the
    translator's reference copy.

Run Verify-Localization.ps1 afterwards; this tool does not touch ids or metadata,
so id-set integrity cannot break, but check 7 (Korean left in a translated
locale) is worth re-running.
"""
import csv
import io
import os
import re
import sys

TABLES = 'Assets/_Projects/Data/Localization/Tables'
LOCALES = ('ko-KR', 'en', 'ja', 'zh-Hans')


def esc(v):
    """Match Add-LocEntries.ps1: only the quote character needs escaping;
    backslash sequences the author typed (\\n) must survive verbatim."""
    return v.replace('"', '\\"')


def scalar_span(txt, start):
    """txt[start:] 에서 시작하는 값의 (끝 위치, 원문)을 돌려준다.

    Unity 는 긴 값을 YAML 줄 접힘으로 **여러 줄에 나눠** 쓴다. 첫 줄만 바꾸면
    뒷줄이 고아로 남고, Unity 는 파일 전체를 거부한다
    ("Expect ':' between key and value within mapping"). 그래서 따옴표가
    닫힐 때까지 줄을 넘어가며 값 전체를 잡는다.
    """
    if start >= len(txt) or txt[start] != '"':
        e = txt.find('\n', start)
        e = len(txt) if e < 0 else e
        return e, txt[start:e].rstrip('\r')
    i, n = start + 1, len(txt)
    while i < n:
        if txt[i] == '\\':
            i += 2
            continue
        if txt[i] == '"':
            i += 1
            break
        i += 1
    return i, txt[start:i]


def unfold(raw):
    """접힌 스칼라를 YAML 규칙대로 한 줄로 편다 (줄바꿈 -> 공백 하나)."""
    parts = raw.split('\n')
    return parts[0].rstrip('\r') + ''.join(' ' + p.rstrip('\r').strip() for p in parts[1:])


ORPHAN = re.compile(r'^\s*m_Localized:', re.M)


def find_orphan(txt):
    """m_Localized 값 바로 뒤가 m_Metadata 가 아니면 고아 줄이 남은 것이다."""
    for m in ORPHAN.finditer(txt):
        vs = txt.index(':', m.start()) + 1
        while vs < len(txt) and txt[vs] == ' ':
            vs += 1
        end, _ = scalar_span(txt, vs)
        nl = txt.find('\n', end)
        if nl < 0:
            continue
        nxt = txt[nl + 1:txt.find('\n', nl + 1)].rstrip('\r').strip()
        if nxt != 'm_Metadata:':
            return txt.count('\n', 0, nl) + 2, nxt
    return None


def main():
    argv = [a for a in sys.argv[1:] if a != '--dry-run']
    dry = '--dry-run' in sys.argv[1:]
    if len(argv) != 1:
        sys.exit(__doc__)

    edits = list(csv.DictReader(io.open(argv[0], encoding='utf-8-sig')))
    if not edits:
        sys.exit('no rows in %s' % argv[0])
    for r in edits:
        for c in ('Collection', 'Key', 'Locale', 'Value'):
            if c not in r:
                sys.exit('missing column %r - need Collection,Key,Locale,Value' % c)

    errors, planned = [], []
    # group by file so each asset is read and written once
    byfile = {}
    for i, r in enumerate(edits):
        col, key, loc = r['Collection'].strip(), r['Key'].strip(), r['Locale'].strip()
        if loc not in LOCALES:
            errors.append('row %d: unknown locale %r' % (i + 2, loc))
            continue
        shared = os.path.join(TABLES, col, '%s Shared Data.asset' % col)
        target = os.path.join(TABLES, col, '%s_%s.asset' % (col, loc))
        if not os.path.exists(shared) or not os.path.exists(target):
            errors.append('row %d: no such collection/locale: %s / %s' % (i + 2, col, loc))
            continue
        sh = io.open(shared, encoding='utf-8', newline='').read()
        m = re.search(r'- m_Id: (\d+)\s*\r?\n\s*m_Key: %s\s*\r?$' % re.escape(key), sh, re.M)
        if not m:
            errors.append('row %d: key %r not found in %s' % (i + 2, key, col))
            continue
        byfile.setdefault(target, []).append((m.group(1), key, r['Value']))
    fail(errors)

    changed = noop = 0
    writes = {}
    for target, items in sorted(byfile.items()):
        txt = io.open(target, encoding='utf-8', newline='').read()
        for mid, key, val in items:
            head = re.compile(r'(- m_Id: %s\s*\r?\n\s*m_Localized: )' % mid)
            hm = head.search(txt)
            if not hm:
                errors.append('%s: entry id %s (%s) has no m_Localized line' % (target, mid, key))
                continue
            end, raw = scalar_span(txt, hm.end())
            cur = unfold(raw)
            new = '"%s"' % esc(val)
            if cur == new:
                noop += 1
                planned.append(('same ', target, key, val))
                continue
            txt = txt[:hm.end()] + new + txt[end:]
            changed += 1
            planned.append(('write', target, key, val))
        orphan = find_orphan(txt)
        if orphan:
            errors.append('%s: line %d is left over from a folded value (%r) - '
                          'Unity would refuse the whole file' % (target, orphan[0], orphan[1]))
        writes[target] = txt
    fail(errors)

    for line in planned:
        print('  %s %-46s %-52s' % (line[0], os.path.basename(line[1]), line[2][:52]))

    if dry:
        print('\n[dry run] %d would change, %d already correct - nothing written' % (changed, noop))
        return

    for target, txt in writes.items():
        # no BOM, and the CRLF that is already in txt is preserved because the
        # file was read and is written with newline=''
        io.open(target, 'w', encoding='utf-8', newline='').write(txt)

    print('\n%d value(s) changed, %d already correct, across %d file(s)'
          % (changed, noop, len(writes)))
    if any('_ko-KR.asset' in t for t in writes):
        print('NOTE: ko-KR was edited. ko is the source language; for the Data '
              'collection the ScriptableObject is the real source and this is only '
              'the translator reference copy.')


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
