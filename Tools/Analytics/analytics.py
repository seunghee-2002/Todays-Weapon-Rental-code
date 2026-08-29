#!/usr/bin/env python3
"""UGS Analytics 이벤트 스키마 도구.

events.json 이 이벤트/파라미터의 단일 원본이다.
UGS Event Manager 는 등록되지 않은 이벤트/파라미터를 invalid 로 조용히 버리므로,
코드가 보내는 것과 스키마가 어긋나면 데이터가 소리 없이 사라진다. 이 도구가 그걸 막는다.

    python analytics.py check    # C# 코드 <-> events.json 대조 (어긋나면 exit 1)
    python analytics.py render   # events.json -> 대시보드_등록.md 의 생성 섹션 갱신
    python analytics.py dump     # 코드 스캔 결과를 JSON 으로 출력 (스키마 작성/디버깅용)

UGS 대시보드에 실제로 반영하는 것은 dashboard_sync.py 가 담당한다.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent.parent
SCRIPTS_DIR = REPO_ROOT / "Assets" / "_Projects" / "Scripts"
SCHEMA_PATH = SCRIPT_DIR / "events.json"
DOC_PATH = (
    REPO_ROOT / "Documents" / "오늘도장비대여" / "Systems" / "Analytics" / "대시보드_등록.md"
)

GENERATED_BEGIN = "<!-- BEGIN GENERATED: analytics.py render -->"
GENERATED_END = "<!-- END GENERATED -->"

# AnalyticsManager 의 래퍼 메서드가 항상 붙이는 파라미터.
# 호출부 딕셔너리에는 없지만 실제로는 전송되므로 스캐너가 대신 채운다.
WRAPPER_FIXED_PARAMS = {
    "SendPanelOpened": ("panel_opened", ["panel", "is_first_time"]),
    "SendPanelClosed": ("panel_closed", ["panel", "duration_sec"]),
    "SendButtonClick": ("btn_clicked", ["panel", "button", "is_first_time"]),
}

# 파라미터 키로 인정할 문자열 형태 (UGS 는 소문자+언더스코어만 받는다)
KEY_RE = r"[a-z][a-z0-9_]*"


# ── 코드 스캔 ────────────────────────────────────────────────────────────────


def strip_line_comments(text: str) -> str:
    """// 주석만 제거한다. 문자열 리터럴 안의 '//' 는 건드리지 않는다."""
    out = []
    quote = None
    i = 0
    while i < len(text):
        ch = text[i]
        if quote:
            out.append(ch)
            if ch == "\\":
                if i + 1 < len(text):
                    out.append(text[i + 1])
                i += 2
                continue
            if ch == quote:
                quote = None
            i += 1
            continue

        if ch in "\"'":
            quote = ch
            out.append(ch)
            i += 1
            continue

        if ch == "/" and i + 1 < len(text) and text[i + 1] == "/":
            while i < len(text) and text[i] != "\n":
                i += 1
            continue

        out.append(ch)
        i += 1
    return "".join(out)


def match_parens(text: str, open_pos: int) -> int:
    """open_pos 의 '(' 에 대응하는 ')' 위치. 문자열 리터럴을 건너뛴다."""
    depth = 0
    quote = None
    i = open_pos
    while i < len(text):
        ch = text[i]
        if quote:
            if ch == "\\":
                i += 2
                continue
            if ch == quote:
                quote = None
        elif ch in "\"'":
            quote = ch
        elif ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
            if depth == 0:
                return i
        i += 1
    raise ValueError("괄호가 닫히지 않았습니다")


GENERIC_RE = re.compile(r"<[A-Za-z0-9_,\.\s\[\]\?]*>")


def strip_generics(text: str) -> str:
    """`Dictionary<string, object>` 같은 제네릭 인자를 지운다.

    인자를 최상위 콤마로 나눌 때 제네릭 안의 콤마에 걸려 잘못 쪼개지는 것을 막는다.
    `<=` 는 다음 문자가 문자 클래스에 없어 매칭되지 않으므로 비교 연산자는 살아남는다.
    """
    while True:
        stripped = GENERIC_RE.sub("", text)
        if stripped == text:
            return text
        text = stripped


def split_args(arg_text: str) -> list[str]:
    """최상위 콤마로만 인자를 나눈다."""
    arg_text = strip_generics(arg_text)
    args: list[str] = []
    depth = 0
    quote = None
    current = []
    i = 0
    while i < len(arg_text):
        ch = arg_text[i]
        if quote:
            current.append(ch)
            if ch == "\\":
                if i + 1 < len(arg_text):
                    current.append(arg_text[i + 1])
                i += 2
                continue
            if ch == quote:
                quote = None
            i += 1
            continue

        if ch in "\"'":
            quote = ch
        elif ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        elif ch == "," and depth == 0:
            args.append("".join(current).strip())
            current = []
            i += 1
            continue
        current.append(ch)
        i += 1

    tail = "".join(current).strip()
    if tail:
        args.append(tail)
    return args


def keys_from_literal(text: str) -> set[str]:
    """딕셔너리 리터럴/인덱서 대입에서 파라미터 키를 뽑는다.

    지원 형태:
      { "key", value }        컬렉션 이니셜라이저
      ["key"] = value         인덱서 이니셜라이저/대입
      .Add("key", value)      명시적 추가
    """
    keys: set[str] = set()
    keys.update(re.findall(rf'\{{\s*"({KEY_RE})"\s*,', text))
    keys.update(re.findall(rf'\[\s*"({KEY_RE})"\s*\]\s*=', text))
    keys.update(re.findall(rf'\.Add\(\s*"({KEY_RE})"\s*,', text))
    return keys


METHOD_DECL_RE = re.compile(
    r"^[ \t]*(?:\[[^\]]*\][ \t]*)?"
    r"(?:public|private|protected|internal)\b[^;=\n]*\([^\n]*$",
    re.MULTILINE,
)


def enclosing_method_start(text: str, pos: int) -> int:
    """호출 지점이 속한 메서드의 시작 오프셋 (못 찾으면 넉넉한 윈도우)."""
    last = 0
    for m in METHOD_DECL_RE.finditer(text, 0, pos):
        last = m.start()
    return last if last else max(0, pos - 3000)


def resolve_variable_keys(text: str, call_pos: int, ident: str) -> set[str] | None:
    """마지막 인자가 변수일 때, 같은 메서드 안에서 그 딕셔너리의 키를 찾는다.

    찾지 못하면 None (호출부를 UNRESOLVED 로 보고한다).
    """
    if not re.fullmatch(r"[A-Za-z_]\w*", ident):
        return None

    region = text[enclosing_method_start(text, call_pos) : call_pos]

    keys: set[str] = set()
    found_decl = False

    # var x = new Dictionary<string, object> { ... };
    for m in re.finditer(rf"\b{re.escape(ident)}\s*=\s*[^;]*?new Dictionary\s*<", region):
        found_decl = True
        brace = region.find("{", m.end())
        if brace == -1:
            continue
        try:
            end = match_parens(region, brace)
        except ValueError:
            continue
        keys.update(keys_from_literal(region[brace : end + 1]))

    # x["key"] = ... / x.Add("key", ...)
    scoped = re.findall(rf'\b{re.escape(ident)}\s*\[\s*"({KEY_RE})"\s*\]\s*=', region)
    scoped += re.findall(rf'\b{re.escape(ident)}\.Add\(\s*"({KEY_RE})"\s*,', region)
    if scoped:
        found_decl = True
        keys.update(scoped)

    return keys if found_decl else None


# `Dictionary extra = null` 처럼 `타입 이름` 형태인 인자 - 메서드 선언의 표식.
# (제네릭은 strip_generics 로 이미 지워진 뒤에 검사한다)
PARAM_DECL_RE = re.compile(r"[A-Za-z_][\w\.\[\]\?]*\s+[A-Za-z_]\w*\s*(?:=\s*.+)?")

ANALYTICS_CALL_RE = re.compile(
    r"(?:(?P<recv>[\w\.\?]*?)\.)?(?P<method>Send|SendPanelOpened|SendPanelClosed|SendButtonClick)\s*\("
)


def scan_file(path: Path) -> tuple[dict[str, set[str]], list[str]]:
    """한 파일에서 (이벤트 -> 파라미터 키 집합, UNRESOLVED 목록) 을 뽑는다."""
    raw = path.read_text(encoding="utf-8")
    text = strip_line_comments(raw)
    rel = path.relative_to(REPO_ROOT).as_posix()

    events: dict[str, set[str]] = {}
    unresolved: list[str] = []

    def line_of(pos: int) -> int:
        return text.count("\n", 0, pos) + 1

    for m in ANALYTICS_CALL_RE.finditer(text):
        recv = m.group("recv") or ""
        method = m.group("method")

        # 다른 클래스의 동명 메서드를 걸러낸다.
        # MorningEventViewBase 의 SendButtonClick 헬퍼는 수신자 없이 호출되므로 허용한다.
        # AnalyticsManager 자신은 SendDayBegin 등에서 수신자 없이 Send() 를 부른다.
        if method == "Send" and "analytics" not in recv.lower() and path.name != "AnalyticsManager.cs":
            continue
        if method != "Send" and recv and "analytics" not in recv.lower():
            continue

        open_paren = m.end() - 1
        try:
            close_paren = match_parens(text, open_paren)
        except ValueError:
            unresolved.append(f"{rel}:{line_of(m.start())} 괄호 파싱 실패 ({method})")
            continue

        args = split_args(text[open_paren + 1 : close_paren])
        loc = f"{rel}:{line_of(m.start())}"

        # AnalyticsManager 와 MorningEventViewBase 의 메서드 '선언'도 같은 패턴에 걸린다.
        # 선언의 인자는 `타입 이름` 형태라 호출 인자와 구분된다.
        if any(PARAM_DECL_RE.fullmatch(a) for a in args):
            continue

        if method == "Send":
            name_match = re.fullmatch(r'"([a-z][a-z0-9_]*)"', args[0]) if args else None
            if not name_match:
                unresolved.append(f"{loc} Send() 의 이벤트 이름이 상수가 아님: {args[:1]}")
                continue
            event = name_match.group(1)
            fixed: list[str] = []
            dict_args = args[1:]
        else:
            event, fixed = WRAPPER_FIXED_PARAMS[method]
            # 수신자가 있으면 AnalyticsManager 의 시그니처, 없으면
            # MorningEventViewBase 의 SendButtonClick(button, extra) 헬퍼다.
            # 헬퍼가 붙이는 event_type 은 헬퍼 본문의 호출에서 따로 잡힌다.
            fixed_arg_count = 1 if not recv else {"SendButtonClick": 2}.get(method, 1)
            dict_args = args[fixed_arg_count:]
            if method == "SendPanelClosed":
                dict_args = []

        keys = set(fixed)
        for arg in dict_args:
            arg = arg.strip()
            if not arg or arg == "null":
                continue
            if "new Dictionary" in arg or arg.startswith("{"):
                keys.update(keys_from_literal(arg))
            else:
                resolved = resolve_variable_keys(text, m.start(), arg)
                if resolved is None:
                    unresolved.append(f"{loc} {method}() 의 파라미터 딕셔너리 '{arg}' 를 추적하지 못함")
                else:
                    keys.update(resolved)

        events.setdefault(event, set()).update(keys)

    return events, unresolved


def scan_code() -> tuple[dict[str, set[str]], list[str]]:
    events: dict[str, set[str]] = {}
    unresolved: list[str] = []
    for path in sorted(SCRIPTS_DIR.rglob("*.cs")):
        file_events, file_unresolved = scan_file(path)
        for name, keys in file_events.items():
            events.setdefault(name, set()).update(keys)
        unresolved.extend(file_unresolved)
    return events, unresolved


# ── 스키마 ──────────────────────────────────────────────────────────────────


def load_schema() -> dict:
    if not SCHEMA_PATH.is_file():
        raise SystemExit(f"스키마 파일이 없습니다: {SCHEMA_PATH}")
    return json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))


def schema_event_params(schema: dict, event: str) -> set[str]:
    """이벤트에 연결된 파라미터 (공통 파라미터 포함)."""
    return set(schema["events"][event]["parameters"]) | set(schema["common_parameters"])


# ── 커맨드 ──────────────────────────────────────────────────────────────────


def cmd_check(args) -> int:
    schema = load_schema()
    code_events, unresolved = scan_code()

    errors: list[str] = []
    warnings: list[str] = []

    for event in sorted(code_events):
        if event not in schema["events"]:
            errors.append(
                f"이벤트 '{event}' 가 코드에는 있는데 events.json 에 없습니다 "
                f"- Event Manager 에 없으면 invalid 로 버려집니다"
            )
            continue

        allowed = schema_event_params(schema, event)
        for key in sorted(code_events[event] - allowed):
            if key not in schema["parameters"]:
                errors.append(f"파라미터 '{key}' ({event}) 가 events.json 에 정의되지 않았습니다")
            else:
                errors.append(f"파라미터 '{key}' 가 이벤트 '{event}' 에 연결돼 있지 않습니다")

    for event in sorted(set(schema["events"]) - set(code_events)):
        warnings.append(f"이벤트 '{event}' 는 events.json 에만 있고 코드가 보내지 않습니다")

    for event in sorted(code_events):
        if event not in schema["events"]:
            continue
        unused = schema_event_params(schema, event) - code_events[event] - set(schema["common_parameters"])
        for key in sorted(unused):
            warnings.append(f"파라미터 '{key}' 는 이벤트 '{event}' 에 연결돼 있지만 코드가 보내지 않습니다")

    orphan_params = set(schema["parameters"]) - set(schema["common_parameters"])
    for event_def in schema["events"].values():
        orphan_params -= set(event_def["parameters"])
    for key in sorted(orphan_params):
        warnings.append(f"파라미터 '{key}' 가 어떤 이벤트에도 연결돼 있지 않습니다")

    print(f"코드에서 발견한 이벤트 {len(code_events)}개 / 스키마 정의 {len(schema['events'])}개")

    if unresolved:
        print(f"\n[UNRESOLVED {len(unresolved)}] 정적으로 읽지 못한 호출 지점 - 직접 확인 필요")
        for item in unresolved:
            print(f"  {item}")

    if warnings:
        print(f"\n[경고 {len(warnings)}]")
        for item in warnings:
            print(f"  {item}")

    if errors:
        print(f"\n[오류 {len(errors)}] - 이 이벤트/파라미터는 UGS 에서 버려집니다")
        for item in errors:
            print(f"  {item}")
        return 1

    if args.strict and (warnings or unresolved):
        print("\n--strict: 경고/UNRESOLVED 가 있어 실패 처리합니다.")
        return 1

    print("\n코드와 스키마가 일치합니다.")
    return 0


def cmd_dump(_args) -> int:
    code_events, unresolved = scan_code()
    payload = {
        "events": {k: sorted(v) for k, v in sorted(code_events.items())},
        "unresolved": unresolved,
    }
    print(json.dumps(payload, ensure_ascii=False, indent=2))
    return 0


# Event Manager 가 쓰는 표기 그대로. 문서 출력 순서이기도 하다.
TYPE_ORDER = ["INTEGER", "FLOAT", "STRING", "BOOLEAN"]


def render_section(schema: dict) -> str:
    """events.json 에서 대시보드 등록용 파라미터/이벤트 목록을 만든다."""
    params = schema["parameters"]
    common = set(schema["common_parameters"])

    lines: list[str] = []
    lines.append("## 1. 파라미터 목록")
    lines.append("")
    lines.append(
        f"전체 {len(params)}개. Event Manager 에서 **한 번 만들면 여러 이벤트에 재사용**되므로 "
        "먼저 전부 만든 뒤 이벤트에 연결하는 순서가 빠르다."
    )
    lines.append("")
    lines.append(
        "> [!warning] required 는 전부 해제한다\n"
        "> 조건부 파라미터가 많다. 하나라도 required 면 값이 없는 순간 이벤트 전체가 invalid 로 버려진다."
    )
    lines.append("")

    if common:
        lines.append("### 공통 (모든 이벤트에 연결)")
        lines.append("")
        for name in sorted(common):
            lines.append(f"- [ ] `{name}` - {params[name]['type']} - {params[name]['description']}")
        lines.append("")

    by_type: dict[str, list[str]] = {}
    for name, meta in params.items():
        if name in common:
            continue
        by_type.setdefault(meta["type"], []).append(name)

    for type_name in TYPE_ORDER + sorted(set(by_type) - set(TYPE_ORDER)):
        names = by_type.get(type_name)
        if not names:
            continue
        lines.append(f"### {type_name} ({len(names)}개)")
        lines.append("")
        for name in sorted(names):
            lines.append(f"- [ ] `{name}` - {params[name]['description']}")
        lines.append("")

    events = schema["events"]
    lines.append(f"## 2. 이벤트 목록 ({len(events)}개)")
    lines.append("")
    common_list = ", ".join(f"`{n}`" for n in sorted(common))
    lines.append(f"모든 이벤트에 공통 파라미터 {common_list} 를 추가로 연결한다.")
    lines.append("")
    for name in sorted(events):
        meta = events[name]
        lines.append(f"- [ ] `{name}` - {meta['description']}")
        attached = meta["parameters"]
        lines.append("  - " + (", ".join(f"`{p}`" for p in attached) if attached else "(공통 파라미터만)"))
    lines.append("")

    return "\n".join(lines)


def cmd_render(_args) -> int:
    schema = load_schema()
    if not DOC_PATH.is_file():
        raise SystemExit(f"문서를 찾을 수 없습니다: {DOC_PATH}")

    doc = DOC_PATH.read_text(encoding="utf-8")
    if GENERATED_BEGIN not in doc or GENERATED_END not in doc:
        raise SystemExit(
            f"{DOC_PATH.name} 에 생성 마커가 없습니다.\n"
            f"  {GENERATED_BEGIN}\n  ...\n  {GENERATED_END}\n"
            "위 두 줄을 파라미터/이벤트 목록 자리에 넣어주세요."
        )

    start = doc.index(GENERATED_BEGIN) + len(GENERATED_BEGIN)
    end = doc.index(GENERATED_END)
    new_doc = doc[:start] + "\n\n" + render_section(schema) + "\n" + doc[end:]

    if new_doc == doc:
        print(f"{DOC_PATH.name} 은 이미 최신입니다.")
        return 0

    DOC_PATH.write_text(new_doc, encoding="utf-8")
    print(f"{DOC_PATH.name} 의 생성 섹션을 갱신했습니다.")
    return 0


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(
        description="UGS Analytics 이벤트 스키마 도구",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    sub = parser.add_subparsers(dest="command", required=True)

    p_check = sub.add_parser("check", help="C# 코드 <-> events.json 대조")
    p_check.add_argument("--strict", action="store_true", help="경고/UNRESOLVED 도 실패로 처리")

    sub.add_parser("render", help="events.json -> 대시보드_등록.md 생성 섹션 갱신")
    sub.add_parser("dump", help="코드 스캔 결과를 JSON 으로 출력")

    args = parser.parse_args()
    handlers = {"check": cmd_check, "render": cmd_render, "dump": cmd_dump}
    return handlers[args.command](args)


if __name__ == "__main__":
    sys.exit(main())
