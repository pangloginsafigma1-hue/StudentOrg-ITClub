-- Add division ownership and administrator roles to the ITClubOrgDB database only.
-- Existing member/activity rows remain unassigned (division_id NULL) and are
-- available only to SuperAdmin accounts until explicitly assigned.
USE ITClubOrgDB;

CREATE TABLE IF NOT EXISTS divisions (
    division_id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    is_active TINYINT(1) NOT NULL DEFAULT 1
);

INSERT INTO divisions (name) VALUES
    ('Software'),
    ('Hardware'),
    ('Multimedia & Arts')
ON DUPLICATE KEY UPDATE is_active = 1;

SET @has_role := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'admins' AND COLUMN_NAME = 'role'
);
SET @sql := IF(@has_role = 0,
    'ALTER TABLE admins ADD COLUMN role VARCHAR(30) NOT NULL DEFAULT ''SuperAdmin''',
    'SELECT ''admins.role already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_admin_division := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'admins' AND COLUMN_NAME = 'division_id'
);
SET @sql := IF(@has_admin_division = 0,
    'ALTER TABLE admins ADD COLUMN division_id INT NULL',
    'SELECT ''admins.division_id already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_admin_active := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'admins' AND COLUMN_NAME = 'is_active'
);
SET @sql := IF(@has_admin_active = 0,
    'ALTER TABLE admins ADD COLUMN is_active TINYINT(1) NOT NULL DEFAULT 1',
    'SELECT ''admins.is_active already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_member_division := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'members' AND COLUMN_NAME = 'division_id'
);
SET @sql := IF(@has_member_division = 0,
    'ALTER TABLE members ADD COLUMN division_id INT NULL',
    'SELECT ''members.division_id already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_activity_division := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'activities' AND COLUMN_NAME = 'division_id'
);
SET @sql := IF(@has_activity_division = 0,
    'ALTER TABLE activities ADD COLUMN division_id INT NULL',
    'SELECT ''activities.division_id already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_admin_index := (
    SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'admins' AND INDEX_NAME = 'ix_admins_division'
);
SET @sql := IF(@has_admin_index = 0,
    'CREATE INDEX ix_admins_division ON admins (division_id)',
    'SELECT ''ix_admins_division already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_member_index := (
    SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'members' AND INDEX_NAME = 'ix_members_division'
);
SET @sql := IF(@has_member_index = 0,
    'CREATE INDEX ix_members_division ON members (division_id)',
    'SELECT ''ix_members_division already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_activity_index := (
    SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'activities' AND INDEX_NAME = 'ix_activities_division'
);
SET @sql := IF(@has_activity_index = 0,
    'CREATE INDEX ix_activities_division ON activities (division_id)',
    'SELECT ''ix_activities_division already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
