"""
Superset config for local development.

Simplified vs production — no TLS, SQLite metadata DB, relaxed cookies.
All secrets come from environment variables.
"""

import os

SECRET_KEY = os.environ.get("SUPERSET_SECRET_KEY", "dev-superset-secret-not-for-production")

# SQLite — no Postgres setup required for local dev
SQLALCHEMY_DATABASE_URI = "sqlite:////app/superset_home/superset.db"

FEATURE_FLAGS = {
    "EMBEDDED_SUPERSET": True,
    "ENABLE_TEMPLATE_PROCESSING": False,
}

GUEST_TOKEN_JWT_SECRET = os.environ["SUPERSET_GUEST_TOKEN_JWT_SECRET"]
GUEST_TOKEN_JWT_ALGO = "HS256"
GUEST_TOKEN_HEADER_NAME = "X-GuestToken"
GUEST_TOKEN_JWT_EXP_SECONDS = 360
# Guest token users need the Gamma role (read-only) so FAB's @protect() allows access.
# Public role stays empty — only guest tokens (not unauthenticated requests) get access.
GUEST_ROLE_NAME = "Gamma"
# Fixed audience so tokens created by the API container (superset:8088)
# validate correctly when the browser accesses localhost:8088.
# Default falls back to WEBDRIVER_BASEURL which is http://0.0.0.0:8080/.
GUEST_TOKEN_JWT_AUDIENCE = os.environ.get("SUPERSET_PUBLIC_URL", "http://localhost:8088")

_redis_pw = os.environ.get("REDIS_PASSWORD", "")
_redis_base = f"redis://:{_redis_pw}@redis:6379" if _redis_pw else "redis://redis:6379"

CELERY_CONFIG = {
    "broker_url": f"{_redis_base}/1",
    "result_backend": f"{_redis_base}/2",
}

CACHE_CONFIG = {
    "CACHE_TYPE": "RedisCache",
    "CACHE_DEFAULT_TIMEOUT": 300,
    "CACHE_KEY_PREFIX": "superset_dev_",
    "CACHE_REDIS_URL": f"{_redis_base}/3",
}

DATA_CACHE_CONFIG = {**CACHE_CONFIG, "CACHE_KEY_PREFIX": "superset_dev_data_"}

# No TLS in local dev
ENABLE_PROXY_FIX = False
TALISMAN_ENABLED = False

WTF_CSRF_ENABLED = True
WTF_CSRF_TIME_LIMIT = None

AUTH_USER_REGISTRATION = False
PUBLIC_ROLE_LIKE = None
