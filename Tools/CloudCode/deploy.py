#!/usr/bin/env python3
"""UGS Cloud Code Scripts 감사/배포 도구.

이 폴더의 .js 파일이 UGS Cloud Code의 단일 원본이다.
대시보드에서 직접 편집하면 이 도구가 다음 실행 때 되돌린다.

- 인증: Authorization: Basic base64(KEY_ID:SECRET)
  (Tools/admin-site/app/integrations/unity_client.py 와 같은 방식)
- 표준 라이브러리만 사용 - CI와 로컬이 같은 스크립트를 돌린다

사용법:
    python deploy.py envs            # 환경 목록/ID 조회
    python deploy.py diff            # 원격 vs 로컬 차이 (읽기 전용, 차이 있으면 exit 1)
    python deploy.py push            # 변경분만 반영 (확인 프롬프트)
    python deploy.py push --yes      # 확인 없이 반영 (CI용)

자격증명은 이 폴더의 .env 또는 OS 환경변수에서 읽는다 (.env.example 참고).
OS 환경변수가 .env보다 우선한다.
"""
from __future__ import annotations

import argparse
import base64
import difflib
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

SERVICES_BASE = "https://services.api.unity.com"
SCRIPT_DIR = Path(__file__).resolve().parent

# module.params 의 타입 표기 -> Cloud Code Admin API 의 enum.
# API 스펙(cloud-code-admin v1)이 받는 값은 이 5개뿐이다.
PARAM_TYPE_MAP = {
    "string": "STRING",
    "boolean": "BOOLEAN",
    "numeric": "NUMERIC",
    "json": "JSON",
    "any": "ANY",
}

# 스펙상 스크립트당 파라미터 최대 10개.
MAX_PARAMS = 10


class UgsError(RuntimeError):
    """UGS API 호출 실패. 상태 코드와 응답 본문을 보존한다."""

    def __init__(self, status: int, body: Any, endpoint: str):
        super().__init__(f"UGS API {status} on {endpoint}: {body}")
        self.status = status
        self.body = body
        self.endpoint = endpoint


# ── 설정 ────────────────────────────────────────────────────────────────────


class Config:
    def __init__(self, project_id: str, env_name: str, key_id: str, secret: str):
        self.project_id = project_id
        self.env_name = env_name
        self.key_id = key_id
        self.secret = secret
        self._env_id: str | None = None

    @property
    def auth_header(self) -> str:
        raw = f"{self.key_id}:{self.secret}".encode("utf-8")
        return "Basic " + base64.b64encode(raw).decode("ascii")


def load_dotenv(path: Path) -> dict[str, str]:
    """KEY=VALUE 형식의 .env 를 읽는다. 없으면 빈 dict."""
    if not path.is_file():
        return {}

    values: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, val = line.partition("=")
        val = val.strip()
        if len(val) >= 2 and val[0] == val[-1] and val[0] in "\"'":
            val = val[1:-1]
        values[key.strip()] = val
    return values


def load_config() -> Config:
    """OS 환경변수 > .env 순으로 자격증명을 읽는다."""
    dotenv = load_dotenv(SCRIPT_DIR / ".env")

    def get(name: str) -> str:
        return os.environ.get(name) or dotenv.get(name, "")

    required = {
        "UNITY_PROJECT_ID": get("UNITY_PROJECT_ID"),
        "UGS_SERVICE_KEY_ID": get("UGS_SERVICE_KEY_ID"),
        "UGS_SERVICE_SECRET_KEY": get("UGS_SERVICE_SECRET_KEY"),
    }
    missing = [k for k, v in required.items() if not v]
    if missing:
        raise SystemExit(
            "자격증명이 없습니다: " + ", ".join(missing) + "\n"
            f"  {SCRIPT_DIR / '.env.example'} 를 .env 로 복사해 값을 채우거나\n"
            "  같은 이름의 환경변수를 설정하세요.\n"
            "  키 발급: Unity Cloud > Administration > Service Accounts\n"
            "  필요 역할: Cloud Code Editor, Cloud Code Script Publisher,\n"
            "            Unity Environments Viewer"
        )

    return Config(
        project_id=required["UNITY_PROJECT_ID"],
        env_name=get("UNITY_ENV_NAME") or "production",
        key_id=required["UGS_SERVICE_KEY_ID"],
        secret=required["UGS_SERVICE_SECRET_KEY"],
    )


# ── HTTP ────────────────────────────────────────────────────────────────────


# 5xx/429 는 일시적일 수 있다 (publish 가 500 을 냈다가 재시도에서 바로 성공한 사례).
# 재시도하는 호출은 전부 멱등이거나(GET/PATCH) 중복 시 400/409 로 걸러진다(publish/create).
RETRY_STATUSES = {429, 500, 502, 503, 504}
MAX_ATTEMPTS = 3
RETRY_DELAY_SEC = 5


def request(cfg: Config, method: str, path: str, body: Any = None) -> Any:
    """UGS Admin API 호출. 2xx면 JSON(또는 None), 그 외 UgsError.

    path 는 보통 '/cloud-code/...' 형태지만, 페이지네이션 링크가 절대 URL 로 오는
    경우도 있어 둘 다 받는다.
    """
    url = path if path.startswith("http") else SERVICES_BASE + path
    data = json.dumps(body).encode("utf-8") if body is not None else None

    for attempt in range(1, MAX_ATTEMPTS + 1):
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("Authorization", cfg.auth_header)
        if data is not None:
            req.add_header("Content-Type", "application/json")

        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                raw = resp.read()
                if not raw:
                    return None
                try:
                    return json.loads(raw.decode("utf-8"))
                except ValueError:
                    return None
        except urllib.error.HTTPError as e:
            raw = e.read()
            try:
                parsed = json.loads(raw.decode("utf-8"))
            except (ValueError, UnicodeDecodeError):
                parsed = raw.decode("utf-8", "replace")[:500]

            if e.code in RETRY_STATUSES and attempt < MAX_ATTEMPTS:
                print(f"  {e.code} 응답 - {RETRY_DELAY_SEC}초 후 재시도 ({attempt}/{MAX_ATTEMPTS}): {method} {path}")
                time.sleep(RETRY_DELAY_SEC)
                continue

            raise UgsError(e.code, parsed, path) from None


def resolve_env_id(cfg: Config) -> str:
    """환경 이름 -> 환경 ID. 한 번 조회하면 캐시한다."""
    if cfg._env_id:
        return cfg._env_id

    result = request(cfg, "GET", f"/unity/v1/projects/{cfg.project_id}/environments")
    envs = (result or {}).get("results", [])
    for env in envs:
        if env.get("name") == cfg.env_name:
            cfg._env_id = env["id"]
            return cfg._env_id

    names = ", ".join(e.get("name", "?") for e in envs) or "(없음)"
    raise SystemExit(
        f"환경 '{cfg.env_name}' 을 찾을 수 없습니다. 존재하는 환경: {names}"
    )


def scripts_path(cfg: Config) -> str:
    env_id = resolve_env_id(cfg)
    return f"/cloud-code/v1/projects/{cfg.project_id}/environments/{env_id}/scripts"


# ── 로컬 .js 파싱 ────────────────────────────────────────────────────────────


def find_params_block(text: str) -> str | None:
    """`module.params = { ... };` 의 중괄호 블록 본문을 잘라낸다.

    문자열 리터럴 안의 중괄호에 속지 않도록 직접 스캔한다.
    선언이 없으면 None (파라미터 없는 스크립트).
    """
    match = re.search(r"^module\.params\s*=\s*\{", text, re.MULTILINE)
    if not match:
        return None

    start = match.end() - 1  # 여는 중괄호 위치
    depth = 0
    quote: str | None = None
    i = start
    while i < len(text):
        ch = text[i]
        if quote:
            if ch == "\\":
                i += 2
                continue
            if ch == quote:
                quote = None
        elif ch in "\"'`":
            quote = ch
        elif ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return text[start + 1 : i]
        i += 1

    raise ValueError("module.params 의 중괄호가 닫히지 않았습니다")


def parse_params(text: str, label: str) -> list[dict[str, Any]]:
    """module.params 를 Admin API 의 ScriptParameterList 형태로 변환한다.

    타입을 못 읽으면 조용히 넘기지 않고 즉시 실패한다 - 파라미터가 누락된 채
    배포되면 클라이언트 호출이 런타임에 깨지기 때문이다.
    """
    block = find_params_block(text)
    if block is None:
        return []

    # 주석 제거 (블록 안에는 URL 같은 '//' 문자열이 없다)
    block = re.sub(r"/\*.*?\*/", "", block, flags=re.DOTALL)
    block = re.sub(r"//[^\n]*", "", block)

    if "{" in re.sub(r"\{[^{}]*\}", "", block):
        raise ValueError(f"{label}: module.params 에 중첩 객체가 있어 파싱할 수 없습니다")

    params: list[dict[str, Any]] = []
    entry_re = re.compile(
        r"([A-Za-z_$][\w$]*|\"[^\"]*\"|'[^']*')\s*:\s*\{([^{}]*)\}"
    )
    for name_raw, inner in entry_re.findall(block):
        name = name_raw.strip("\"'")

        type_match = re.search(r"\btype\s*:\s*[\"']([A-Za-z]+)[\"']", inner)
        if not type_match:
            raise ValueError(f"{label}: 파라미터 '{name}' 에 type 이 없습니다")

        raw_type = type_match.group(1).lower()
        if raw_type not in PARAM_TYPE_MAP:
            raise ValueError(
                f"{label}: 파라미터 '{name}' 의 타입 '{type_match.group(1)}' 은 "
                f"UGS 가 받지 않습니다. 허용: String/Boolean/Numeric/JSON/Any"
            )

        required_match = re.search(r"\brequired\s*:\s*(true|false)", inner)
        params.append({
            "name": name,
            "type": PARAM_TYPE_MAP[raw_type],
            "required": required_match.group(1) == "true" if required_match else False,
        })

    if len(params) > MAX_PARAMS:
        raise ValueError(f"{label}: 파라미터가 {len(params)}개로 최대치({MAX_PARAMS})를 넘었습니다")

    return params


def normalize_code(code: str) -> str:
    """개행만 정규화해서 비교한다.

    저장소는 CRLF로 체크아웃되고 UGS는 LF로 보관하므로, 정규화하지 않으면
    모든 파일이 매번 '변경됨'으로 뜬다. 파일 끝 개행 차이도 무시한다.
    """
    return code.replace("\r\n", "\n").replace("\r", "\n").rstrip("\n")


def local_scripts() -> dict[str, dict[str, Any]]:
    """이 폴더의 .js 를 {스크립트명: {code, params}} 로 읽는다."""
    out: dict[str, dict[str, Any]] = {}
    for path in sorted(SCRIPT_DIR.glob("*.js")):
        text = path.read_text(encoding="utf-8")
        out[path.stem] = {
            # 업로드는 항상 LF 로 통일한다. 그러지 않으면 Windows 로컬(CRLF)과
            # CI(LF)가 번갈아 밀 때마다 원격 내용이 왔다갔다 한다.
            "code": normalize_code(text) + "\n",
            "params": parse_params(text, path.name),
            "path": path,
        }
    return out


# ── 원격 조회 ────────────────────────────────────────────────────────────────


def remote_script_names(cfg: Config) -> list[str]:
    """페이지네이션을 따라가며 원격 스크립트 이름을 모두 모은다."""
    names: list[str] = []
    path = scripts_path(cfg) + "?limit=100"
    while path:
        result = request(cfg, "GET", path) or {}
        names.extend(item["name"] for item in result.get("results", []))
        next_link = (result.get("links") or {}).get("next")
        path = next_link if next_link else None
    return names


def remote_script(cfg: Config, name: str) -> dict[str, Any] | None:
    """원격 스크립트 상세. 없으면 None."""
    try:
        return request(cfg, "GET", f"{scripts_path(cfg)}/{name}")
    except UgsError as e:
        if e.status == 404:
            return None
        raise


def params_signature(params: list[dict[str, Any]]) -> list[tuple]:
    """비교용 정규화 - 순서 차이는 무시한다."""
    return sorted(
        (p.get("name"), p.get("type", "ANY"), bool(p.get("required", False)))
        for p in params
    )


# ── 커맨드 ──────────────────────────────────────────────────────────────────


def cmd_envs(cfg: Config, _args) -> int:
    result = request(cfg, "GET", f"/unity/v1/projects/{cfg.project_id}/environments")
    envs = (result or {}).get("results", [])
    if not envs:
        print("환경이 없습니다.")
        return 1

    print(f"프로젝트 {cfg.project_id} 의 환경 {len(envs)}개:")
    for env in envs:
        flags = []
        if env.get("isDefault"):
            flags.append("default")
        if env.get("archivedAt"):
            flags.append("archived")
        if env.get("name") == cfg.env_name:
            flags.append("<- 설정된 환경")
        suffix = ("  [" + ", ".join(flags) + "]") if flags else ""
        print(f"  {env.get('name'):<16} {env.get('id')}{suffix}")
    return 0


def compute_plan(cfg: Config) -> tuple[list[dict[str, Any]], list[str]]:
    """로컬과 원격을 대조해 (변경 계획, 원격 전용 스크립트) 를 만든다."""
    local = local_scripts()
    remote_names = set(remote_script_names(cfg))

    plan: list[dict[str, Any]] = []
    for name, info in local.items():
        remote = remote_script(cfg, name) if name in remote_names else None
        active = (remote or {}).get("activeScript")

        if remote is None:
            plan.append({"name": name, "action": "create", "local": info, "remote": None})
            continue

        if active is None:
            plan.append({"name": name, "action": "publish-only", "local": info, "remote": remote})
            continue

        code_differs = normalize_code(active["code"]) != normalize_code(info["code"])
        params_differ = params_signature(active.get("params") or []) != params_signature(info["params"])

        if code_differs or params_differ:
            plan.append({
                "name": name,
                "action": "update",
                "local": info,
                "remote": remote,
                "code_differs": code_differs,
                "params_differ": params_differ,
            })
        else:
            plan.append({"name": name, "action": "unchanged", "local": info, "remote": remote})

    remote_only = sorted(remote_names - set(local))
    return plan, remote_only


def cmd_diff(cfg: Config, args) -> int:
    plan, remote_only = compute_plan(cfg)

    changed = 0
    for item in plan:
        name = item["name"]
        action = item["action"]

        if action == "unchanged":
            print(f"  = {name}")
            continue

        changed += 1
        if action == "create":
            print(f"\n  + {name} - 원격에 없음 (신규 배포 대상)")
            continue

        if action == "publish-only":
            print(f"\n  ! {name} - 원격에 초안만 있고 publish 된 적이 없음")
            continue

        print(f"\n  ~ {name}")
        if item["params_differ"]:
            remote_params = (item["remote"]["activeScript"].get("params") or [])
            print(f"      params 원격: {json.dumps(params_signature(remote_params), ensure_ascii=False)}")
            print(f"      params 로컬: {json.dumps(params_signature(item['local']['params']), ensure_ascii=False)}")
        if item["code_differs"]:
            remote_code = normalize_code(item["remote"]["activeScript"]["code"])
            local_code = normalize_code(item["local"]["code"])
            diff = difflib.unified_diff(
                remote_code.splitlines(),
                local_code.splitlines(),
                fromfile=f"UGS/{name}.js (v{item['remote']['activeScript']['version']})",
                tofile=f"local/{name}.js",
                lineterm="",
                n=args.context,
            )
            for line in diff:
                print("      " + line)

    # 대시보드에서 편집만 하고 publish 안 한 초안이 있으면 알려준다
    for item in plan:
        remote = item.get("remote")
        if not remote:
            continue
        drafts = [v for v in (remote.get("versions") or []) if v.get("isDraft")]
        active = remote.get("activeScript")
        if drafts and active and normalize_code(drafts[0].get("code", "")) != normalize_code(active["code"]):
            print(f"\n  * {item['name']} - 원격에 publish 되지 않은 초안이 있습니다 (대시보드 직접 편집 흔적)")

    if remote_only:
        print("\n  ? 원격에만 있는 스크립트 (이 도구는 삭제하지 않습니다):")
        for name in remote_only:
            print(f"      {name}")

    print(f"\n로컬 {len(plan)}개 중 변경 필요 {changed}개, 일치 {len(plan) - changed}개.")
    return 1 if changed else 0


def cmd_push(cfg: Config, args) -> int:
    plan, remote_only = compute_plan(cfg)
    todo = [i for i in plan if i["action"] != "unchanged"]

    if remote_only:
        print("경고: 원격에만 있는 스크립트 (삭제하지 않음): " + ", ".join(remote_only))

    if not todo:
        print("변경 사항 없음 - UGS 와 저장소가 일치합니다.")
        return 0

    print("반영 대상:")
    for item in todo:
        print(f"  {item['action']:<13} {item['name']}")

    if not args.yes:
        answer = input("\n위 내용을 UGS 프로덕션에 반영합니다. 계속할까요? [y/N] ").strip().lower()
        if answer not in ("y", "yes"):
            print("취소했습니다.")
            return 130

    base = scripts_path(cfg)
    failed: list[str] = []
    published = 0

    # 한 스크립트가 실패해도 나머지는 계속 처리한다. 중간에 멈추면 일부만 반영된
    # 상태가 남고 무엇이 남았는지 알기 어렵다.
    for item in todo:
        name = item["name"]
        info = item["local"]

        try:
            if item["action"] == "create":
                request(cfg, "POST", base, {
                    "name": name,
                    "type": "API",
                    "language": "JS",
                    "code": info["code"],
                    "params": info["params"],
                })
                print(f"  생성 {name}")
            elif item["action"] == "update":
                request(cfg, "PATCH", f"{base}/{name}", {
                    "code": info["code"],
                    "params": info["params"],
                })
                print(f"  갱신 {name}")
        except UgsError as e:
            failed.append(f"{name} 갱신 실패 ({e.status}): {describe_error(e)}")
            print(f"  실패 {name}: {e.status}")
            continue

        # publish 는 활성 버전과 동일하면 400 을 낸다 (스펙: 활성 버전 재publish 불가)
        try:
            result = request(cfg, "POST", f"{base}/{name}/publish", {}) or {}
            published += 1
            print(f"  publish {name} -> v{result.get('version', '?')}")
        except UgsError as e:
            if e.status == 400:
                print(f"  publish {name} 생략 (이미 활성 버전과 동일)")
            else:
                failed.append(f"{name} publish 실패 ({e.status}): {describe_error(e)}")
                print(f"  실패 {name} publish: {e.status}")

    print(f"\n{published}/{len(todo)}개 publish 완료.")
    if failed:
        print("\n실패 항목:")
        for line in failed:
            print(f"  {line}")
        if any("403" in line for line in failed):
            print(
                "\n403 은 서비스 계정 역할 부족이다. Unity Cloud > Administration >\n"
                "  Service Accounts 에서 프로젝트 역할을 확인하세요:\n"
                "    Cloud Code Editor          - 스크립트 조회/수정\n"
                "    Cloud Code Script Publisher - publish (별도 역할이다)\n"
                "    Unity Environments Viewer   - 환경 ID 조회\n"
                "  갱신(PATCH)까지 됐다면 초안만 올라간 상태이고 활성 버전은 그대로다.\n"
                "  역할을 추가한 뒤 push 를 다시 실행하면 이어서 publish 된다."
            )
        return 1

    return 0


def describe_error(e: UgsError) -> str:
    """UgsError 본문에서 사람이 읽을 부분만 뽑는다."""
    if isinstance(e.body, dict):
        return str(e.body.get("detail") or e.body.get("title") or e.body)
    return str(e.body)


# ── 진입점 ──────────────────────────────────────────────────────────────────


def main() -> int:
    # Windows 콘솔 기본 코드페이지에서 한국어 diff 가 깨지지 않도록.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(
        description="UGS Cloud Code Scripts 감사/배포",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("envs", help="환경 목록/ID 조회")

    p_diff = sub.add_parser("diff", help="원격 vs 로컬 차이 (읽기 전용)")
    p_diff.add_argument("--context", type=int, default=3, help="diff 문맥 줄 수 (기본 3)")

    p_push = sub.add_parser("push", help="변경분만 UGS 에 반영")
    p_push.add_argument("--yes", action="store_true", help="확인 프롬프트 생략 (CI용)")

    args = parser.parse_args()
    handlers = {"envs": cmd_envs, "diff": cmd_diff, "push": cmd_push}

    try:
        cfg = load_config()
        return handlers[args.command](cfg, args)
    except UgsError as e:
        print(f"UGS API 오류 {e.status} ({e.endpoint}): {e.body}", file=sys.stderr)
        return 2
    except (ValueError, urllib.error.URLError) as e:
        print(f"오류: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
