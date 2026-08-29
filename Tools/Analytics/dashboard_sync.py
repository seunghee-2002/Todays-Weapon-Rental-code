#!/usr/bin/env python3
"""UGS Analytics Event Manager 대시보드 동기화 (Playwright).

>>> 비공식 경로다. <<<
Unity 는 Analytics 이벤트 스키마를 위한 공개 API 를 제공하지 않는다.
  - 공식 Analytics OpenAPI(v1) 의 경로는 이벤트 전송용 2개뿐이다
  - Unity Web API 카탈로그에 analytics-admin 네임스페이스가 없다
  - ugs deploy 지원 목록에도 Analytics 가 없다
그래서 이 스크립트는 사람이 하던 대시보드 조작을 브라우저로 대신한다.
대시보드 UI 나 내부 API 가 바뀌면 깨질 수 있으므로, apply 후에는 반드시
pull 로 결과를 다시 확인한다.

사용 순서:
    python dashboard_sync.py login      # 브라우저에서 직접 로그인 -> 세션 저장
    python dashboard_sync.py capture    # 대시보드가 쓰는 내부 API 관찰 (진단용)
    python dashboard_sync.py pull       # 현재 등록 상태 -> dashboard.json
    python dashboard_sync.py diff       # dashboard.json vs events.json
    python dashboard_sync.py apply --dry-run
    python dashboard_sync.py apply      # 부족한 파라미터/이벤트 생성 (삭제는 안 함)

설치:
    pip install -r requirements.txt
    playwright install chromium

브라우저:
    창을 띄우는 실행에서 Playwright 번들 chromium 이 즉시 죽는 환경이 있다
    (headless 는 되는데 headed 만 실패). 그래서 설치된 Edge/Chrome 을 먼저 시도하고,
    처음 성공한 채널을 .auth/config.json 에 기억한다. --channel 로 직접 고를 수도 있다.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
AUTH_DIR = SCRIPT_DIR / ".auth"
# 로그인 세션은 storage_state 파일이 아니라 실제 브라우저 프로필에 둔다.
# Unity 로그인은 여러 오리진을 오가며 쿠키/스토리지를 쓰기 때문에 프로필 쪽이 안정적이고,
# 창을 닫은 뒤에 상태를 긁어오는 경쟁 조건도 없다.
PROFILE_DIR = AUTH_DIR / "profile"
CONFIG_PATH = AUTH_DIR / "config.json"
NETWORK_PATH = SCRIPT_DIR / "network.json"
DASHBOARD_PATH = SCRIPT_DIR / "dashboard.json"
SCHEMA_PATH = SCRIPT_DIR / "events.json"

CLOUD_HOME = "https://cloud.unity.com/"

# 창을 띄우는 실행에서 Playwright 번들 chromium 이 즉시 죽는 환경이 있다
# (headless 는 되는데 headed 만 실패). 설치된 실제 브라우저를 먼저 시도한다.
CHANNEL_CANDIDATES = ["msedge", "chrome", ""]  # "" = 번들 chromium

# 내부 API 응답을 골라내는 힌트. capture 로 실제 트래픽을 본 뒤 좁힐 수 있다.
API_URL_HINT = re.compile(r"(analytics|event)", re.IGNORECASE)

# Event Manager 화면인지 판정. 대시보드 경로가 바뀌어 왔으므로 넉넉하게 본다
# (현재는 .../analytics/v2/events, 예전에는 .../analytics/event-manager).
EVENT_MANAGER_URL_HINT = re.compile(r"/analytics/.*\b(events?|event-manager)\b", re.IGNORECASE)


def require_playwright():
    try:
        from playwright.sync_api import sync_playwright  # noqa: F401
    except ImportError:
        raise SystemExit(
            "playwright 가 설치돼 있지 않습니다.\n"
            f"  pip install -r {SCRIPT_DIR / 'requirements.txt'}\n"
            "  playwright install chromium"
        )
    from playwright.sync_api import sync_playwright

    return sync_playwright


def load_config() -> dict:
    if CONFIG_PATH.is_file():
        return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    return {}


def save_config(config: dict) -> None:
    AUTH_DIR.mkdir(exist_ok=True)
    CONFIG_PATH.write_text(json.dumps(config, ensure_ascii=False, indent=2), encoding="utf-8")


def event_manager_url(args) -> str:
    override = getattr(args, "url", None)
    if override:
        # 한 번 넘긴 URL 은 기억해서 다음부터 --url 없이도 돌게 한다.
        config = load_config()
        if config.get("event_manager_url") != override:
            config["event_manager_url"] = override
            save_config(config)
        return override

    url = load_config().get("event_manager_url")
    if not url:
        raise SystemExit(
            "Event Manager URL 을 모릅니다.\n"
            "  python dashboard_sync.py login 을 실행해 대시보드의 Event Manager 화면까지\n"
            "  이동한 뒤 창을 닫으면 그 URL 이 저장됩니다.\n"
            "  또는 --url 로 직접 넘기세요."
        )
    return url


def require_session():
    if not PROFILE_DIR.is_dir():
        raise SystemExit(
            "로그인 세션이 없습니다. 먼저 실행하세요:\n"
            "  python dashboard_sync.py login"
        )


def launch_context(p, headless: bool, channel: str | None):
    """브라우저 프로필로 컨텍스트를 연다.

    channel 이 주어지면 그것만, 없으면 후보를 차례로 시도하고 성공한 채널을 저장한다.
    """
    AUTH_DIR.mkdir(exist_ok=True)
    candidates = [channel] if channel is not None else CHANNEL_CANDIDATES
    errors: list[str] = []

    for candidate in candidates:
        kwargs = {"channel": candidate} if candidate else {}
        try:
            context = p.chromium.launch_persistent_context(
                str(PROFILE_DIR), headless=headless, **kwargs
            )
        except Exception as e:
            errors.append(f"  {candidate or '번들 chromium'}: {str(e).strip().splitlines()[0]}")
            continue

        if channel is None:
            config = load_config()
            if config.get("channel") != candidate:
                config["channel"] = candidate
                save_config(config)
        return context, candidate

    raise SystemExit(
        "브라우저를 띄우지 못했습니다. 시도한 채널:\n"
        + "\n".join(errors)
        + "\n  Edge 나 Chrome 이 설치돼 있으면 --channel msedge / --channel chrome 로 지정하고,\n"
        "  번들 chromium 을 쓰려면 'playwright install chromium' 을 다시 실행하세요."
    )


def resolve_channel(args) -> str | None:
    """--channel 우선, 없으면 저장된 값, 그것도 없으면 None(자동 탐색).

    CLI 의 'chromium' 은 Playwright 번들을 뜻하며 내부적으로는 빈 문자열이다.
    """
    chosen = getattr(args, "channel", None)
    if chosen is not None:
        return "" if chosen == "chromium" else chosen
    return load_config().get("channel")


# ── login ───────────────────────────────────────────────────────────────────


def cmd_login(args) -> int:
    sync_playwright = require_playwright()

    print("브라우저를 엽니다. Unity 계정으로 로그인한 뒤")
    print("  Analytics > Event Manager 화면까지 이동하고 창을 닫으세요.")
    print("  (그 시점의 URL 이 저장되고, 로그인 상태는 브라우저 프로필에 남습니다)")

    last_url = {"value": ""}

    def track(page):
        last_url["value"] = page.url
        page.on(
            "framenavigated",
            lambda frame: last_url.update(value=frame.url)
            if frame == page.main_frame else None,
        )

    with sync_playwright() as p:
        context, channel = launch_context(p, headless=False, channel=resolve_channel(args))
        print(f"  브라우저: {channel or '번들 chromium'}")

        context.on("page", track)
        page = context.pages[0] if context.pages else context.new_page()
        track(page)
        page.goto(args.url or load_config().get("event_manager_url") or CLOUD_HOME)

        # 사용자가 창을 닫을 때까지 기다린다.
        try:
            context.wait_for_event("close", timeout=0)
        except Exception:
            pass

    config = load_config()
    final_url = last_url["value"]
    if EVENT_MANAGER_URL_HINT.search(final_url):
        config["event_manager_url"] = final_url
        print(f"Event Manager URL 저장: {final_url}")
    else:
        print(f"마지막 URL 이 Event Manager 가 아닙니다: {final_url or '(없음)'}")
        print("  로그인 자체는 저장됐습니다. Event Manager 화면 URL 을 --url 로 넘기거나")
        print("  login 을 다시 실행해 그 화면에서 창을 닫으세요.")
    save_config(config)

    print(f"프로필 저장: {PROFILE_DIR}")
    print("이 폴더(.auth)는 .gitignore 대상입니다 - 절대 커밋하지 마세요.")
    return 0


# ── capture ─────────────────────────────────────────────────────────────────


def collect_json_responses(page, url: str, timeout_ms: int) -> list[dict]:
    """페이지를 열고 오가는 JSON 응답을 전부 모은다."""
    captured: list[dict] = []

    def on_response(response):
        try:
            ctype = response.headers.get("content-type", "")
            if "json" not in ctype:
                return
            captured.append({
                "url": response.url,
                "method": response.request.method,
                "status": response.status,
                "request_body": response.request.post_data,
                "body": response.json(),
            })
        except Exception:
            pass

    page.on("response", on_response)
    page.goto(url, wait_until="networkidle", timeout=timeout_ms)
    page.wait_for_timeout(3000)
    return captured


def cmd_capture(args) -> int:
    require_session()
    sync_playwright = require_playwright()
    url = event_manager_url(args)

    with sync_playwright() as p:
        context, _ = launch_context(p, args.headless, resolve_channel(args))
        page = context.pages[0] if context.pages else context.new_page()
        captured = collect_json_responses(page, url, args.timeout * 1000)
        context.close()

    NETWORK_PATH.write_text(
        json.dumps(captured, ensure_ascii=False, indent=2, default=str), encoding="utf-8"
    )
    print(f"JSON 응답 {len(captured)}건을 {NETWORK_PATH.name} 에 저장했습니다.")

    interesting = [c for c in captured if API_URL_HINT.search(c["url"])]
    if interesting:
        print("\nAnalytics 관련으로 보이는 응답:")
        for item in interesting:
            print(f"  {item['status']} {item['method']} {item['url']}")
    else:
        print("\n힌트에 걸리는 응답이 없습니다. network.json 을 직접 확인하세요.")
    return 0


# ── pull ────────────────────────────────────────────────────────────────────


def normalize_pulled(schemas: list, parameters: list) -> dict:
    """대시보드 응답을 {events, parameters, ...} 로 정규화한다.

    - 이벤트별 파라미터는 `parameters` 필드가 아니라 `schema`(JSON Schema 문자열)의
      properties.eventParams.properties 에 들어있다. 전자는 항상 비어 있다.
    - SDK 가 모든 이벤트에 자동으로 붙이는 표준 파라미터(platform/clientVersion/
      sdkMethod/userCountry 등)는 isPredefined 로 걸러 커스텀 것만 남긴다.
    """
    predefined = {p["name"] for p in parameters if p.get("isPredefined")}

    events: dict[str, list[str]] = {}
    disabled: list[str] = []
    required: dict[str, list[str]] = {}

    for entry in schemas:
        if entry.get("isPredefined"):
            continue
        name = entry.get("name")
        if not name:
            continue

        try:
            event_params = json.loads(entry["schema"])["properties"]["eventParams"]
        except (ValueError, KeyError, TypeError):
            event_params = {}

        props = event_params.get("properties") or {}
        events[name] = sorted(k for k in props if k not in predefined)

        if event_params.get("required"):
            required[name] = list(event_params["required"])
        if not entry.get("isEnabled", True):
            disabled.append(name)

    return {
        "events": events,
        "parameters": {
            p["name"]: str(p.get("type", "")).upper()
            for p in parameters
            if not p.get("isPredefined")
        },
        "disabled_events": sorted(disabled),
        "required_parameters": required,
    }


def fetch_dashboard(args) -> dict:
    """Event Manager 화면을 열고 대시보드가 부르는 두 응답을 가로챈다.

    비공식 내부 API:
      GET .../live-ops/events/v3/organizations/{org}/projects/{p}/environments/{e}/schemas
      GET .../live-ops/events/v3/organizations/{org}/projects/{p}/environments/{e}/parameters
    직접 호출하지 않고 화면 로드 중의 응답을 받는다 - 인증 헤더를 흉내 낼 필요가 없다.
    """
    require_session()
    sync_playwright = require_playwright()
    url = event_manager_url(args)

    grabbed: dict[str, list] = {}
    captured: list[dict] = []

    with sync_playwright() as p:
        context, _ = launch_context(p, args.headless, resolve_channel(args))
        page = context.pages[0] if context.pages else context.new_page()

        def on_response(response):
            try:
                if "json" not in response.headers.get("content-type", ""):
                    return
                body = response.json()
            except Exception:
                return
            captured.append({"url": response.url, "status": response.status, "body": body})
            for key in ("schemas", "parameters"):
                if response.url.endswith(f"/{key}") and isinstance(body, list):
                    grabbed[key] = body

        page.on("response", on_response)
        page.goto(url, wait_until="domcontentloaded", timeout=args.timeout * 1000)
        # SPA 가 목록을 받아올 때까지 기다린다 (networkidle 로는 이르다)
        for _ in range(args.timeout):
            if "schemas" in grabbed and "parameters" in grabbed:
                break
            page.wait_for_timeout(1000)
        context.close()

    if "schemas" not in grabbed or "parameters" not in grabbed:
        NETWORK_PATH.write_text(
            json.dumps(captured, ensure_ascii=False, indent=2, default=str), encoding="utf-8"
        )
        raise SystemExit(
            "대시보드에서 스키마 응답을 받지 못했습니다 "
            f"(받은 것: {', '.join(grabbed) or '없음'}).\n"
            f"  캡처 원본을 {NETWORK_PATH.name} 에 저장했습니다. 내부 API 경로가 바뀌었을 수 있으니\n"
            "  fetch_dashboard 의 응답 필터를 확인하세요. --headless 없이 다시 시도해도 좋습니다."
        )

    return normalize_pulled(grabbed["schemas"], grabbed["parameters"])


def cmd_pull(args) -> int:
    result = fetch_dashboard(args)
    DASHBOARD_PATH.write_text(
        json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(
        f"커스텀 이벤트 {len(result['events'])}개 / 커스텀 파라미터 "
        f"{len(result['parameters'])}개를 {DASHBOARD_PATH.name} 에 저장했습니다."
    )
    if result["disabled_events"]:
        print(f"  비활성 이벤트: {', '.join(result['disabled_events'])}")
    if result["required_parameters"]:
        print(f"  required 가 걸린 이벤트: {', '.join(result['required_parameters'])}")
    return 0


# ── diff / apply ────────────────────────────────────────────────────────────


def load_schema() -> dict:
    return json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))


def compute_actions() -> tuple[list[dict], list[str]]:
    """(대시보드에 추가해야 할 액션, 사람이 판단해야 할 경고)."""
    if not DASHBOARD_PATH.is_file():
        raise SystemExit("dashboard.json 이 없습니다. 먼저 pull 을 실행하세요.")

    schema = load_schema()
    dashboard = json.loads(DASHBOARD_PATH.read_text(encoding="utf-8"))

    live_events: dict[str, set[str]] = {
        k: set(v) for k, v in dashboard.get("events", {}).items()
    }
    live_params: dict[str, str] = dashboard.get("parameters", {})

    actions: list[dict] = []
    warnings: list[str] = []
    common = list(schema["common_parameters"])

    for name, meta in schema["parameters"].items():
        if name not in live_params:
            actions.append({"kind": "create_parameter", "name": name, "type": meta["type"]})
        elif live_params[name] != meta["type"]:
            warnings.append(
                f"파라미터 '{name}' 타입 불일치 - 대시보드={live_params[name]}, "
                f"events.json={meta['type']} (대시보드에서 타입은 바꿀 수 없다)"
            )

    for name, meta in schema["events"].items():
        wanted = list(dict.fromkeys(meta["parameters"] + common))
        if name not in live_events:
            actions.append({"kind": "create_event", "name": name, "parameters": wanted})
            continue
        missing = [p for p in wanted if p not in live_events[name]]
        if missing:
            actions.append({"kind": "assign_parameters", "name": name, "parameters": missing})

        extra_params = sorted(live_events[name] - set(wanted))
        if extra_params:
            warnings.append(
                f"이벤트 '{name}' 에 코드가 보내지 않는 파라미터가 연결돼 있음: "
                f"{', '.join(extra_params)} (required 만 아니면 무해)"
            )

    for name in sorted(set(live_events) - set(schema["events"])):
        warnings.append(f"이벤트 '{name}' 은 대시보드에만 있음 (이 도구는 삭제하지 않는다)")

    for name in dashboard.get("disabled_events", []):
        warnings.append(f"이벤트 '{name}' 이 비활성 상태 - 수집되지 않는다")

    for name, required in (dashboard.get("required_parameters") or {}).items():
        warnings.append(
            f"이벤트 '{name}' 에 required 파라미터가 걸려 있음: {', '.join(required)} "
            f"- 값이 빠지는 순간 이벤트 전체가 invalid 로 버려진다"
        )

    orphan = sorted(set(live_params) - set(schema["parameters"]))
    if orphan:
        warnings.append(
            f"대시보드에만 있는 커스텀 파라미터 {len(orphan)}개: {', '.join(orphan)} "
            f"(어떤 이벤트에도 연결돼 있지 않으면 무해)"
        )

    return actions, warnings


def cmd_diff(_args) -> int:
    actions, warnings = compute_actions()

    if not actions:
        print("대시보드가 events.json 과 일치합니다 (추가할 것 없음).")
    else:
        print(f"대시보드에 추가해야 할 항목 {len(actions)}건:")
        for action in actions:
            if action["kind"] == "create_parameter":
                print(f"  파라미터 생성  {action['name']} ({action['type']})")
            elif action["kind"] == "create_event":
                print(f"  이벤트 생성    {action['name']}  [{', '.join(action['parameters'])}]")
            else:
                print(f"  파라미터 연결  {action['name']} <- {', '.join(action['parameters'])}")

    if warnings:
        print(f"\n경고 {len(warnings)}건:")
        for line in warnings:
            print(f"  {line}")

    return 1 if actions else 0


class EventManagerPage:
    """Event Manager 화면 조작.

    DOM 구조에 의존하는 유일한 부분이다. 셀렉터는 CSS 대신 역할/텍스트 기반으로 잡아
    클래스명 변경에 덜 취약하게 만들었지만, 대시보드 UI 가 바뀌면 여기부터 고치면 된다.
    """

    def __init__(self, page):
        self.page = page

    def _click_add_new(self, item_text: str) -> None:
        self.page.get_by_role("button", name=re.compile("Add New", re.I)).click()
        self.page.get_by_role("menuitem", name=re.compile(item_text, re.I)).click()

    def create_parameter(self, name: str, type_name: str) -> None:
        self._click_add_new("Parameter")
        self.page.get_by_label(re.compile("name", re.I)).fill(name)
        self.page.get_by_label(re.compile("type", re.I)).select_option(label=type_name.title())
        self.page.get_by_role("button", name=re.compile("^(Create|Save|Add)$", re.I)).click()
        self.page.wait_for_timeout(500)

    def create_event(self, name: str) -> None:
        self._click_add_new("Custom Event")
        self.page.get_by_label(re.compile("name", re.I)).fill(name)
        self.page.get_by_role("button", name=re.compile("^(Create|Save|Add)$", re.I)).click()
        self.page.wait_for_timeout(500)

    def assign_parameters(self, event_name: str, parameters: list[str]) -> None:
        self.page.get_by_role("link", name=event_name, exact=True).click()
        for param in parameters:
            self.page.get_by_role("button", name=re.compile("Assign Parameter", re.I)).click()
            self.page.get_by_role("textbox").last.fill(param)
            self.page.get_by_role("option", name=param, exact=True).click()
            self.page.get_by_role("button", name=re.compile("^(Assign|Save|Add)$", re.I)).click()
            self.page.wait_for_timeout(400)
        self.page.go_back()


def cmd_apply(args) -> int:
    actions, warnings = compute_actions()
    for line in warnings:
        print(f"경고: {line}")

    if not actions:
        print("추가할 항목이 없습니다.")
        return 0

    print(f"적용 대상 {len(actions)}건:")
    for action in actions:
        print(f"  {action['kind']:<18} {action['name']}")

    if args.dry_run:
        print("\n--dry-run: 실제로는 아무것도 바꾸지 않았습니다.")
        return 0

    if not args.yes:
        answer = input("\nUGS Event Manager 에 위 항목을 생성합니다. 계속할까요? [y/N] ").strip().lower()
        if answer not in ("y", "yes"):
            print("취소했습니다.")
            return 130

    require_session()
    sync_playwright = require_playwright()
    url = event_manager_url(args)

    done = 0
    failed: list[str] = []
    with sync_playwright() as p:
        context, _ = launch_context(p, args.headless, resolve_channel(args))
        page = context.pages[0] if context.pages else context.new_page()
        page.goto(url, wait_until="networkidle", timeout=args.timeout * 1000)
        em = EventManagerPage(page)

        for action in actions:
            label = f"{action['kind']} {action['name']}"
            try:
                if action["kind"] == "create_parameter":
                    em.create_parameter(action["name"], action["type"])
                elif action["kind"] == "create_event":
                    em.create_event(action["name"])
                    em.assign_parameters(action["name"], action["parameters"])
                else:
                    em.assign_parameters(action["name"], action["parameters"])
                done += 1
                print(f"  완료 {label}")
            except Exception as e:  # 대시보드 UI 변경 등
                failed.append(f"{label}: {e}")
                print(f"  실패 {label}: {e}")

        context.close()

    print(f"\n{done}/{len(actions)}건 적용.")
    if failed:
        print("\n실패 항목 - 대시보드에서 직접 처리하거나 EventManagerPage 셀렉터를 고쳐야 합니다:")
        for item in failed:
            print(f"  {item}")

    print("\n반드시 pull 을 다시 돌려 결과를 확인하세요:")
    print("  python dashboard_sync.py pull && python dashboard_sync.py diff")
    return 1 if failed else 0


# ── 진입점 ──────────────────────────────────────────────────────────────────


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(
        description="UGS Analytics Event Manager 대시보드 동기화 (비공식)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--url", help="Event Manager URL (기본: login 때 저장된 값)")
    parser.add_argument("--timeout", type=int, default=60, help="페이지 로드 타임아웃(초)")
    parser.add_argument("--headless", action="store_true", help="브라우저 창 없이 실행")
    parser.add_argument(
        "--channel",
        choices=["msedge", "chrome", "chromium"],
        help="사용할 브라우저 (기본: 처음 성공한 채널을 자동 선택해 기억)",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("login", help="브라우저 로그인 후 세션 저장")
    sub.add_parser("capture", help="대시보드 내부 API 트래픽 기록 (진단용)")
    sub.add_parser("pull", help="현재 등록 상태 -> dashboard.json")
    sub.add_parser("diff", help="dashboard.json vs events.json")

    p_apply = sub.add_parser("apply", help="부족한 파라미터/이벤트 생성")
    p_apply.add_argument("--dry-run", action="store_true", help="계획만 출력")
    p_apply.add_argument("--yes", action="store_true", help="확인 프롬프트 생략")

    args = parser.parse_args()
    handlers = {
        "login": cmd_login,
        "capture": cmd_capture,
        "pull": cmd_pull,
        "diff": cmd_diff,
        "apply": cmd_apply,
    }
    return handlers[args.command](args)


if __name__ == "__main__":
    sys.exit(main())
