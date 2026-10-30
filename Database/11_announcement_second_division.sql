-- Allow an announcement to target up to two divisions.
USE ITClubOrgDB;

SELECT COUNT(*) INTO @has_announcement_division_2
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'announcements'
  AND column_name = 'division_id_2';
SET @add_announcement_division_2 = IF(
    @has_announcement_division_2 = 0,
    'ALTER TABLE announcements ADD COLUMN division_id_2 INT NULL AFTER division_id',
    'SELECT 1');
PREPARE add_announcement_division_2_stmt FROM @add_announcement_division_2;
EXECUTE add_announcement_division_2_stmt;
DEALLOCATE PREPARE add_announcement_division_2_stmt;
