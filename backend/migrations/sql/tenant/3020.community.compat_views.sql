-- Compatibility views that satisfy SaaS-shaped foundation queries (SessionService,
-- ContextEnrichmentMiddleware) in the single-tenant community model.
--
-- Community has one "tenant" (itself) and every user is always a member.
-- These views synthesize that shape from the community's existing tables so
-- the shared foundation code path works without modification.

CREATE VIEW public.tenants AS
SELECT
    '00000000-0000-0000-0000-000000000001'::uuid    AS id,
    'community'::varchar(255)                        AS slug,
    'Orkyo Community'::varchar(255)                  AS display_name,
    'active'::varchar(30)                            AS status,
    NULL::uuid                                       AS owner_user_id,
    2::int                                           AS tier,
    NULL::varchar                                    AS suspension_reason,
    NULL::timestamptz                                AS suspended_at;

-- Every user in the community is an active owner of the single tenant.
CREATE VIEW public.tenant_memberships AS
SELECT
    gen_random_uuid()                                AS id,
    u.id                                             AS user_id,
    '00000000-0000-0000-0000-000000000001'::uuid    AS tenant_id,
    'owner'::varchar(50)                             AS role,
    'active'::varchar(30)                            AS status
FROM public.users u;
