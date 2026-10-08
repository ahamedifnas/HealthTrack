# Client registration email OTP

## Scope and flow

Only public Client registration requires an email OTP. Login keeps its existing password, lockout, Remember Me and role redirect behavior; the only added branch blocks a newly registered pending account after its password has been checked and offers a verification link. Forgot Password, Reset Password, the Gmail SMTP sender, staff account creation and existing role definitions are unchanged.

Registration creates the Identity user, Client role assignment and OTP placeholder atomically. It does not sign in. A six-digit code is generated using `RandomNumberGenerator.GetInt32`, hashed using Identity's salted PBKDF2 PasswordHasher and sent with the existing `IEmailSender`. Only the hash is stored. The verification page uses the existing HealthTrack auth layout and Bootstrap styling.

Rules:
- Code lifetime: **5 minutes**, enforced against UTC server time.
- Maximum **5 wrong attempts per issued code**, checked and persisted server-side.
- Resend cooldown: **60 seconds**, enforced server-side even during concurrent requests or SMTP failures.
- Resend replaces the previous hash, resets the attempt window and guarantees the immediately previous code is not reissued.
- Correct verification marks `EmailConfirmed=true`, clears the new-registration flag, marks the code used and erases its hash. The user returns to Login without automatic sign-in.
- SQL row update locks serialize verification/resend requests across application instances. Confirmation and OTP consumption commit in one transaction.
- SMTP failure is reported safely and invalidates the undelivered code. The pending account can retry after the cooldown.

A time-limited, protected HttpOnly cookie identifies the pending user; no OTP appears in URLs or rendered HTML. The cookie expires after one day and uses Secure on HTTPS. Returning users can enter the correct password at Login to obtain a new verification context. Posted OTP values are cleared before rendering any validation response. All mutation actions require antiforgery tokens. No OTP, password, email body or provider exception payload is logged.

## Files

Created:
- `Models/EmailVerificationOtp.cs`
- `Models/Account/VerifyEmailOtpViewModel.cs`
- `Services/RegistrationOtpService.cs`
- `Services/RegistrationOtpEmail.cs`
- `Views/Account/VerifyEmailOtp.cshtml`
- `Migrations/20261008184508_ClientRegistrationEmailOtp.cs` and its Designer file
- `Tests/RegistrationOtp.Smoke.ps1`
- `Tests/GmailRegistrationOtp.Smoke.ps1`
- `Tests/RegistrationOtp.Browser.cjs` and its PowerShell runner
- This document

Modified for this feature:
- `Controllers/AccountController.cs`: registration, verification/resend actions and pending-registration login guard
- `Models/ApplicationUser.cs`: explicit opt-in marker for new public registrations
- `Data/ApplicationDbContext.cs`: OTP table mapping and cascading user relationship
- `Migrations/ApplicationDbContextModelSnapshot.cs`
- `Program.cs`: registration OTP service and TimeProvider registrations
- `Views/Account/Login.cshtml`: verification guidance and completion message only

## Database and migration

Migration: **20261008184508_ClientRegistrationEmailOtp**.

Adds `EmailVerificationOtps`, keyed by UserId with OtpHash, CreatedAt, ExpiresAt, LastSentAt, IsUsed and AttemptCount. The user foreign key cascades on user deletion. Adds `AspNetUsers.RequiresRegistrationEmailVerification` with default **false**. Only the public Register action sets it true. Consequently, existing accounts remain eligible for normal sign-in even when their old EmailConfirmed value is false; no bulk email-confirmation update was performed.

The configured SQL Server database already had the application tables but **zero EF migration-history entries**. A normal `dotnet ef database update --no-build` therefore attempted InitialIdentity and stopped at the existing AspNetRoles table. No existing account tables were rebuilt.

The exact EF-generated incremental SQL from `20261008120739_ArchiveWorkoutExercises` to `20261008184508_ClientRegistrationEmailOtp` was reviewed and applied transactionally to the existing database. It adds only the OTP table and user flag and records only this new migration. All five existing users retained the default-false marker. The existing connection settings were not changed.

For a database with complete migration history, the normal EF update command applies this migration. On this database, older migration-history gaps remain a pre-existing maintenance issue: verify/baseline its old schema history before a future full EF update. Do not blindly insert older migration IDs or recreate tables. The registration feature's schema is applied and usable now.

## Verification performed

- Full project build: **0 warnings, 0 errors**.
- `RegistrationOtp.Smoke.ps1`: **44 checks passed**, using a local SMTP fixture and disposable accounts on the configured SQL Server database. Includes form validation, Client role, no auto-login, hashed storage, 300-second expiry, wrong/correct codes, five-attempt lock, resend cooldown, replacement-code validity, single use, antiforgery, tampered cookies, SMTP failure/recovery and all four legacy-role login paths with EmailConfirmed=false.
- Expiry and elapsed cooldown were tested by moving timestamps only on disposable test OTP records; no machine clock or production account timestamps were changed. This verifies server-side comparisons without waiting five minutes.
- `GmailRegistrationOtp.Smoke.ps1`: real Gmail submission, inbox receipt over TLS IMAP, six-digit code and branded content, wrong-code rejection, correct-code confirmation and normal Client login all passed. Secrets and codes stayed in memory. The disposable plus-address account was removed; its test email remains in the inbox.
- Browser tests cover 320, 375, 768 and 1440 pixel widths, labels/autofill, no OTP in source, empty code after an error, focused validation, successful verification and simultaneous wrong-code requests. Screenshots are under ignored `bin/RegistrationOtpBrowser`.

Only registration-focused tests and required login compatibility checks were run for this change. Older broad smoke suites assume immediate sign-in after public registration; their setup must perform OTP verification before they are used with the new flow. No unrelated workflow tests or password-recovery tests were run or changed.

## Manual demonstration

1. Start HealthTrack in Development with the existing Gmail User Secrets. Register a new Client using an accessible email address.
2. Confirm that Verify your email opens and the account is not signed in. Check the Gmail inbox/spam folder for the six-digit code.
3. Enter an incorrect code: validation appears and the input is cleared. Enter the correct code: registration completes and Login displays confirmation.
4. Sign in normally. EmailConfirmed is now true and the Client role is preserved.
5. With another new registration, wait over five minutes and submit its code: expiry guidance appears.
6. Resend immediately after a send to observe the 60-second restriction. After the cooldown, resend and confirm the old code fails while the newest code works.
7. On another code, make five wrong attempts. Even the correct code is blocked until a fresh code is requested after the cooldown.
8. Confirm an existing account can still sign in normally.

No new Gmail credentials or packages are needed. Restart an already-running application to load this build. In another environment, apply the migration using its normal deployment process before starting the updated application.
