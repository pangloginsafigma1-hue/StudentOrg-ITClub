-- Add auditable participation details without changing existing registration behavior.
USE ITClubOrgDB;

SET @column_exists := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'activity_registrations'
      AND COLUMN_NAME = 'participation_type'
);
SET @sql := IF(@column_exists = 0,
    'ALTER TABLE activity_registrations ADD COLUMN participation_type VARCHAR(30) NULL',
    'SELECT ''activity_registrations.participation_type already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @column_exists := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'activity_registrations'
      AND COLUMN_NAME = 'notes'
);
SET @sql := IF(@column_exists = 0,
    'ALTER TABLE activity_registrations ADD COLUMN notes VARCHAR(1000) NULL',
    'SELECT ''activity_registrations.notes already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @column_exists := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'activity_registrations'
      AND COLUMN_NAME = 'recorded_by'
);
SET @sql := IF(@column_exists = 0,
    'ALTER TABLE activity_registrations ADD COLUMN recorded_by INT NULL',
    'SELECT ''activity_registrations.recorded_by already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @column_exists := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'activity_registrations'
      AND COLUMN_NAME = 'recorded_at'
);
SET @sql := IF(@column_exists = 0,
    'ALTER TABLE activity_registrations ADD COLUMN recorded_at DATETIME NULL',
    'SELECT ''activity_registrations.recorded_at already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @index_exists := (
    SELECT COUNT(*)
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'activity_registrations'
      AND INDEX_NAME = 'ix_activity_registrations_recorded_by'
);
SET @sql := IF(@index_exists = 0,
    'ALTER TABLE activity_registrations ADD INDEX ix_activity_registrations_recorded_by (recorded_by)',
    'SELECT ''ix_activity_registrations_recorded_by already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @foreign_key_exists := (
    SELECT COUNT(*)
    FROM information_schema.REFERENTIAL_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'activity_registrations'
      AND CONSTRAINT_NAME = 'fk_activity_registrations_recorded_by'
);
SET @sql := IF(@foreign_key_exists = 0,
    'ALTER TABLE activity_registrations ADD CONSTRAINT fk_activity_registrations_recorded_by FOREIGN KEY (recorded_by) REFERENCES admins(admin_id) ON DELETE SET NULL',
    'SELECT ''fk_activity_registrations_recorded_by already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
