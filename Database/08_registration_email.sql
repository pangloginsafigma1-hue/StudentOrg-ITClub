-- Add registration email addresses for approval notifications.
-- Run against ITClubOrgDB after 05_member_portal.sql. Safe to rerun.
USE ITClubOrgDB;

SELECT COUNT(*) INTO @has_registration_email
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'member_registration_requests'
  AND column_name = 'email';
SET @add_registration_email = IF(
    @has_registration_email = 0,
    'ALTER TABLE member_registration_requests ADD COLUMN email VARCHAR(254) NULL AFTER student_name',
    'SELECT 1');
PREPARE add_registration_email_stmt FROM @add_registration_email;
EXECUTE add_registration_email_stmt;
DEALLOCATE PREPARE add_registration_email_stmt;
