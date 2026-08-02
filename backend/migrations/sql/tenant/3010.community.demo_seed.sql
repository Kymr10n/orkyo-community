-- @supersedes-checksum: 43699ffc3277e8333948ee976eda24d466e605e853748330ee639addb3af18ca
--
-- Community demo seed data.
-- Idempotent (all inserts use ON CONFLICT DO NOTHING).
-- Provides a demo site with spaces and criteria so the application is
-- useful immediately after a clean install.

-- ── Demo site ─────────────────────────────────────────────────────────────────
INSERT INTO sites (id, code, name, created_at, updated_at)
VALUES (
    'd0000000-0000-0000-0000-000000000001',
    'DEMO',
    'Demo Office',
    NOW(), NOW()
) ON CONFLICT (id) DO NOTHING;

-- ── Demo spaces ───────────────────────────────────────────────────────────────
-- A space is a row in `resources`. It used to be two rows — one in `resources`, one in the
-- `spaces` side table — until foundation migration 1710 folded the side tables away, at which
-- point this seed's INSERT INTO spaces would have failed on every fresh install.
--
-- Reaching into a foundation table from a community migration is what made that possible: this
-- edition is a composition layer and has no business writing another repo's physical schema.
-- The columns are inlined here because the seed predates any seeding API; the durable fix is
-- for foundation to own demo seeding outright.
--
-- The @supersedes-checksum above declares the pre-fold text this replaces, so an installation
-- that already ran it keeps its history instead of failing validation. Safe because the
-- resulting rows are identical: the same ids, names and codes, in one table instead of two.
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
ON CONFLICT (id) DO NOTHING;

-- ── Demo criteria ─────────────────────────────────────────────────────────────
INSERT INTO criteria (id, name, data_type, created_at, updated_at)
VALUES
    ('d0000000-0000-0000-0002-000000000001', 'Capacity (persons)', 'Number', NOW(), NOW()),
    ('d0000000-0000-0000-0002-000000000002', 'AV Equipment',       'Boolean', NOW(), NOW()),
    ('d0000000-0000-0000-0002-000000000003', 'Whiteboard',         'Boolean', NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
