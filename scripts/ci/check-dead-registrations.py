#!/usr/bin/env python3
"""check-dead-registrations.py — fail when a DI registration has no consumer.

A service that is registered (``services.AddScoped<IFoo, Foo>()``) but never
injected anywhere is dead code that compiles, passes its own unit tests and
survives coverage gates. This check reads every composition root in the
current repo, collects the interface types it registers, and requires each one
to appear in production code outside a registration line: a constructor or
handler parameter, ``GetRequiredService<IFoo>()``, ``IEnumerable<IFoo>``, and
so on.

Consumers can live in a sibling repo (foundation registers a seam that only
saas injects), so the sibling checkouts ``../orkyo-foundation``,
``../orkyo-saas`` and ``../orkyo-community`` are scanned too when present.
When a sibling is missing the result cannot be trusted, so the check prints
its findings and exits 0 (advisory). With every sibling present, or with
``--strict``, findings exit 1.

Known cross-boundary seams that no repo injects directly go in
``scripts/ci/dead-registrations.allow`` (one ``IName<TAB>reason`` per line).

Usage: python3 scripts/ci/check-dead-registrations.py [--strict] [--report]
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

SIBLINGS = ("orkyo-foundation", "orkyo-saas", "orkyo-community")
TEST_PATH_MARKERS = ("/tests/", "/testsupport/", "seeding-tests", "/e2e/", "/obj/", "/bin/")
ALLOWLIST = Path("scripts/ci/dead-registrations.allow")

# services.AddScoped<IFoo, Foo>() / AddSingleton<IFoo>(sp => ...) / TryAddScoped<IFoo, Foo>()
# / AddHttpClient<IFoo, Foo>(). Only interface-shaped first type arguments (I + capital)
# are checked; concrete registrations are their own consumer.
REGISTRATION = re.compile(
    r"\b(?:Try)?Add(?:Scoped|Singleton|Transient|HttpClient)\s*<\s*(I[A-Z]\w*)\s*[,>]"
)
REGISTRATION_LINE = re.compile(r"\b(?:Try)?Add\w*\s*<|\bRemoveAll\s*<|\bReplace\s*\(|ServiceDescriptor")


def is_test_path(path: Path) -> bool:
    text = "/" + path.as_posix() + "/"
    return any(marker in text for marker in TEST_PATH_MARKERS)


def production_cs_files(root: Path) -> list[Path]:
    return [p for p in root.rglob("*.cs") if not is_test_path(p.relative_to(root))]


def load_allowlist() -> dict[str, str]:
    allow: dict[str, str] = {}
    if ALLOWLIST.exists():
        for raw in ALLOWLIST.read_text(encoding="utf-8").splitlines():
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            name, _, reason = line.partition("\t")
            allow[name.strip()] = reason.strip()
    return allow


def find_registrations(files: list[Path]) -> dict[str, list[str]]:
    found: dict[str, list[str]] = {}
    for path in files:
        try:
            lines = path.read_text(encoding="utf-8").splitlines()
        except UnicodeDecodeError:
            continue
        for number, line in enumerate(lines, start=1):
            for match in REGISTRATION.finditer(line):
                found.setdefault(match.group(1), []).append(f"{path.as_posix()}:{number}")
    return found


def consumer_pattern(name: str) -> re.Pattern[str]:
    return re.compile(rf"\b{re.escape(name)}\b")


def is_consumer_line(name: str, line: str) -> bool:
    stripped = line.strip()
    if stripped.startswith("using ") or stripped.startswith("//"):
        return False
    if REGISTRATION_LINE.search(line):
        return False
    if re.search(rf"\binterface\s+{re.escape(name)}\b", line):
        return False
    # `class Foo : IFoo`, `record Bar(...) : IBaz, IFoo` — an implementation, not a consumer.
    if re.search(r"\b(?:class|record|struct)\b", line) and re.search(rf"[:,]\s*{re.escape(name)}\b", line):
        return False
    return True


def find_consumers(names: set[str], files: list[Path]) -> set[str]:
    patterns = {name: consumer_pattern(name) for name in names}
    consumed: set[str] = set()
    for path in files:
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        pending = [n for n in names if n not in consumed and patterns[n].search(text)]
        if not pending:
            continue
        for line in text.splitlines():
            for name in pending:
                if name in consumed:
                    continue
                if patterns[name].search(line) and is_consumer_line(name, line):
                    consumed.add(name)
    return consumed


def main(argv: list[str]) -> int:
    strict = "--strict" in argv
    report_only = "--report" in argv
    repo = Path.cwd()
    parent = repo.resolve().parent

    roots = [repo]
    missing: list[str] = []
    for sibling in SIBLINGS:
        candidate = parent / sibling
        if candidate.resolve() == repo.resolve():
            continue
        if candidate.is_dir():
            roots.append(candidate)
        else:
            missing.append(sibling)

    own_files = production_cs_files(repo)
    registrations = find_registrations(own_files)
    if not registrations:
        print("check-dead-registrations: no interface registrations found")
        return 0

    all_files = own_files + [f for root in roots[1:] for f in production_cs_files(root)]
    consumed = find_consumers(set(registrations), all_files)
    allow = load_allowlist()

    dead = sorted(n for n in registrations if n not in consumed and n not in allow)
    stale_allow = sorted(n for n in allow if n in consumed)

    for name in dead:
        for site in registrations[name]:
            print(f"{site}: {name} is registered but nothing injects it")
    for name in stale_allow:
        print(f"{ALLOWLIST}: {name} is allowlisted but now has a consumer — remove the row")

    advisory = bool(missing) and not strict
    if missing:
        print(f"check-dead-registrations: sibling repo(s) not checked out: {', '.join(missing)}"
              f" — {'advisory only' if advisory else 'strict mode requested anyway'}")

    findings = len(dead) + len(stale_allow)
    if findings == 0:
        print(f"check-dead-registrations: OK ({len(registrations)} registrations, {len(allow)} allowlisted)")
        return 0
    if advisory or report_only:
        print(f"check-dead-registrations: {findings} finding(s), not failing")
        return 0
    print(f"check-dead-registrations: {findings} finding(s)")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
