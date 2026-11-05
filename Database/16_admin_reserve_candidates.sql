-- Store optional succession candidates independently of active administrator accounts.
USE ITClubOrgDB;

CREATE TABLE IF NOT EXISTS admin_reserve_candidates (
    slot_key VARCHAR(50) NOT NULL PRIMARY KEY,
    member_id VARCHAR(20) NOT NULL UNIQUE,
    updated_by INT NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_admin_reserve_candidates_member
        FOREIGN KEY (member_id) REFERENCES members (member_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_admin_reserve_candidates_admin
        FOREIGN KEY (updated_by) REFERENCES admins (admin_id)
        ON DELETE SET NULL
);
