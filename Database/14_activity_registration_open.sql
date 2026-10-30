-- Add an explicit member-registration switch to activities.
USE ITClubOrgDB;

SET @column_exists := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'activities'
      AND COLUMN_NAME = 'registration_open'
);
SET @sql := IF(@column_exists = 0,
    'ALTER TABLE activities ADD COLUMN registration_open TINYINT(1) NOT NULL DEFAULT 1',
    'SELECT ''activities.registration_open already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
