-- @migration-class: data
--
-- Restore the demo spaces on fresh installs, now that no resource type is built in.
--
-- Foundation migration 1880 deletes the formerly seeded `space` type when nothing
-- references it. On a fresh install every foundation migration runs before this repo's
-- 3010 demo seed, so 3010 finds no `space` type and silently skips its demo resources.
-- Existing installs are unaffected: their demo resources reference the type, 1880 keeps it,
-- and the guards below make this migration a no-op.
--
-- The type is created only when the demo site exists, no demo resource exists, and no
-- `space` type exists — i.e. exactly the fresh-install gap. Values match the 1880 revert
-- (the 1300 row with the flags later foundation migrations gave it).
--
-- Classification: data
-- Description: Ensure the demo space type, then re-run 3010's demo resource insert
-- Rollback:
--   DELETE FROM public.resources WHERE id IN (
--     'd0000000-0000-0000-0001-000000000001','d0000000-0000-0000-0001-000000000002',
--     'd0000000-0000-0000-0001-000000000003','d0000000-0000-0000-0001-000000000004');
--   (The space type row is indistinguishable from a tenant-created one; leave it.)

INSERT INTO public.resource_types
    (key, display_name, display_name_plural, icon,
     is_system, is_active, has_geometry, has_directory_profile, single_group_membership)
SELECT 'space', 'Space', 'Spaces', 'Box', false, true, true, false, true
WHERE EXISTS (
        SELECT 1 FROM public.sites
        WHERE id = 'd0000000-0000-0000-0000-000000000001'::uuid)
  AND NOT EXISTS (
        SELECT 1 FROM public.resources
        WHERE id IN (
            'd0000000-0000-0000-0001-000000000001'::uuid,
            'd0000000-0000-0000-0001-000000000002'::uuid,
            'd0000000-0000-0000-0001-000000000003'::uuid,
            'd0000000-0000-0000-0001-000000000004'::uuid))
ON CONFLICT (key) DO NOTHING;

-- 3010's demo resource insert, unchanged. Still idempotent; still a no-op when the
-- `space` type is absent (an install whose admin removed it stays clean).
INSERT INTO resources (
    id, resource_type_id, name, allocation_mode, base_availability_percent, is_active,
    home_site_id, cross_site_allowed, code, is_physical, geometry, created_at, updated_at)
SELECT
    r.id,
    rt.id,
    r.name,
    'Exclusive',
    100,
    true,
    'd0000000-0000-0000-0000-000000000001'::uuid,
    -- A space cannot travel; this is what the scheduler reads to know its site is fixed.
    false,
    r.code,
    true,
    '{}'::jsonb,
    NOW(), NOW()
FROM (VALUES
    ('d0000000-0000-0000-0001-000000000001'::uuid, 'Meeting Room A',  'MR-A'),
    ('d0000000-0000-0000-0001-000000000002'::uuid, 'Meeting Room B',  'MR-B'),
    ('d0000000-0000-0000-0001-000000000003'::uuid, 'Open Workspace',  'OW-1'),
    ('d0000000-0000-0000-0001-000000000004'::uuid, 'Focus Booth',     'FB-1')
) AS r(id, name, code)
CROSS JOIN (SELECT id FROM resource_types WHERE key = 'space') rt
WHERE EXISTS (
        SELECT 1 FROM public.sites
        WHERE id = 'd0000000-0000-0000-0000-000000000001'::uuid)
ON CONFLICT (id) DO NOTHING;
