-- Fix tenant_memberships compat view: 'owner' is not a valid RoleConstants string.
-- RoleConstants.ParseRoleString only recognises 'admin', 'editor', 'viewer';
-- 'owner' maps to TenantRole.None → every endpoint returns 403.
-- All community users should be admins of the single tenant.
CREATE OR REPLACE VIEW public.tenant_memberships AS
SELECT
    gen_random_uuid()                                AS id,
    u.id                                             AS user_id,
    '00000000-0000-0000-0000-000000000001'::uuid    AS tenant_id,
    'admin'::varchar(50)                             AS role,
    'active'::varchar(30)                            AS status
FROM public.users u;
