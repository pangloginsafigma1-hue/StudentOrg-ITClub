-- Create the database's base tables for a fresh installation.
-- Existing tables and their data are left unchanged; this does not import legacy records.
CREATE DATABASE IF NOT EXISTS ITClubOrgDB
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

USE ITClubOrgDB;

CREATE TABLE IF NOT EXISTS admins (
    admin_id INT AUTO_INCREMENT PRIMARY KEY,
    full_name VARCHAR(100) NOT NULL,
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(100) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    photo LONGBLOB NULL
);

CREATE TABLE IF NOT EXISTS members (
    member_id VARCHAR(20) PRIMARY KEY,
    student_name VARCHAR(100) NOT NULL,
    course VARCHAR(50) NOT NULL,
    year_level VARCHAR(20) NOT NULL,
    position VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL,
    date_joined DATE NOT NULL,
    INDEX ix_members_student_name (student_name)
);

CREATE TABLE IF NOT EXISTS activities (
    activity_id INT AUTO_INCREMENT PRIMARY KEY,
    activity_name VARCHAR(150) NOT NULL,
    location VARCHAR(200) NOT NULL,
    activity_date DATE NOT NULL,
    status VARCHAR(30) NOT NULL,
    description TEXT NOT NULL,
    attendance_done TINYINT(1) NOT NULL DEFAULT 0,
    INDEX ix_activities_date (activity_date)
);

CREATE TABLE IF NOT EXISTS attendance (
    activity_id INT NOT NULL,
    member_id VARCHAR(20) NOT NULL,
    is_present TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (activity_id, member_id),
    CONSTRAINT fk_attendance_activity
        FOREIGN KEY (activity_id) REFERENCES activities (activity_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_attendance_member
        FOREIGN KEY (member_id) REFERENCES members (member_id)
        ON DELETE CASCADE
);
