# Admin section

The local HealthTrackDB inspection on 2026-09-27 found one Client membership and no Admin accounts. Existing passwords were not inspected. No administrator password is hardcoded.

## Seed and sign in

From the project directory, run these PowerShell commands. Choose an unused email and replace the password placeholder with your own password (8�100 characters, uppercase, lowercase, digit, and symbol).

```powershell
dotnet user-secrets set "Admin:Email" "admin@healthtrack.com"
dotnet user-secrets set "Admin:Password" "<your-private-strong-password>"
dotnet run --launch-profile http
```

The http launch profile runs in Development, where IdentitySeeder creates the account and assigns Admin. The configured SQL database must be available with migrations applied (`dotnet ef database update` if needed). An existing Client or Trainer account cannot be modified by this seeder; choose a different email. While these secrets remain configured, each Development startup resets the Admin password to Admin:Password, activates the account, and clears lockout. Log in at `/Account/Login` using the email and password above. After successful seeding, remove the seed credentials:

```powershell
dotnet user-secrets remove "Admin:Password"
dotnet user-secrets remove "Admin:Email"
```

Removing seed settings does not delete the account. The database stores the Identity password hash.

## Trainer workflow

1. Log in as Admin. On the Admin Dashboard, click + Add Trainer to open the MVC Create Trainer form.
2. Enter Full Name, an unused Email, Password, and matching Confirm Password. Submit.
3. The new account receives only Trainer and is active. The dashboard table below the summary cards shows Full Name, Email, Status, Created Date (UTC), and Actions. Edit opens an MVC form to update name and sign-in email; Activate/Deactivate controls login and booking availability. Validation errors appear on the submitted form; all validation runs in C#.
4. In another browser session, log in as a Client (public registration creates Clients). Open Book Appointment. The active trainer appears by name and email in the Trainer dropdown.
5. Deactivate the trainer as Admin and refresh Client booking: the trainer disappears. Reactivation restores the trainer.

Dashboard, Clients, Appointments, Users, and Logout are available in Admin navigation. The dashboard links to trainer creation and editing forms and contains status controls. Successful changes return to the dashboard. Clients and Users display account status and creation date; Users also displays roles. Admin appointments are read-only. Non-admin access to Admin routes returns Access Denied (HTTP 403).

## Verification

`Tests/TrainerManagement.Smoke.ps1` exercises trainer creation, validation, hashing, editing, role authorization, activation, session rejection, and Client booking visibility against a running app and local HealthTrackDB. It creates uniquely named test users and removes them afterward.

## Files changed for this task

- Controllers/AdminController.cs (created)
- Controllers/AdminTrainersController.cs (updated existing work)
- Models/Admin/AdminUserListItem.cs (created)
- Models/Admin/EditTrainerViewModel.cs (created)
- Views/Admin/Clients.cshtml (created)
- Views/Admin/Users.cshtml (created)
- Views/Admin/Appointments.cshtml (created)
- Views/AdminTrainers/Edit.cshtml (created)
- Views/AdminTrainers/Index.cshtml (updated existing work)
- Views/Shared/_AdminUsersTable.cshtml (created)
- Views/Shared/_Layout.cshtml (updated)
- Views/Dashboard/Admin.cshtml (updated)
- Tests/TrainerManagement.Smoke.ps1 (extended existing work)
- ADMIN.md (created)

Validation completed: `dotnet build HealthTrack/HealthTrack.csproj --no-restore -o HealthTrack/bin/DashboardActionsCheck` passed with 0 warnings and 0 errors. All 58 trainer management smoke checks passed, including trainer edit validation and Client booking visibility. The separate output directory avoids an existing running app locking the default build output.

