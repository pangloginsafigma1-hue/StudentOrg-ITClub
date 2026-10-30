-- Add member portal email addresses for member password recovery.
-- Run against ITClubOrgDB after 05_member_portal.sql. Safe to rerun.
USE ITClubOrgDB;

SELECT COUNT(*) INTO @has_portal_email
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'member_portal_accounts'
  AND column_name = 'email';
SET @add_portal_email = IF(
    @has_portal_email = 0,
    'ALTER TABLE member_portal_accounts ADD COLUMN email VARCHAR(254) NULL AFTER member_id',
    'SELECT 1');
PREPARE add_portal_email_stmt FROM @add_portal_email;
EXECUTE add_portal_email_stmt;
DEALLOCATE PREPARE add_portal_email_stmt;
