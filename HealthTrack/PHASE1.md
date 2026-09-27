# Phase 1 setup and testing

This MVC application targets .NET 10 and uses EF Core SQL Server and ASP.NET Core Identity 10.0.9. Install the .NET 10 SDK and SQL Server Express LocalDB on Windows.

## Database

`ConnectionStrings:DefaultConnection` points to `(localdb)\MSSQLLocalDB`, database `HealthTrackDB`, using Windows authentication. The certificate trust setting is for local development. Override the connection string through deployment configuration for other environments; do not commit production credentials.

Run from the project directory:

```powershell
dotnet tool restore
dotnet restore
dotnet ef database update -- --environment Development
dotnet run --launch-profile https
```

Migrations are applied explicitly, not automatically at application startup. Startup seeds the Admin, Trainer and Client roles idempotently. No dashboards or other feature modules are included.

## Optional development admin

Set these values locally before starting the application, replacing both placeholders:

```powershell
dotnet user-secrets set "DevelopmentAdmin:Email" "admin@healthtrack.local"
dotnet user-secrets set "DevelopmentAdmin:Password" "<your unique strong password>"
```

The project already has a UserSecretsId. User-secrets are stored outside the repository and are for development only. Alternatively, set `DevelopmentAdmin__Email` and `DevelopmentAdmin__Password` environment variables in the process running the app.

The password must contain at least eight characters with uppercase, lowercase, a digit and a non-alphanumeric character. Never use the literal placeholder above.

When both values are absent, admin seeding is skipped. When only one is provided, startup reports a configuration error. Seeding runs only in Development and does not reset an existing admin password. A matching non-admin account is never promoted automatically: choose an unused email. Changing the seed email can create another admin, so keep one configured email for the development account.

## Browser checks

1. Start with `dotnet run --launch-profile https` and open `https://localhost:7041`. Trust the ASP.NET Core development certificate if your local setup requires it.
2. Click Register. Submit blank/invalid fields, mismatched passwords and a weak password to check validation. Then register using a new email and a valid password. Registration signs you in and returns to Home. There is no role selector; all public registrations receive Client.
3. Click Logout. The navigation should show Register and Login again.
4. Click Login and sign in with the registered credentials. Try an incorrect password separately; five failed attempts lock the account for 15 minutes. Remember me requests a persistent cookie.
5. Try registering the same email again; it must be rejected.
6. Visit `/Account/Login?returnUrl=https://example.com`, then sign in: external return URLs must be ignored and you should return to Home.
7. Visit `/Account/AccessDenied` to see the access-denied page (HTTP 403).
8. If development admin secrets are configured, restart and log in with those credentials. All users currently return to Home because dashboards are outside Phase 1.

Use SQL Server Object Explorer to connect to `(localdb)\MSSQLLocalDB`. Under HealthTrackDB, inspect `__EFMigrationsHistory`, `AspNetRoles`, `AspNetUsers` and `AspNetUserRoles` to verify the migration, three roles, profile fields and assignments. Passwords are stored as Identity hashes, never plaintext.

Identity role claims and authorization services are configured. Future role-specific controllers can use `[Authorize(Roles = AppRoles.Admin)]` (or Trainer/Client). No role-specific feature endpoints are introduced in this phase. Inactive accounts cannot log in, and existing cookies are rejected on their next request after deactivation.

## Repeatable smoke test

Start the app with `dotnet run --launch-profile http`, then run `powershell -NoProfile -File Tests\Phase1.Smoke.ps1` in another terminal. This tests the local database and HTTP form flows, creates a uniquely named temporary account and removes it afterward. It checks role seeding, Client-only registration, validation, duplicate email rejection, login, logout antiforgery protection, external redirect rejection, inactive accounts and access denied. This script is only for the default local development database.

## Verification performed

- Original project build: passed, zero warnings/errors.
- Phase 1 build including generated migrations: passed, zero warnings/errors.
- Applied `20260927112856_InitialIdentity` to `HealthTrackDB`.
- All 16 smoke-test checks passed; the temporary user was removed.
- Development admin creation is optional and was not exercised with personal credentials.

Package compatibility reference: [EF Core SQL Server 10.0.9 targets .NET 10](https://packages.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/10.0.9).
