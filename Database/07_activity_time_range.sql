-- Add full activity start and end timestamps while preserving activity_date for compatibility.
-- Run against ITClubOrgDB after 04_divisions.sql. This migration is safe to rerun.
USE ITClubOrgDB;

SELECT COUNT(*) INTO @has_activity_start_at
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'activities'
  AND column_name = 'activity_start_at';
SET @add_activity_start_at = IF(
    @has_activity_start_at = 0,
    'ALTER TABLE activities ADD COLUMN activity_start_at DATETIME NULL AFTER activity_date',
    'SELECT 1');
PREPARE add_activity_start_at_stmt FROM @add_activity_start_at;
EXECUTE add_activity_start_at_stmt;
DEALLOCATE PREPARE add_activity_start_at_stmt;

SELECT COUNT(*) INTO @has_activity_end_at
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'activities'
  AND column_name = 'activity_end_at';
SET @add_activity_end_at = IF(
    @has_activity_end_at = 0,
    'ALTER TABLE activities ADD COLUMN activity_end_at DATETIME NULL AFTER activity_start_at',
    'SELECT 1');
PREPARE add_activity_end_at_stmt FROM @add_activity_end_at;
EXECUTE add_activity_end_at_stmt;
DEALLOCATE PREPARE add_activity_end_at_stmt;

UPDATE activities
SET activity_start_at = COALESCE(activity_start_at, CAST(activity_date AS DATETIME)),
    activity_end_at = COALESCE(
        activity_end_at,
        DATE_ADD(CAST(activity_date AS DATETIME), INTERVAL 1 HOUR))
WHERE activity_id > 0
  AND (activity_start_at IS NULL OR activity_end_at IS NULL);

ALTER TABLE activities
    MODIFY COLUMN activity_start_at DATETIME NOT NULL,
    MODIFY COLUMN activity_end_at DATETIME NOT NULL;
