-- Membership status history, administrator term history, and member activity registration.
USE ITClubOrgDB;

CREATE TABLE IF NOT EXISTS member_status_history (
    history_id INT AUTO_INCREMENT PRIMARY KEY,
    member_id VARCHAR(20) NOT NULL,
    status VARCHAR(20) NOT NULL,
    effective_from DATETIME NOT NULL,
    effective_to DATETIME NULL,
    reason VARCHAR(500) NULL,
    INDEX ix_member_status_history_member (member_id, effective_from),
    CONSTRAINT fk_member_status_history_member FOREIGN KEY (member_id) REFERENCES members(member_id)
        ON DELETE CASCADE
);

INSERT INTO member_status_history (member_id, status, effective_from)
SELECT m.member_id, m.status, COALESCE(m.date_joined, CURRENT_DATE())
FROM members m
WHERE NOT EXISTS (SELECT 1 FROM member_status_history h WHERE h.member_id = m.member_id);

CREATE TABLE IF NOT EXISTS admin_position_history (
    history_id INT AUTO_INCREMENT PRIMARY KEY,
    admin_id INT NOT NULL,
    position VARCHAR(100) NOT NULL,
    term_start DATE NOT NULL,
    term_end DATE NULL,
    INDEX ix_admin_position_history_admin (admin_id, term_start),
    CONSTRAINT fk_admin_position_history_admin FOREIGN KEY (admin_id) REFERENCES admins(admin_id)
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS activity_registrations (
    activity_id INT NOT NULL,
    member_id VARCHAR(20) NOT NULL,
    registered_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    participated TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (activity_id, member_id),
    INDEX ix_activity_registrations_member (member_id),
    CONSTRAINT fk_activity_registrations_activity FOREIGN KEY (activity_id) REFERENCES activities(activity_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_activity_registrations_member FOREIGN KEY (member_id) REFERENCES members(member_id)
        ON DELETE CASCADE
);
