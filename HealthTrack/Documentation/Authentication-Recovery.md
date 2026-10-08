# Authentication and password recovery

## Changes

Created:
- `Models/Account/ForgotPasswordViewModel.cs`, `ResetPasswordViewModel.cs`
- `Services/EmailSettings.cs`, `SmtpEmailSender.cs`, `PasswordResetEmail.cs`
- `Views/Account/_ViewStart.cshtml`, `ForgotPassword.cshtml`, `ForgotPasswordConfirmation.cshtml`, `ResetPassword.cshtml`, `ResetPasswordConfirmation.cshtml`
- `Views/Shared/_AuthLayout.cshtml`
- `wwwroot/css/auth.css`, `wwwroot/js/auth.js`
- `Tests/AuthRecovery.Smoke.ps1`, `AuthSmtp.Fixture.cjs`, `Auth.Browser.cjs`, `GmailRecovery.Smoke.ps1`
- This document.

Modified: `Controllers/AccountController.cs`, `Program.cs`, and the Login, Register and AccessDenied views. No database schema changes or new packages. The existing `appsettings.json` change is unrelated and was preserved.

AccountController adds anonymous GET/POST ForgotPassword and ResetPassword actions, plus GET ForgotPasswordConfirmation and ResetPasswordConfirmation. Both POST actions require antiforgery tokens. Existing registration, login, role routing, lockout and inactive-account logic are unchanged.

All seven authentication screens use the existing Bootstrap and HealthTrack colors, shared compact card styling, associated labels, visible focus states and readable validation. Password toggles work by keyboard, expose their state to assistive technology and are hidden when JavaScript is disabled. Forms work without JavaScript. Submit buttons disable during submission and restore on browser history navigation.

## Configure real email

The app uses ASP.NET Identity's `IEmailSender`, implemented by `SmtpEmailSender`. It sends actual SMTP messages; there is no production email simulation or token console output. Credentials are already configured in this workstation's User Secrets and are not included in the repository. Missing configuration or SMTP failures produce the same privacy-preserving confirmation, and the server records a safe failure category (numeric SMTP status, timeout or configuration). That confirmation explicitly does not promise successful delivery. Failures are not queued or retried automatically. Successful SMTP acceptance records event 1001 without recipient or message content; SMTP acceptance alone does not prove inbox arrival.

Set these keys with development User Secrets (`UserSecretsId` already exists) or deployment environment variables:

| Configuration key | Environment variable | Value |
| --- | --- | --- |
| EmailSettings:Host | EmailSettings__Host | SMTP server hostname |
| EmailSettings:Port | EmailSettings__Port | Usually 587; default 587 |
| EmailSettings:Username | EmailSettings__Username | Provider username, when required |
| EmailSettings:Password | EmailSettings__Password | Provider/app password |
| EmailSettings:FromEmail | EmailSettings__FromEmail | Verified sender email |
| EmailSettings:FromName | EmailSettings__FromName | Defaults to HealthTrack |
| EmailSettings:EnableSsl | EmailSettings__EnableSsl | true (default); SMTP STARTTLS |
| EmailSettings:PublicBaseUrl | EmailSettings__PublicBaseUrl | Required for deployment: trusted public HTTPS URL, e.g. https://healthtrack.example.edu; optional for a locally bound Development instance |

Example command shape: `dotnet user-secrets set "EmailSettings:Host" "smtp.example.edu"`. Enter actual credentials locally; never place them in committed settings, command transcripts or screenshots. `System.Net.Mail.SmtpClient` uses STARTTLS rather than implicit TLS on port 465. Use a provider/relay that supports SMTP credentials and STARTTLS (typically port 587).

Gmail uses `smtp.gmail.com`, port `587`, TLS enabled, the configured username and an App Password. The sender rejects an insecure or incomplete Gmail configuration before connecting. See [Google's SMTP configuration guidance](https://support.google.com/mail/answer/7104828?hl=en). Gmail SMTP submission and inbox arrival were both verified with the configured secrets.

PublicBaseUrl must be HTTPS outside Development; Development permits HTTP only for loopback URLs. When absent in Development, recovery uses the running server's bound loopback address, preferring HTTPS, so both existing launch profiles work without another secret. It never derives the URL from the request Host header. Wildcard/network bindings need an explicit PublicBaseUrl. Include any deployment path prefix. Configure HTTPS and your trusted proxy correctly. Persist ASP.NET Data Protection keys across deployments and share the key ring between replicas so outstanding Identity links remain valid. Keep request/query-string logging disabled or redacted at the hosting/proxy layer for reset URLs; the existing ASP.NET logging threshold is Warning. Do not enable sensitive-data or request-body logging for authentication.

## Security behavior

- Identity generates and validates the reset token and hashes the new password. No custom password storage.
- URL-safe Base64 wraps the token for transport; HTML encoding protects the email link.
- Links use the configured public URL (or a bound loopback server URL in Development), never an untrusted request Host header.
- The default Identity data-protection token lifetime is 24 hours. Successful resets change the security stamp, invalidate old sessions on their next request, and prevent token reuse.
- Unknown/inactive accounts and SMTP failures use identical recovery confirmation content. No account identifier, token or password is logged by the new code. Sender exceptions are deliberately not logged because provider messages can contain recipients or message content.
- Missing, malformed, invalid or expired links show safe recovery guidance. A fresh-link action is always available. Password rules remain Identity's 8-character minimum with uppercase, lowercase, digit and symbol; registration retains its existing 100-character maximum.
- Reset does not automatically sign in, activate accounts or clear lockout. Existing dashboard role redirects ignore caller-supplied return URLs and cannot redirect to an external site.
- Auth pages disable referrer transmission; the existing global no-store and antiforgery filters remain in force.

## Manual demonstration

1. Start the application in Development using the existing Gmail User Secrets, and create/use an active account with an inbox you can access. For deployment, configure PublicBaseUrl and the SMTP values as environment variables.
2. Open Login and select **Forgot password?**.
3. Enter the account email and select **Send Reset Link**. Confirm the generic response.
4. Receive the HealthTrack reset email (check spam) and open **Reset password**.
5. Confirm the email, enter a new password meeting the displayed rules twice, and select **Reset Password**.
6. On **Password reset successful**, select **Back to Login** and sign in with the new password.
7. Verify the old password fails and reopening/submitting the used link gives invalid/expired guidance.
8. Request another link, wait beyond 24 hours, and verify that submission is rejected; request a fresh link to recover.
9. Repeat the request with an unknown email: the response must be identical and no email should arrive. Test malformed/missing tokens, mismatched passwords and a weak password.

Real Gmail delivery was verified end to end using a unique plus-address in the configured Gmail inbox and a disposable account in the current SQL Server database. The test read only the matching test email over authenticated TLS IMAP, used the received link, confirmed reset success, rejected the old password, and signed in with the new password. It removed the disposable HealthTrack account and left existing accounts unchanged. The test email remains in the inbox, but its link is no longer usable. No secrets, tokens, passwords or email contents were printed. A genuinely time-expired 24-hour link was not tested by waiting for its lifetime; malformed, invalid and already-used links were tested.

## Automated verification

Restore and build: `dotnet restore`, `dotnet build --no-restore` (zero warnings/errors).

Recovery integration: start `node Tests/AuthSmtp.Fixture.cjs`, then run the app in Development with process-local EmailSettings values: Host=127.0.0.1, Port=2526, EnableSsl=false, FromEmail=healthtrack@example.test, PublicBaseUrl=http://localhost:5092. Use the existing LocalDB test connection and `dotnet run --no-build --no-launch-profile --urls http://localhost:5092`. Run `powershell -NoProfile -File Tests/AuthRecovery.Smoke.ps1`.

The fixture accepts real SMTP messages in memory, never sends externally, and exposes them on loopback port 2527 for the test. Stop both processes after testing. Do not deploy or use this fixture with real account data. Test accounts are unique and removed in finally.

The recovery suite checks registration; all four role logins/redirects; Remember Me; invalid email; antiforgery; equal known/unknown/inactive/failure responses; actual SMTP message capture; trusted links; password rules; matching confirmation; successful reset; old/new password login; existing-session invalidation; no auto-login; replay and malformed/invalid tokens; SMTP failure; inactivity and lockout.

Browser: `node Tests/Auth.Browser.cjs http://localhost:5092 <path-to-playwright-module>`. Uses headless Edge, screenshots under ignored `bin/AuthBrowserChecks`, and widths 320, 375, 768, 1440. Checks overflow, labels, headings, autofocus, keyboard toggles, recovery navigation, repeated-submit protection and JavaScript-free submission. Desktop Login and mobile Register screenshots were visually reviewed.

Regression: `Tests/FinalVerification.ps1` runs the existing 15 smoke suites. Ensure the app AND test process set `ConnectionStrings__DefaultConnection` to the same LocalDB connection; Phase3 launches a child development-assignment process that otherwise reads the configured SQL Express database.

Latest Gmail follow-up results: the aggregate runner completed with exit 0; all 15 suites passed (Database, Phase1, TrainerManagement, HealthProgress, Phase3, Phase4, Assignments, DashboardPlans, DashboardUi, Notifications, Reports, Security, Coursework, CrudPdf, RemainingCrud). The app and test process used the same isolated LocalDB connection. Recovery and browser suites also completed successfully, including all four role logins, Remember Me, inactive users, lockout, malformed/invalid/reused tokens, password validation, SMTP failure and mobile/desktop layout. The real Gmail test separately used the existing SQL Server connection without changing it.

## Gmail follow-up verification

`Tests/GmailRecovery.Smoke.ps1` is an explicit, real-mail test (never part of the default regression runner). Start the app on `http://localhost:5075` in Development using the existing SQL Server configuration and Gmail User Secrets, then run the script from the project root. It sends one email to a unique plus-address of the configured Gmail username, reads only that message through TLS IMAP, verifies the received reset link and both password outcomes, and removes its disposable account in finally. It does not print secret values. IMAP inbox checks are test tooling only; the application itself only uses SMTP.

The Gmail follow-up reused all existing actions, ViewModels, DI registrations, Identity policy, Login/Register views and shared auth styling. It added the email template and real-delivery test, added the trusted Development URL fallback and safe SMTP diagnostics, and updated Forgot Password and reset-success supporting text. No packages, migrations, SMTP credentials, database connection changes or registration business-rule changes were introduced.
