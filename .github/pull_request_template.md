## Summary
<!-- 1–3 bullets describing what changed and why. Focus on the why; the diff shows the what. -->

## Type of change
- [ ] Backend (api / worker / migrator / src)
- [ ] Frontend
- [ ] Migration (SQL)
- [ ] Keycloak (embedded provider)
- [ ] Release bundle (release/)
- [ ] CI/CD
- [ ] Documentation only

## Placement check
<!-- Per the foundation-placement rule: code with a SaaS analogue belongs in orkyo-foundation. -->
- [ ] Change is Community-specific (single-tenant behavior with no SaaS analogue)
- [ ] Change should ideally live in foundation but is being landed in Community for [reason]
- [ ] N/A (docs / CI / infra-only)

## Migration classification (if a SQL migration is part of this PR)
- [ ] `none`
- [ ] `expand` (additive, rollback-safe)
- [ ] `data` (backfill/transform, rollback-safe)
- [ ] `contract` (destructive — requires `approve_unsafe_migration` in deploy)
- [ ] N/A

See `orkyo-infra/docs/migrations/classification.md`.

## Test plan
- [ ] Unit tests added / updated
- [ ] Integration tests pass locally
- [ ] Self-hosted release bundle smoke-tested (for release/ changes)
