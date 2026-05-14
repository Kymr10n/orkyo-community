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
-- Post-resource-model cutover: spaces.name/description live on resources.
-- Insert into resources first, then into spaces (subtype extension).
INSERT INTO resources (id, resource_type_id, name, allocation_mode, base_availability_percent, is_active, created_at, updated_at)
SELECT
    r.id,
    rt.id,
    r.name,
    'Exclusive',
    100,
    true,
    NOW(), NOW()
FROM (VALUES
    ('d0000000-0000-0000-0001-000000000001'::uuid, 'Meeting Room A'),
    ('d0000000-0000-0000-0001-000000000002'::uuid, 'Meeting Room B'),
    ('d0000000-0000-0000-0001-000000000003'::uuid, 'Open Workspace'),
    ('d0000000-0000-0000-0001-000000000004'::uuid, 'Focus Booth')
) AS r(id, name)
CROSS JOIN (SELECT id FROM resource_types WHERE key = 'space') rt
ON CONFLICT (id) DO NOTHING;

INSERT INTO spaces (id, site_id, code, is_physical, geometry, created_at, updated_at)
VALUES
    ('d0000000-0000-0000-0001-000000000001', 'd0000000-0000-0000-0000-000000000001',
     'MR-A', true, '{}'::jsonb, NOW(), NOW()),
    ('d0000000-0000-0000-0001-000000000002', 'd0000000-0000-0000-0000-000000000001',
     'MR-B', true, '{}'::jsonb, NOW(), NOW()),
    ('d0000000-0000-0000-0001-000000000003', 'd0000000-0000-0000-0000-000000000001',
     'OW-1', true, '{}'::jsonb, NOW(), NOW()),
    ('d0000000-0000-0000-0001-000000000004', 'd0000000-0000-0000-0000-000000000001',
     'FB-1', true, '{}'::jsonb, NOW(), NOW())
ON CONFLICT (id) DO NOTHING;

-- ── Demo criteria ─────────────────────────────────────────────────────────────
INSERT INTO criteria (id, name, data_type, created_at, updated_at)
VALUES
    ('d0000000-0000-0000-0002-000000000001', 'Capacity (persons)', 'Number', NOW(), NOW()),
    ('d0000000-0000-0000-0002-000000000002', 'AV Equipment',       'Boolean', NOW(), NOW()),
    ('d0000000-0000-0000-0002-000000000003', 'Whiteboard',         'Boolean', NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
