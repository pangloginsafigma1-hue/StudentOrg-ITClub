-- Add self-registration and personal member portal access.
-- Run against ITClubOrgDB after 04_divisions.sql.
USE ITClubOrgDB;

CREATE TABLE IF NOT EXISTS member_registration_requests (
    member_id VARCHAR(20) PRIMARY KEY,
    student_name VARCHAR(100) NOT NULL,
    email VARCHAR(254) NULL,
    course VARCHAR(50) NOT NULL,
    year_level VARCHAR(20) NOT NULL,
    division_id INT NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    submitted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    reviewed_at DATETIME NULL,
    reviewed_by INT NULL,
    CONSTRAINT fk_registration_division
        FOREIGN KEY (division_id) REFERENCES divisions (division_id),
    CONSTRAINT fk_registration_admin
        FOREIGN KEY (reviewed_by) REFERENCES admins (admin_id),
    INDEX ix_registration_status (status, submitted_at)
);

CREATE TABLE IF NOT EXISTS member_portal_accounts (
    member_id VARCHAR(20) PRIMARY KEY,
    password_hash VARCHAR(255) NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_member_portal_member
        FOREIGN KEY (member_id) REFERENCES members (member_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS announcements (
    announcement_id INT AUTO_INCREMENT PRIMARY KEY,
    title VARCHAR(150) NOT NULL,
    body TEXT NOT NULL,
    division_id INT NULL,
    published_by INT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_announcement_division
        FOREIGN KEY (division_id) REFERENCES divisions (division_id),
    CONSTRAINT fk_announcement_admin
        FOREIGN KEY (published_by) REFERENCES admins (admin_id),
    INDEX ix_announcement_created (created_at)
);

CREATE TABLE IF NOT EXISTS member_requests (
    request_id INT AUTO_INCREMENT PRIMARY KEY,
    member_id VARCHAR(20) NOT NULL,
    request_type VARCHAR(30) NOT NULL,
    message VARCHAR(1000) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    submitted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    reviewed_at DATETIME NULL,
    reviewed_by INT NULL,
    CONSTRAINT fk_member_request_member
        FOREIGN KEY (member_id) REFERENCES members (member_id) ON DELETE CASCADE,
    CONSTRAINT fk_member_request_admin
        FOREIGN KEY (reviewed_by) REFERENCES admins (admin_id),
    INDEX ix_member_request_status (status, submitted_at),
    INDEX ix_member_request_member (member_id, submitted_at)
);
