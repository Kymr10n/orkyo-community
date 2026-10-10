#!/usr/bin/env bash
# keycloak-converge-check.sh — prove release/scripts/keycloak-converge.py against a real Keycloak.
#
# Simulates an EXISTING installation: the realm is imported from an older shape of
# release/config/keycloak/realm.json (an old APP_BASE_URL, an old client secret and no
# WebAuthn Passwordless policy), then the converger runs with the new .env values. It must
#   1. exit 0 and report changes,
#   2. leave the realm with the new secret, the new redirect URIs beside the old ones, the
#      passkey policy, the "Condition - credential" step Required in the browser flow, and the
#      orkyo-password-check client (password grant only, no API audience) while orkyo-backend
#      refuses the password grant,
#   3. report zero changes on a second run.
# Uses the upstream Keycloak image, the same version foundation's image is built from, and the
# python image compose.yml pins. Needs Docker and a free local port (KC_CHECK_PORT, 18089).
set -Eeuo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
KC_IMAGE="${KC_IMAGE:-quay.io/keycloak/keycloak:26.7.5@sha256:37dbaf6f0722c9ec246335f36e1ef8b2e6cb960f7c27e0d8c615121a3d475a85}"
PY_IMAGE="$(grep -oE 'image: python:3[^ ]+' "$ROOT/release/compose.yml" | head -1 | cut -d' ' -f2)"
PORT="${KC_CHECK_PORT:-18089}"
ID="kcc-$$"
NET="$ID-net"
WORK="$(mktemp -d)"

OLD_URL="http://old.example.test"
NEW_URL="https://orkyo.example.test:8443"
OLD_SECRET="old-secret-from-first-boot"
NEW_SECRET="new-secret-from-env-$$"
CHECK_SECRET="check-client-from-env-$$"

cleanup() {
  docker rm -f "$ID-kc" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  rm -rf "${WORK:?}"
}
trap cleanup EXIT

fail() { echo "::error::$*" >&2; exit 1; }

# ── An old realm: rendered for the old URL and secret, before passkeys existed ───────────
mkdir -p "$WORK/import"
python3 - "$ROOT/release/config/keycloak/realm.json" "$WORK/import/realm.json" "$OLD_URL" "$OLD_SECRET" <<'PY'
import json, sys
src, dst, url, secret = sys.argv[1:]
text = (open(src).read().replace("${APP_BASE_URL}", url).replace("${APP_BASE_HOST}", "old.example.test")
        .replace("${KEYCLOAK_BACKEND_CLIENT_SECRET}", secret).replace("${KEYCLOAK_PASSWORD_CHECK_CLIENT_SECRET}", secret))
realm = json.loads(text)
# An install from before the password-check client: no such client, and the password grant on
# orkyo-backend.
realm["clients"] = [c for c in realm["clients"] if c["clientId"] != "orkyo-password-check"]
for c in realm["clients"]:
    if c["clientId"] == "orkyo-backend":
        c["directAccessGrantsEnabled"] = True
for key in [k for k in realm if k.startswith("webAuthnPolicyPasswordless")]:
    del realm[key]
realm.pop("loginTheme", None)  # the theme lives in the Orkyo image, not in upstream Keycloak
json.dump(realm, open(dst, "w"), indent=2)
PY

docker network create "$NET" >/dev/null
docker run -d --name "$ID-kc" --network "$NET" -p "${PORT}:8080" \
  -e KC_BOOTSTRAP_ADMIN_USERNAME=admin -e KC_BOOTSTRAP_ADMIN_PASSWORD=admin \
  -e KC_HTTP_ENABLED=true \
  -v "$WORK/import:/opt/keycloak/data/import:ro" \
  "$KC_IMAGE" start-dev --import-realm >/dev/null

echo "waiting for Keycloak to import the old realm"
for _ in $(seq 1 90); do
  if curl -sf -o /dev/null "http://localhost:${PORT}/realms/orkyo-community"; then ready=1; break; fi
  sleep 2
done
[[ "${ready:-}" == 1 ]] || { docker logs "$ID-kc" | tail -40; fail "Keycloak did not import the realm"; }

# Keycloak 26.7's stock browser flow already carries "Condition - credential", and a built-in
# flow cannot be edited. An operator who copied the flow before passkeys existed has a custom
# flow without the step, so build exactly that: copy the stock flow, bind it, remove the step,
# and make the converger put it back.
echo "binding a copied browser flow without 'Condition - credential' (an operator's pre-passkey copy)"
python3 - "$PORT" <<'PY'
import json, sys, urllib.parse, urllib.request
port = sys.argv[1]; base = f"http://localhost:{port}"
def call(method, path, token=None, form=None):
    data = urllib.parse.urlencode(form).encode() if form else None
    req = urllib.request.Request(base + path, data=data, method=method)
    if token: req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req) as r: return json.loads(r.read() or b"null")
token = call("POST", "/realms/master/protocol/openid-connect/token",
             form={"grant_type": "password", "client_id": "admin-cli", "username": "admin", "password": "admin"})["access_token"]
R = "/admin/realms/orkyo-community"
def call_json(method, path, token, body):
    req = urllib.request.Request(base + path, data=json.dumps(body).encode(), method=method,
                                 headers={"Content-Type": "application/json", "Authorization": f"Bearer {token}"})
    with urllib.request.urlopen(req) as r: return json.loads(r.read() or b"null")
call_json("POST", f"{R}/authentication/flows/browser/copy", token, {"newName": "browser-operator-copy"})
call_json("PUT", R, token, {"browserFlow": "browser-operator-copy"})
execs = call("GET", f"{R}/authentication/flows/browser-operator-copy/executions", token)
for e in execs:
    if e.get("providerId") == "conditional-credential":
        call("DELETE", f"{R}/authentication/executions/{e['id']}", token)
        print("removed", e["id"], "from the copied flow")
PY

converge() {
  docker run --rm --network "$NET" \
    -v "$ROOT/release/scripts/keycloak-converge.py:/converge.py:ro" \
    -v "$ROOT/release/config/keycloak/realm.json:/realm.json:ro" \
    -e KEYCLOAK_INTERNAL_URL="http://$ID-kc:8080" \
    -e KEYCLOAK_REALM=orkyo-community \
    -e KEYCLOAK_ADMIN=admin -e KEYCLOAK_ADMIN_PASSWORD=admin \
    -e APP_BASE_URL="$NEW_URL" \
    -e KEYCLOAK_BACKEND_CLIENT_ID=orkyo-backend \
    -e KEYCLOAK_BACKEND_CLIENT_SECRET="$NEW_SECRET" \
    -e KEYCLOAK_PASSWORD_CHECK_CLIENT_ID=orkyo-password-check \
    -e KEYCLOAK_PASSWORD_CHECK_CLIENT_SECRET="$CHECK_SECRET" \
    "$PY_IMAGE" python /converge.py
}

echo "== first run (existing installation)"
first="$(converge)"; echo "$first"
grep -q 'done: [1-9][0-9]* change' <<<"$first" || fail "first run reported no changes"

echo "== second run (must be a no-op)"
second="$(converge)"; echo "$second"
grep -q 'done: 0 change' <<<"$second" || fail "second run was not a no-op"

echo "== assertions through the admin API"
python3 - "$PORT" "$NEW_URL" "$NEW_SECRET" "$OLD_URL" "$CHECK_SECRET" <<'PY'
import base64, json, sys, urllib.error, urllib.parse, urllib.request
port, new_url, new_secret, old_url, check_secret = sys.argv[1:]
base = f"http://localhost:{port}"

def call(method, path, token=None, form=None):
    data = urllib.parse.urlencode(form).encode() if form else None
    req = urllib.request.Request(base + path, data=data, method=method)
    if token: req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req) as r: return json.loads(r.read() or b"null")

token = call("POST", "/realms/master/protocol/openid-connect/token",
             form={"grant_type": "password", "client_id": "admin-cli", "username": "admin", "password": "admin"})["access_token"]
R = "/admin/realms/orkyo-community"
client = call("GET", f"{R}/clients?clientId=orkyo-backend", token)[0]
assert f"{new_url}/*" in client["redirectUris"], client["redirectUris"]
assert f"{old_url}/*" in client["redirectUris"], "old redirect URI was removed"
assert new_url in client["webOrigins"], client["webOrigins"]
assert f"{new_url}/*" in client["attributes"]["post.logout.redirect.uris"].split("##")
secret = call("GET", f"{R}/clients/{client['id']}/client-secret", token)["value"]
assert secret == new_secret, "secret not converged"
# The new secret must actually authenticate.
call("POST", "/realms/orkyo-community/protocol/openid-connect/token",
     form={"grant_type": "client_credentials", "client_id": "orkyo-backend", "client_secret": new_secret})

realm = call("GET", R, token)
assert realm["webAuthnPolicyPasswordlessRpId"] == "orkyo.example.test", realm.get("webAuthnPolicyPasswordlessRpId")
assert realm["webAuthnPolicyPasswordlessPasskeysEnabled"] is True
assert realm["webAuthnPolicyPasswordlessMediation"] == "conditional"

flow = realm.get("browserFlow") or "browser"
execs = call("GET", f"{R}/authentication/flows/{urllib.parse.quote(flow, safe='')}/executions", token)
sub = next(e for e in execs if e.get("authenticationFlow") and "Browser - Conditional 2FA" in e["displayName"])
children = []
for e in execs[execs.index(sub) + 1:]:
    if e["level"] <= sub["level"]: break
    if e["level"] == sub["level"] + 1: children.append(e)
ids = [c.get("providerId") for c in children]
assert "conditional-credential" in ids, ids
step = next(c for c in children if c.get("providerId") == "conditional-credential")
assert step["requirement"] == "REQUIRED", step["requirement"]
cfg = call("GET", f"{R}/authentication/config/{step['authenticationConfig']}", token)
assert cfg["config"]["credentials"] == "webauthn-passwordless", cfg
assert ids.index("conditional-credential") == ids.index("conditional-user-configured") + 1, ids

# The password-check client (orkyo-infra ADR 0008): created on the existing install, password
# grant only, no audience mapper, no full scope; orkyo-backend lost the grant.
def token_error(client_id, secret, username, password):
    form = urllib.parse.urlencode({"grant_type": "password", "client_id": client_id, "client_secret": secret,
                                   "username": username, "password": password}).encode()
    try:
        with urllib.request.urlopen(urllib.request.Request(f"{base}/realms/orkyo-community/protocol/openid-connect/token", data=form)) as r:
            return None, json.loads(r.read())
    except urllib.error.HTTPError as e:
        body = json.loads(e.read() or b"{}")
        return body.get("error"), body

check = call("GET", f"{R}/clients?clientId=orkyo-password-check", token)
assert check, "orkyo-password-check was not created"
check = check[0]
assert check["fullScopeAllowed"] is False and check["directAccessGrantsEnabled"] is True, check
assert not any(m.get("protocolMapper") == "oidc-audience-mapper" for m in check.get("protocolMappers") or []), check
assert client["directAccessGrantsEnabled"] is False, "orkyo-backend still allows the password grant"
assert token_error("orkyo-password-check", check_secret, "no-such-user", "x")[0] == "invalid_grant"
assert token_error("orkyo-backend", new_secret, "no-such-user", "x")[0] == "unauthorized_client"

# A real user's token from the check client carries no API audience.
req = urllib.request.Request(base + f"{R}/users", method="POST",
    data=json.dumps({"username": "probe", "enabled": True, "email": "probe@example.test", "emailVerified": True,
                     "firstName": "P", "lastName": "R",
                     "credentials": [{"type": "password", "value": "Probe-value-1", "temporary": False}]}).encode(),
    headers={"Content-Type": "application/json", "Authorization": f"Bearer {token}"})
urllib.request.urlopen(req)
err, ok = token_error("orkyo-password-check", check_secret, "probe@example.test", "Probe-value-1")
assert err is None, ok
part = ok["access_token"].split(".")[1]; part += "=" * (-len(part) % 4)
aud = json.loads(base64.urlsafe_b64decode(part)).get("aud", [])
aud = [aud] if isinstance(aud, str) else aud
assert not {"orkyo-backend", "account"} & set(aud), aud
print("all assertions passed:", ids, "| password-check client: no API audience")
PY
echo "keycloak-converge-check: OK"
