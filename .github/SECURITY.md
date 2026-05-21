# Security Policy

## Supported Versions

Security fixes are applied to the latest release of `orkyo-community`. We do not backport fixes to older major versions unless a critical severity warrants it.

| Version | Supported |
|---------|-----------|
| Latest  | ✅        |
| Older   | ❌        |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Report vulnerabilities privately via [GitHub's private vulnerability reporting](../../security/advisories/new) for this repository.

Include as much of the following as possible:

- Type of vulnerability (e.g. injection, broken access control, insecure deserialization).
- Location: file path, line number, endpoint, or relevant component.
- Steps to reproduce or a minimal proof of concept.
- Potential impact: what an attacker could achieve.

## Response Timeline

- **Acknowledgement:** within 2 business days.
- **Initial assessment:** within 5 business days.
- **Fix or mitigation:** timeline communicated after initial assessment, depending on severity.

## Scope

This policy covers the `orkyo-community` application. Vulnerabilities in the shared domain library should be reported to [`orkyo-foundation`](https://github.com/Kymr10n/orkyo-foundation/security/advisories/new) instead.

Security-relevant components in this repo include:

- Embedded Keycloak configuration and Dockerfile (`backend/keycloak/`)
- API authentication and authorisation middleware (`backend/api/`)
- Self-hosted release bundle (`release/`)
- Database migrations (`backend/migrations/`)
