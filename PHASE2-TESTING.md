# Phase 2 dashboard testing

Run `dotnet build` from the repository root, then start the configured Phase 1 application with `dotnet run --project HealthTrack`. Use the URL printed by the application. The existing SQL Server database and Identity configuration are required; no new migration is needed.

## Test accounts

Use separate active accounts with exactly one role each: Client, Trainer, and Admin. Public registration creates a Client and now opens the Client dashboard. Use the existing development Admin account if configured.

If you need a Trainer test account, register a separate account first, log it out, and run the following against your **development database** in SQL Server Management Studio. Replace the email with that test account's email. This changes only its role membership. Set `@RoleName` to `Admin` to prepare a separate Admin test account if necessary. Do not use a real user's account.

```sql
SET XACT_ABORT ON;
DECLARE @Email nvarchar(256) = N'trainer-test@example.com';
DECLARE @RoleName nvarchar(256) = N'Trainer';
DECLARE @UserId nvarchar(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = UPPER(@Email));
DECLARE @RoleId nvarchar(450) = (SELECT Id FROM AspNetRoles WHERE Name = @RoleName);
IF @UserId IS NULL OR @RoleId IS NULL
    THROW 50000, 'Test account or role was not found.', 1;
BEGIN TRANSACTION;
DELETE FROM AspNetUserRoles WHERE UserId = @UserId;
INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);
COMMIT;
```

Sign in again after changing roles so the authentication cookie contains the new role.

## Role and authorization checks

| Signed in as | Expected page after login / clicking Dashboard | URLs that must redirect to `/Account/AccessDenied` |
| --- | --- | --- |
| Client | `/Dashboard/Client` | `/Dashboard/Trainer`, `/Dashboard/Admin` |
| Trainer | `/Dashboard/Trainer` | `/Dashboard/Client`, `/Dashboard/Admin` |
| Admin | `/Dashboard/Admin` | `/Dashboard/Client`, `/Dashboard/Trainer` |

1. Test each row using separate browser sessions or log out between accounts. Check the final Access Denied page returns HTTP 403 in browser developer tools.
2. While logged out, visit `/Dashboard`, `/Dashboard/Client`, `/Dashboard/Trainer`, and `/Dashboard/Admin`. Each must redirect to Login. After successful login, the user's own dashboard opens, including when the login URL contains a return URL pointing elsewhere.
3. Check logged-out navigation shows Home, Login, and Register. Logged-in navigation shows Dashboard, the user's FullName, and Logout. Logout must sign out and return to Home; revisiting a dashboard must require login.
4. Client: confirm the welcome message uses the registered FullName and all eight requested cards appear. Trainer: confirm all six requested cards appear. Placeholder values must be visibly marked as unavailable or coming soon.
5. Admin: compare the four real counts with the queries below. Active Users means `IsActive = 1`, not currently online. Total Appointments and System Activity remain placeholders.
6. Resize to phone and desktop widths. Check that cards stack, long names wrap, and the mobile navigation toggle works.

```sql
SELECT COUNT(*) AS TotalUsers,
       COUNT(CASE WHEN IsActive = 1 THEN 1 END) AS ActiveUsers
FROM AspNetUsers;

SELECT r.Name, COUNT(ur.UserId) AS UserCount
FROM AspNetRoles r
LEFT JOIN AspNetUserRoles ur ON ur.RoleId = r.Id
WHERE r.Name IN ('Client', 'Trainer')
GROUP BY r.Name;
```

Authorization uses explicit role membership with no Admin bypass. An account with no recognized role reaches Access Denied through `/Dashboard`. For accounts intentionally assigned multiple roles, the landing-page priority is Admin, Trainer, then Client; each page still requires its own role.

## Implementation validation

`dotnet build` passed with 0 warnings and 0 errors after implementation. The signed-in browser and database checks above must still be performed with your local test accounts.
