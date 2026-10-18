-- @migration-class: contract
-- Community is invitation-only.
--
-- 3020 installed a trigger that granted every new users row an admin membership of the
-- single tenant. Together with identity auto-creation on first sign-in, any Keycloak identity
-- that reached the API became an admin. The API now rejects identities nobody invited
-- (AllowSelfRegistration = false in api/Program.cs) and the invitation carries the role, so
-- the trigger goes.
--
-- A fresh install has no users row for the shipped admin account yet, and an identity with no
-- row is rejected. The row is created here, on an empty users table only, so the first sign-in
-- links to it. The address is the one in release/config/keycloak/realm.json. An install that
-- already has users keeps its memberships and gets no extra row.
--
-- Rollback: re-run the CREATE FUNCTION and CREATE TRIGGER statements from 3020.

DROP TRIGGER IF EXISTS community_auto_grant_membership_trigger ON public.users;
DROP FUNCTION IF EXISTS public.community_auto_grant_membership();

WITH shipped_admin AS (
    INSERT INTO public.users (email, display_name, status, created_at, updated_at)
    SELECT 'admin@example.com', 'Admin User', 'active', now(), now()
    WHERE NOT EXISTS (SELECT 1 FROM public.users)
    RETURNING id
)
INSERT INTO public.tenant_memberships (user_id, tenant_id, role, status, created_at, updated_at)
SELECT id, '00000000-0000-0000-0000-000000000001'::uuid, 'admin', 'active', now(), now()
FROM shipped_admin;
