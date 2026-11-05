-- Preserve member requests and announcements when an administrator account is removed.
USE ITClubOrgDB;

ALTER TABLE announcements
    MODIFY COLUMN published_by INT NULL;

SET @registration_admin_delete_rule := (
    SELECT delete_rule
    FROM information_schema.REFERENTIAL_CONSTRAINTS
    WHERE constraint_schema = DATABASE()
      AND table_name = 'member_registration_requests'
      AND constraint_name = 'fk_registration_admin'
);
SET @sql := IF(
    @registration_admin_delete_rule IS NOT NULL
        AND @registration_admin_delete_rule <> 'SET NULL',
    'ALTER TABLE member_registration_requests DROP FOREIGN KEY fk_registration_admin',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql := IF(
    @registration_admin_delete_rule = 'SET NULL',
    'SELECT 1',
    'ALTER TABLE member_registration_requests ADD CONSTRAINT fk_registration_admin FOREIGN KEY (reviewed_by) REFERENCES admins (admin_id) ON DELETE SET NULL'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @announcement_admin_delete_rule := (
    SELECT delete_rule
    FROM information_schema.REFERENTIAL_CONSTRAINTS
    WHERE constraint_schema = DATABASE()
      AND table_name = 'announcements'
      AND constraint_name = 'fk_announcement_admin'
);
SET @sql := IF(
    @announcement_admin_delete_rule IS NOT NULL
        AND @announcement_admin_delete_rule <> 'SET NULL',
    'ALTER TABLE announcements DROP FOREIGN KEY fk_announcement_admin',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql := IF(
    @announcement_admin_delete_rule = 'SET NULL',
    'SELECT 1',
    'ALTER TABLE announcements ADD CONSTRAINT fk_announcement_admin FOREIGN KEY (published_by) REFERENCES admins (admin_id) ON DELETE SET NULL'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @member_request_admin_delete_rule := (
    SELECT delete_rule
    FROM information_schema.REFERENTIAL_CONSTRAINTS
    WHERE constraint_schema = DATABASE()
      AND table_name = 'member_requests'
      AND constraint_name = 'fk_member_request_admin'
);
SET @sql := IF(
    @member_request_admin_delete_rule IS NOT NULL
        AND @member_request_admin_delete_rule <> 'SET NULL',
    'ALTER TABLE member_requests DROP FOREIGN KEY fk_member_request_admin',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql := IF(
    @member_request_admin_delete_rule = 'SET NULL',
    'SELECT 1',
    'ALTER TABLE member_requests ADD CONSTRAINT fk_member_request_admin FOREIGN KEY (reviewed_by) REFERENCES admins (admin_id) ON DELETE SET NULL'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
