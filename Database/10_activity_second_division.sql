-- Allow an activity to target up to two divisions.
-- Run against ITClubOrgDB after 07_activity_time_range.sql. Safe to rerun.
USE ITClubOrgDB;

SELECT COUNT(*) INTO @has_activity_division_2
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'activities'
  AND column_name = 'division_id_2';
SET @add_activity_division_2 = IF(
    @has_activity_division_2 = 0,
    'ALTER TABLE activities ADD COLUMN division_id_2 INT NULL AFTER division_id',
    'SELECT 1');
PREPARE add_activity_division_2_stmt FROM @add_activity_division_2;
EXECUTE add_activity_division_2_stmt;
DEALLOCATE PREPARE add_activity_division_2_stmt;
