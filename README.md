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

The app requires the `admins`, `members`, `activities`, and `attendance` tables in `ITClubOrgDB`. For a fresh or empty database, start with [`Database/01_core_schema.sql`](./Database/01_core_schema.sql). It creates only missing tables and does not import legacy records; restore or import your data separately before applying the updates. Existing tables and rows are left unchanged by this script.

Run the schema scripts in this order: `01_core_schema.sql`, then `04_divisions.sql` through `17_admin_account_to_member.sql` in numeric order. Each script selects `ITClubOrgDB`; `01_core_schema.sql` creates that database if needed. Do not start with `05` or run data updates such as `06` and `07` before their base tables exist. The additive migrations target **only** `ITClubOrgDB`; they do not read or modify `somams_db`.

The division migration (`04`) adds account roles and division ownership; existing member and activity records remain unassigned until assigned in the app. The member portal migrations (`05`, `08`, and `09`) add self-registration and email-based account recovery. New registrations use a two-step form, require an email address, and accept passwords from 8 to 72 characters. After approval, SOMAMS sends an email notification to the applicant using the configured SMTP settings; if delivery fails, the approval remains in effect and the administrator is told to contact the member. Older pending registrations without an email can still be approved, but must be notified manually. Approved portal accounts retain the registration email for member password recovery. Approval creates the member record if it does not exist, or links the portal account to an existing matching record. If the Student ID already exists but the submitted name, course, year, or division differs, an administrator must reconcile the member record before approval. Division admins can review registrations and announcements only for their assigned division.
Run [`Database/12_history_and_activity_registration.sql`](./Database/12_history_and_activity_registration.sql) after the portal migrations to add member status history, administrator position/term history, and activity registration/participation tracking. Members can register or unregister from eligible activities in the portal; authorized administrators can review registrations and record participation.
Run [`Database/13_activity_participation_details.sql`](./Database/13_activity_participation_details.sql) afterward to add participation type, optional notes, and administrator audit timestamps to registration records. Super admins and division admins scoped to the activity can update these details. Run [`Database/14_activity_registration_open.sql`](./Database/14_activity_registration_open.sql) to add the per-activity member-registration switch; existing activities remain open by default.

Configure `Email:SmtpHost`, `Email:SmtpPort`, `Email:SmtpUsername`, `Email:SmtpPassword`, `Email:FromAddress`, and `Email:EnableSsl` through user secrets or deployment environment variables to send approval notifications and password-reset codes. Do not commit SMTP credentials.

Migration `06` converts existing member and registration courses to BSIT, maps legacy President positions to Division Head, and normalizes other member positions to Member. New membership forms offer only Division Head and Member; division administrators can add Members but cannot assign the Division Head position.

Migration `07` adds activity start/end timestamps. Existing activities are backfilled from their current `activity_date` with a one-hour interval beginning at midnight; update their times in Activities if the original event times are known. Super admins can create organization-wide or division-specific activities and edit recorded attendance across divisions. Once attendance is first saved, the activity status is set to **Completed**.

Migration `15` adds an optional email address to member records and optional student ID, course, year level, position, and date-joined profile fields to administrator accounts. Administrator email addresses are expanded to 254 characters to match member email addresses. Membership forms and member exports include email; editing a member also synchronizes the email on an existing portal account, and password recovery uses the member email as a fallback.

Migration `16` creates the optional administrator reserve-candidate roster. Reserve nominations do not create or enable administrator accounts. Attendance can be recorded only at or after the activity start time; activity selection is shown in a paginated table.

Migration `17` allows administrator accounts to be removed while preserving member requests and announcements. User Roles limits the system to two Super Admin accounts; changing an administrator's role to Member creates or updates their active member record and removes their administrator login.

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
   dotnet user-secrets set "ConnectionStrings:SOMAMS" "Server=localhost;Port=3306;Database=ITClubOrgDB;User ID=root;Password=mich09252005!" Flodes@111023
   ```root@127.0.0.1:3306

2. If the `admins` table is empty, optionally configure the initial administrator. The account is created only once; use a unique password of at least 12 characters:

   ```powershell
   dotnet user-secrets set "BootstrapAdmin:FullName" "Organization Administrator"
   dotnet user-secrets set "BootstrapAdmin:Username" "YOUR_USERNAME"
   dotnet user-secrets set "BootstrapAdmin:Email" "YOUR_EMAIL"
   dotnet user-secrets set "BootstrapAdmin:Password" "YOUR_UNIQUE_PASSWORD"
   ```
dotnet user-secrets set "BootstrapAdmin:James Davis" "Organization Administrator"
dotnet user-secrets set "BootstrapAdmin:Username" "admin"
dotnet user-secrets set "BootstrapAdmin:Email" "davis11jamesmichael@gmail.com"
dotnet user-secrets set "BootstrapAdmin:Password" "jamesadmin123"
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