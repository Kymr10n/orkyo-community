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
INSERT INTO spaces (id, site_id, name, code, is_physical, geometry, created_at, updated_at)
VALUES
    ('d0000000-0000-0000-0001-000000000001', 'd0000000-0000-0000-0000-000000000001',
     'Meeting Room A', 'MR-A', true, '{}'::jsonb, NOW(), NOW()),
    ('d0000000-0000-0000-0001-000000000002', 'd0000000-0000-0000-0000-000000000001',
     'Meeting Room B', 'MR-B', true, '{}'::jsonb, NOW(), NOW()),
    ('d0000000-0000-0000-0001-000000000003', 'd0000000-0000-0000-0000-000000000001',
     'Open Workspace', 'OW-1', true, '{}'::jsonb, NOW(), NOW()),
    ('d0000000-0000-0000-0001-000000000004', 'd0000000-0000-0000-0000-000000000001',
     'Focus Booth', 'FB-1', true, '{}'::jsonb, NOW(), NOW())
ON CONFLICT (id) DO NOTHING;

-- ── Demo criteria ─────────────────────────────────────────────────────────────
INSERT INTO criteria (id, name, data_type, created_at, updated_at)
VALUES
    ('d0000000-0000-0000-0002-000000000001', 'Capacity (persons)', 'Number', NOW(), NOW()),
    ('d0000000-0000-0000-0002-000000000002', 'AV Equipment',       'Boolean', NOW(), NOW()),
    ('d0000000-0000-0000-0002-000000000003', 'Whiteboard',         'Boolean', NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
