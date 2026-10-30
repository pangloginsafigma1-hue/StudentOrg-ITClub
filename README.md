# SOMAMS - ASP.NET Core MVC

This is the phased C# / ASP.NET Core MVC migration of the SOMAMS WinForms application. The legacy desktop source is not included in this repository.

## Phase 1

- Shared responsive application shell, accessible navigation, consistent cards and tables, and SOMAMS artwork.
- Cookie-based administrator sign-in using the existing BCrypt hashes, validation messages, password visibility toggle, and remember-me cookie.
- Data-backed dashboard with member/activity totals, 12-month member and activity trends, recent member/activity lists, and upcoming activities.
- Logout confirmation modal and working navigation links for all seven original sections.
- Unconverted modules are clearly identified as upcoming instead of showing nonfunctional forms.

The original page design and interactions are based on the WinForms forms and the coding-practices guide in `Coding_Practices.pdf`. The implementation keeps HTTP handling in MVC controllers, database queries in services/repositories, and page presentation in Razor views.

The migration follows the guide's naming conventions (PascalCase for types and members, camelCase for parameters/locals, and `_camelCase` for private fields), keeps repeated layout and styling in shared files, and organizes code by MVC responsibility (`Controllers`, `Models`, `Services`, and `Views`).

## Database setup

The migrated app uses the existing tables (`admins`, `members`, `activities`, and `attendance`) in `ITClubOrgDB`. The initial migration copied the source records there without modifying `somams_db`.

To enable division-scoped access, run [`Database/04_divisions.sql`](./Database/04_divisions.sql) against `ITClubOrgDB` before starting the updated app. If you ran an earlier version of this migration, run the current file again; it is idempotent and adds newer account columns such as `admins.is_active`. This additive migration targets **only** `ITClubOrgDB`; it does not read or modify `somams_db`. Existing member and activity records remain unassigned and visible only to super admins until assigned to a division in the app.

To enable the member portal, also run [`Database/05_member_portal.sql`](./Database/05_member_portal.sql), followed by [`Database/08_registration_email.sql`](./Database/08_registration_email.sql), [`Database/09_member_portal_email.sql`](./Database/09_member_portal_email.sql), [`Database/10_activity_second_division.sql`](./Database/10_activity_second_division.sql), and [`Database/11_announcement_second_division.sql`](./Database/11_announcement_second_division.sql) when upgrading an existing database. Activities and announcements may target one or two divisions; members in either selected division can see them. New registrations use a two-step form, require an email address, and accept passwords from 8 to 72 characters. After approval, SOMAMS sends an email notification to the applicant using the configured SMTP settings; if delivery fails, the approval remains in effect and the administrator is told to contact the member. Older pending registrations without an email can still be approved, but must be notified manually. Approved portal accounts retain the registration email for member password recovery. Approval creates the member record if it does not exist, or links the portal account to an existing matching record. If the Student ID already exists but the submitted name, course, year, or division differs, an administrator must reconcile the member record before approval. Division admins can review registrations and announcements only for their assigned division.
Run [`Database/12_history_and_activity_registration.sql`](./Database/12_history_and_activity_registration.sql) after the portal migrations to add member status history, administrator position/term history, and activity registration/participation tracking. Members can register or unregister from eligible activities in the portal; authorized administrators can review registrations and record participation.
Run [`Database/13_activity_participation_details.sql`](./Database/13_activity_participation_details.sql) afterward to add participation type, optional notes, and administrator audit timestamps to registration records. Super admins and division admins scoped to the activity can update these details. Run [`Database/14_activity_registration_open.sql`](./Database/14_activity_registration_open.sql) to add the per-activity member-registration switch; existing activities remain open by default.

Configure `Email:SmtpHost`, `Email:SmtpPort`, `Email:SmtpUsername`, `Email:SmtpPassword`, `Email:FromAddress`, and `Email:EnableSsl` through user secrets or deployment environment variables to send approval notifications and password-reset codes. Do not commit SMTP credentials.

After `05_member_portal.sql`, run [`Database/06_member_data_cleanup.sql`](./Database/06_member_data_cleanup.sql) to convert existing member and registration courses to BSIT, map legacy President positions to Division Head, and normalize other member positions to Member. New membership forms offer only Division Head and Member; division administrators can add Members but cannot assign the Division Head position.

Run [`Database/07_activity_time_range.sql`](./Database/07_activity_time_range.sql) to add activity start/end timestamps. Existing activities are backfilled from their current `activity_date` with a one-hour interval beginning at midnight; update their times in Activities if the original event times are known. Super admins can create organization-wide or division-specific activities and edit recorded attendance across divisions. Once attendance is first saved, the activity status is set to **Completed**.

The member portal exposes a member's own record and attendance, upcoming organization-wide and own-division activities, and published announcements. Administrators create posts from **Announcements**; super admins can target all members or an individual division. The admin **Profile** page manages the signed-in administrator's own account. Member request tables are retained for existing data, but the Member Inbox and member request/correction surfaces are no longer available.

The shared interface uses an indigo, ivory, and gold visual system with a simplified SVG SOMAMS mark. Authentication pages use a custom responsive layout without a background image. The original badge artwork remains available as a separate source asset.

The president appoints division administrators according to the organization's bylaws and records the appointment, division, responsibilities, and review/term date. A SuperAdmin provisions the named account with least privilege; SuperAdmin is a technical custodian role, not automatically the president's title. Review and remove access during leadership handovers.

Existing administrator accounts remain active super admins after migration. Super admins can create and manage administrator accounts from **User Roles**, including division assignment, password reset, and account disabling. The page also lists approved member portal accounts and allows Super Admins to enable/disable portal access or reset member passwords. At least one active super admin must remain enabled. Disabled accounts cannot sign in. To make an existing account a division admin, use the account-management UI or update that account's `role` and `division_id` in `ITClubOrgDB` (the division IDs are in the `divisions` table). A division admin must have `role = 'DivisionAdmin'` and a valid division ID; a super admin must have `role = 'SuperAdmin'` and `division_id = NULL`. Division member records currently retain a globally unique membership/student ID and belong to one division.

For example, after replacing the username with an existing admin account, this assigns it to Software:

```sql
USE ITClubOrgDB;
UPDATE admins AS a
JOIN divisions AS d ON d.name = 'Software'
SET a.role = 'DivisionAdmin', a.division_id = d.division_id
WHERE a.username = 'SOFTWARE_ADMIN_USERNAME';
```

## Run locally

Requirements: .NET 10 SDK and MySQL Server 8.

1. Store the connection string in the project's **local-only ASP.NET Core user-secrets store** (never put passwords in `appsettings.json`). Use the host, port, database, and MySQL account from your MySQL Workbench connection; these database credentials are separate from the app's sign-in credentials:

   ```powershell
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:SOMAMS" "Server=localhost;Port=3306;Database=ITClubOrgDB;User ID=root;Password=mich09252005!"
   ```

2. If the `admins` table is empty, optionally configure the initial administrator. The account is created only once; use a unique password of at least 12 characters:

   ```powershell
   dotnet user-secrets set "BootstrapAdmin:FullName" "Organization Administrator"
   dotnet user-secrets set "BootstrapAdmin:Username" "YOUR_USERNAME"
   dotnet user-secrets set "BootstrapAdmin:Email" "YOUR_EMAIL"
   dotnet user-secrets set "BootstrapAdmin:Password" "YOUR_UNIQUE_PASSWORD"
   ```

3. Run the web app:

   ```powershell
   dotnet run
   ```

Sign in with an administrator account already present in the `admins` table, or use the optional one-time bootstrap settings above. Members use the **Member Portal** link on the administrator sign-in page. The MVC app does not create or assume the legacy default `admin / admin123` credentials.

4. To enable forgot-password recovery, configure an SMTP sender in the local user-secrets store. For Gmail, use an app password rather than the account password:

   ```powershell
   dotnet user-secrets set "Email:SmtpHost" "smtp.gmail.com"
   dotnet user-secrets set "Email:SmtpPort" "587"
   dotnet user-secrets set "Email:SmtpUsername" "YOUR_SENDER_EMAIL"
   dotnet user-secrets set "Email:SmtpPassword" "YOUR_EMAIL_APP_PASSWORD"
   dotnet user-secrets set "Email:FromAddress" "YOUR_SENDER_EMAIL"
   dotnet user-secrets set "Email:EnableSsl" "true"
   ```

   Recovery codes expire after 10 minutes, allow three verification attempts, and are rate-limited. Active recovery challenges are kept in application memory, so restarting the app invalidates any outstanding codes.

## Migration phases

- **Phase 1:** shared MVC shell, authentication, dashboard, logout.
- **Phase 2:** Members and Membership CRUD, search, Student ID/name validation, delete confirmation, and Word export.
- **Phase 3:** Activities CRUD and search, status and attendance indicators, plus the active-member attendance roster with search, live present/absent counts, confirmation, and transactional one-time saving.
- **Phase 4:** Member, activity date-range, and activity attendance Word reports; administrator profile, username/email, profile photo, and optional password updates.
- **Phase 5:** Forgot-password email verification and password reset with expiring codes.
- **Phase 6:** Member self-registration with administrator approval, separate member sign-in, personal membership/activity/attendance views, and announcements.
ITClubOrg-Demo-2026!