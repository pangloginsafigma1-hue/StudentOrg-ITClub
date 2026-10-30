-- Align existing member data with the single-course and position lookups.
-- Run against ITClubOrgDB after 05_member_portal.sql.
USE ITClubOrgDB;

UPDATE members
SET course = 'BSIT'
WHERE CAST(course AS BINARY) <> CAST('BSIT' AS BINARY)
  AND member_id >= '';

UPDATE members
SET position = 'Division Head'
WHERE LOWER(TRIM(position)) = 'president'
  AND member_id >= '';

UPDATE members
SET position = 'Member'
WHERE position NOT IN ('Division Head', 'Member')
  AND member_id >= '';

UPDATE member_registration_requests
SET course = 'BSIT'
WHERE CAST(course AS BINARY) <> CAST('BSIT' AS BINARY)
  AND member_id >= '';
