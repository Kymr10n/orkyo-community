-- Community single-tenant seed.
--
-- Foundation provides real `tenants` and `tenant_memberships` tables (1080, 1090).
-- Community has exactly ONE tenant: itself. This migration:
--   1. Inserts the single tenant row with the well-known id used throughout the codebase.
--   2. Installs a trigger that auto-grants every new user an admin membership in that tenant,
--      preserving the "every community user is automatically a member" behaviour the
--      previous compat-view approach (3020/3021) provided.
--
-- Classification: safe
-- Description: Seed the single community tenant and auto-grant memberships
-- Rollback:
--   DROP TRIGGER IF EXISTS community_auto_grant_membership_trigger ON public.users;
--   DROP FUNCTION IF EXISTS public.community_auto_grant_membership();
--   DELETE FROM public.tenant_memberships WHERE tenant_id = '00000000-0000-0000-0000-000000000001';
--   DELETE FROM public.tenants WHERE id = '00000000-0000-0000-0000-000000000001';

INSERT INTO public.tenants (
    id, slug, display_name, status, db_identifier, tier, created_at, updated_at
) VALUES (
    '00000000-0000-0000-0000-000000000001'::uuid,
    'community',
    'Orkyo Community',
    'active',
    'community',
    2,
    now(),
    now()
);

-- Backfill memberships for any users that already exist (typical fresh install: zero).
INSERT INTO public.tenant_memberships (user_id, tenant_id, role, status, created_at, updated_at)
SELECT u.id, '00000000-0000-0000-0000-000000000001'::uuid, 'admin', 'active', now(), now()
FROM public.users u
ON CONFLICT (user_id, tenant_id) DO NOTHING;

-- Auto-grant admin membership when a user is created.
CREATE OR REPLACE FUNCTION public.community_auto_grant_membership()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO public.tenant_memberships (user_id, tenant_id, role, status)
    VALUES (NEW.id, '00000000-0000-0000-0000-000000000001'::uuid, 'admin', 'active')
    ON CONFLICT (user_id, tenant_id) DO NOTHING;
    RETURN NEW;
END;
$$;

CREATE TRIGGER community_auto_grant_membership_trigger
    AFTER INSERT ON public.users
    FOR EACH ROW EXECUTE FUNCTION public.community_auto_grant_membership();
