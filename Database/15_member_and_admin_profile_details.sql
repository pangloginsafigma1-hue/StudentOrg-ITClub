-- Add profile details collected by membership and administrator forms.
USE ITClubOrgDB;

ALTER TABLE admins MODIFY COLUMN email VARCHAR(254) NOT NULL;

SET @has_member_email := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'members'
      AND COLUMN_NAME = 'email'
);
SET @sql := IF(@has_member_email = 0,
    'ALTER TABLE members ADD COLUMN email VARCHAR(254) NULL AFTER student_name',
    'SELECT ''members.email already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_admin_student_id := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'admins'
      AND COLUMN_NAME = 'student_id'
);
SET @sql := IF(@has_admin_student_id = 0,
    'ALTER TABLE admins ADD COLUMN student_id VARCHAR(20) NULL AFTER email',
    'SELECT ''admins.student_id already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_admin_course := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'admins'
      AND COLUMN_NAME = 'course'
);
SET @sql := IF(@has_admin_course = 0,
    'ALTER TABLE admins ADD COLUMN course VARCHAR(50) NULL AFTER student_id',
    'SELECT ''admins.course already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_admin_year_level := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'admins'
      AND COLUMN_NAME = 'year_level'
);
SET @sql := IF(@has_admin_year_level = 0,
    'ALTER TABLE admins ADD COLUMN year_level VARCHAR(20) NULL AFTER course',
    'SELECT ''admins.year_level already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_admin_position := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'admins'
      AND COLUMN_NAME = 'position'
);
SET @sql := IF(@has_admin_position = 0,
    'ALTER TABLE admins ADD COLUMN position VARCHAR(50) NULL AFTER year_level',
    'SELECT ''admins.position already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_admin_date_joined := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'admins'
      AND COLUMN_NAME = 'date_joined'
);
SET @sql := IF(@has_admin_date_joined = 0,
    'ALTER TABLE admins ADD COLUMN date_joined DATE NULL AFTER position',
    'SELECT ''admins.date_joined already exists'' AS info');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
