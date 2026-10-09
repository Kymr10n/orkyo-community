#!/usr/bin/env python3
"""keycloak-converge.py — bring an existing Keycloak realm to the state this release expects.

Keycloak imports realm.json only when the realm does not exist yet (`--import-realm` is
first-boot only). Every realm change shipped in a later release reached new installations
only; an existing self-hosted install ran an old realm against a new application, and the
operator got a manual admin-console procedure (saas#314). The `keycloak-config` service in
compose.yml runs this script after Keycloak is healthy and before the API starts, on every
`docker compose up`, so the realm converges to the release.

Owns exactly the realm properties the product depends on, and nothing an operator may have
tuned by hand:

  1. the `orkyo-backend` client: its secret from .env, and the redirect URIs, web origins and
     post-logout URIs that APP_BASE_URL implies (added, never removed);
  2. the WebAuthn Passwordless policy that passkeys need, read from this release's
     realm.json, with the relying-party id derived from APP_BASE_URL;
  3. the passkey step in the browser flow: "Condition - credential" set to
     `webauthn-passwordless`, Required, inside "Browser - Conditional 2FA".

Two rules, the same as orkyo-infra's configure-keycloak-deploy.sh: every write is additive
and convergent, and nothing is ever deleted. A second run after a successful one changes
nothing. Standard library only: the container that runs it is a plain `python:3-alpine`.
"""

from __future__ import annotations

import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

TIMEOUT_SECONDS = 180


# Every value comes from the compose environment; an absent or empty one stops the run,
# which stops the API (compose.yml makes the API depend on this service completing).
REQUIRED_SETTINGS = (
    "KEYCLOAK_INTERNAL_URL",
    "KEYCLOAK_REALM",
    "KEYCLOAK_ADMIN",
    "KEYCLOAK_ADMIN_PASSWORD",
    "APP_BASE_URL",
    "KEYCLOAK_BACKEND_CLIENT_ID",
    "KEYCLOAK_BACKEND_CLIENT_SECRET",
)
missing = [name for name in REQUIRED_SETTINGS if not os.environ.get(name)]
if missing:
    print(f"[ERR ] required: {', '.join(missing)}", file=sys.stderr)
    sys.exit(1)
settings = {name: os.environ[name] for name in REQUIRED_SETTINGS}

KC = settings["KEYCLOAK_INTERNAL_URL"].rstrip("/")
REALM = settings["KEYCLOAK_REALM"]
ADMIN_USER = settings["KEYCLOAK_ADMIN"]
APP_BASE_URL = settings["APP_BASE_URL"].rstrip("/")
CLIENT_ID = settings["KEYCLOAK_BACKEND_CLIENT_ID"]

APP_BASE_HOST = urllib.parse.urlsplit(APP_BASE_URL).hostname or ""
if not APP_BASE_HOST:
    print(f"[ERR ] cannot derive a host from APP_BASE_URL={APP_BASE_URL!r}", file=sys.stderr)
    sys.exit(1)

# The realm.json this release bakes into its Keycloak image, mounted by compose.yml. The
# policy is read from it rather than restated here, so a new installation and a converged
# one get the same values. The relying-party id is a template placeholder in the file.
REALM_JSON = "/realm.json"
POLICY_PREFIX = "webAuthnPolicyPasswordless"


def load_passwordless_policy() -> dict:
    with open(REALM_JSON, encoding="utf-8") as f:
        realm = json.load(f)
    policy = {k: v for k, v in realm.items() if k.startswith(POLICY_PREFIX)}
    if not policy:
        print(f"[ERR ] {REALM_JSON} has no {POLICY_PREFIX}* keys", file=sys.stderr)
        sys.exit(1)
    policy[POLICY_PREFIX + "RpId"] = APP_BASE_HOST
    return policy


PASSWORDLESS_POLICY = load_passwordless_policy()

SUBFLOW_NAME = "Browser - Conditional 2FA"
CONDITION_PROVIDER = "conditional-credential"
USER_CONFIGURED_PROVIDER = "conditional-user-configured"
CONDITION_CONFIG_ALIAS = "orkyo-passkey-credential"

changes: list[str] = []


def info(msg: str) -> None:
    print(f"[INFO] {msg}", flush=True)


def changed(msg: str) -> None:
    changes.append(msg)
    print(f"[ OK ] {msg}", flush=True)


def http(method: str, url: str, body: object | None = None, token: str | None = None,
         form: dict[str, str] | None = None):
    data = None
    headers = {"Accept": "application/json"}
    if form is not None:
        data = urllib.parse.urlencode(form).encode()
        headers["Content-Type"] = "application/x-www-form-urlencoded"
    elif body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            raw = response.read()
            return json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        detail = error.read().decode(errors="replace")
        print(f"[ERR ] {method} {url} -> {error.code}: {detail[:500]}", file=sys.stderr)
        sys.exit(1)


def wait_for_keycloak() -> None:
    deadline = time.monotonic() + TIMEOUT_SECONDS
    while True:
        try:
            with urllib.request.urlopen(f"{KC}/realms/master", timeout=5) as response:
                if response.status == 200:
                    return
        except (urllib.error.URLError, OSError):
            pass
        if time.monotonic() > deadline:
            print(f"[ERR ] Keycloak at {KC} did not answer within {TIMEOUT_SECONDS}s", file=sys.stderr)
            sys.exit(1)
        time.sleep(3)


def admin_token() -> str:
    answer = http("POST", f"{KC}/realms/master/protocol/openid-connect/token", form={
        "grant_type": "password",
        "client_id": "admin-cli",
        "username": ADMIN_USER,
        "password": settings["KEYCLOAK_ADMIN_PASSWORD"],
    })
    return answer["access_token"]


class Admin:
    def __init__(self, token: str) -> None:
        self.token = token
        self.base = f"{KC}/admin/realms/{urllib.parse.quote(REALM, safe='')}"

    def get(self, path: str):
        return http("GET", f"{self.base}{path}", token=self.token)

    def put(self, path: str, body: object) -> None:
        http("PUT", f"{self.base}{path}", body=body, token=self.token)

    def post(self, path: str, body: object) -> None:
        http("POST", f"{self.base}{path}", body=body, token=self.token)


# ── 1. The backend client ────────────────────────────────────────────────────

def converge_backend_client(admin: Admin) -> None:
    clients = admin.get(f"/clients?clientId={urllib.parse.quote(CLIENT_ID)}")
    if not clients:
        print(f"[ERR ] client {CLIENT_ID} does not exist in realm {REALM}; the realm import never ran", file=sys.stderr)
        sys.exit(1)
    client = clients[0]
    client_path = f"/clients/{client['id']}"
    updates: dict[str, object] = {}

    want_redirect = f"{APP_BASE_URL}/*"
    redirect_uris = list(client.get("redirectUris") or [])
    if want_redirect not in redirect_uris:
        updates["redirectUris"] = redirect_uris + [want_redirect]

    web_origins = list(client.get("webOrigins") or [])
    if APP_BASE_URL not in web_origins:
        updates["webOrigins"] = web_origins + [APP_BASE_URL]

    attributes = dict(client.get("attributes") or {})
    post_logout = [u for u in (attributes.get("post.logout.redirect.uris") or "").split("##") if u]
    if want_redirect not in post_logout:
        attributes["post.logout.redirect.uris"] = "##".join(post_logout + [want_redirect])
        updates["attributes"] = attributes

    current_secret = (admin.get(f"{client_path}/client-secret") or {}).get("value")
    if current_secret != settings["KEYCLOAK_BACKEND_CLIENT_SECRET"]:
        updates["secret"] = settings["KEYCLOAK_BACKEND_CLIENT_SECRET"]

    if not updates:
        info(f"client {CLIENT_ID}: already converged")
        return
    body = {**client, **updates}
    admin.put(client_path, body)
    for key in updates:
        changed(f"client {CLIENT_ID}: {'secret set from .env' if key == 'secret' else key + ' now includes ' + APP_BASE_URL}")


# ── 2. The WebAuthn Passwordless policy ─────────────────────────────────────

def converge_passwordless_policy(admin: Admin) -> None:
    realm = admin.get("")
    drift = {k: v for k, v in PASSWORDLESS_POLICY.items() if realm.get(k) != v}
    if not drift:
        info("WebAuthn Passwordless policy: already converged")
        return
    admin.put("", drift)
    changed("WebAuthn Passwordless policy: " + ", ".join(f"{k.removeprefix('webAuthnPolicyPasswordless')}={v!r}" for k, v in drift.items()))


# ── 3. The passkey step in the browser flow ──────────────────────────────────

def subflow_children(executions: list[dict], subflow: dict) -> list[dict]:
    """The sub-flow's direct steps. The flattened list is ordered and nested by `level`:
    the children follow the sub-flow entry one level deeper, until the next entry at its
    own level or above."""
    children = []
    for entry in executions[executions.index(subflow) + 1:]:
        if entry["level"] <= subflow["level"]:
            break
        if entry["level"] == subflow["level"] + 1:
            children.append(entry)
    return children


def converge_browser_flow(admin: Admin) -> None:
    realm = admin.get("")
    browser_alias = realm.get("browserFlow") or "browser"
    browser_path = f"/authentication/flows/{urllib.parse.quote(browser_alias, safe='')}/executions"

    def load() -> tuple[list[dict], dict | None]:
        executions = admin.get(browser_path)
        # A flow an operator copied carries the copy's name in every sub-flow alias
        # ("<copy name> Browser - Conditional 2FA"), so match by containment, not equality.
        subflow = next((e for e in executions if e.get("authenticationFlow") and SUBFLOW_NAME in (e.get("displayName") or "")), None)
        return executions, subflow

    executions, subflow = load()
    if subflow is None:
        print(f"[WARN] browser flow {browser_alias!r} has no sub-flow {SUBFLOW_NAME!r}; passkey step left as is", flush=True)
        return

    # /authentication/flows lists top-level flows only; the sub-flow is read by its id.
    subflow_alias = admin.get(f"/authentication/flows/{subflow['flowId']}")["alias"]
    subflow_path = f"/authentication/flows/{urllib.parse.quote(subflow_alias, safe='')}/executions"

    children = subflow_children(executions, subflow)
    step = next((c for c in children if c.get("providerId") == CONDITION_PROVIDER), None)
    if step is None:
        # Keycloak refuses to edit a built-in flow (400). A stock realm on Keycloak 26.7 already
        # has the step, and Keycloak's own upgrade adds it to a stock realm from an older
        # version; only a flow an operator copied and customised can lack it AND be editable.
        browser_flow = next((f for f in admin.get("/authentication/flows") if f["alias"] == browser_alias), None)
        if browser_flow is None or browser_flow.get("builtIn"):
            print(f"[WARN] browser flow {browser_alias!r} is built in and has no 'Condition - credential'; "
                  "Keycloak adds it on its own upgrade, nothing to do here", flush=True)
            return
        admin.post(f"{subflow_path}/execution", {"provider": CONDITION_PROVIDER})
        changed(f"browser flow: added 'Condition - credential' to '{SUBFLOW_NAME}'")
        executions, subflow = load()
        children = subflow_children(executions, subflow)
        step = next(c for c in children if c.get("providerId") == CONDITION_PROVIDER)
    else:
        info("browser flow: 'Condition - credential' present")

    if step.get("requirement") != "REQUIRED":
        admin.put(subflow_path, {"id": step["id"], "requirement": "REQUIRED"})
        changed("browser flow: 'Condition - credential' set to Required")

    if not step.get("authenticationConfig"):
        admin.post(f"/authentication/executions/{step['id']}/config",
                   {"alias": CONDITION_CONFIG_ALIAS, "config": {"credentials": "webauthn-passwordless"}})
        changed("browser flow: 'Condition - credential' configured for webauthn-passwordless")
    else:
        config = admin.get(f"/authentication/config/{step['authenticationConfig']}")
        if (config.get("config") or {}).get("credentials") != "webauthn-passwordless":
            config["config"] = {**(config.get("config") or {}), "credentials": "webauthn-passwordless"}
            admin.put(f"/authentication/config/{step['authenticationConfig']}", config)
            changed("browser flow: 'Condition - credential' now checks webauthn-passwordless")

    # Directly after "Condition - user configured", where the manual procedure put it. Keycloak
    # may insert a new execution first or last, so move in whichever direction is needed, one
    # step per call, re-reading after each so the order is real.
    moves = 0
    while True:
        executions, subflow = load()
        children = subflow_children(executions, subflow)
        ids = [c["id"] for c in children]
        user_configured = next((c for c in children if c.get("providerId") == USER_CONFIGURED_PROVIDER), None)
        if user_configured is None or step["id"] not in ids:
            break
        position, target = ids.index(step["id"]), ids.index(user_configured["id"]) + 1
        if position == target:
            break
        # Above the target means above "Condition - user configured" too (positions are
        # distinct), so lowering once swaps the two and lands exactly on the target.
        direction = "raise-priority" if position > target else "lower-priority"
        admin.post(f"/authentication/executions/{step['id']}/{direction}", {})
        moves += 1
        if moves > len(ids):
            print("[WARN] browser flow: could not place 'Condition - credential'; leaving its position", flush=True)
            break
    if moves:
        changed(f"browser flow: moved 'Condition - credential' after 'Condition - user configured' ({moves} step(s))")


def main() -> None:
    info(f"converging realm {REALM!r} at {KC} for {APP_BASE_URL}")
    # Browsers offer passkeys only on a secure origin: https, or localhost for a trial.
    # The policy is still converged, so passkeys work as soon as the URL moves to https.
    if urllib.parse.urlsplit(APP_BASE_URL).scheme == "http" and APP_BASE_HOST not in ("localhost", "127.0.0.1"):
        print(f"[WARN] APP_BASE_URL is http://; browsers refuse passkeys on a non-secure origin. "
              "Use https:// for passkey sign-in.", flush=True)
    wait_for_keycloak()
    admin = Admin(admin_token())
    converge_backend_client(admin)
    converge_passwordless_policy(admin)
    converge_browser_flow(admin)
    info(f"done: {len(changes)} change(s)")


if __name__ == "__main__":
    main()
