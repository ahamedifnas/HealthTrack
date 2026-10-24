# Verified local Demo Payment setup

Verified 2026-10-21. Run commands below from the directory containing `HealthTrack.csproj`.

## Configuration audit before this change

- `appsettings.json`: DefaultConnection selects `.\SQLEXPRESS / HealthTrackDB`; DemoConnection selects `.\SQLEXPRESS / HealthTrack_LocalDemoOnly`. Both use Windows authentication and TrustServerCertificate. Provider is PayHere, Mode is Sandbox, checkout URL is `https://sandbox.payhere.lk/pay/checkout`, currency LKR. AllowedHosts is `*`. Credentials are intentionally omitted here.
- `appsettings.Development.json`: logging only. It does not select a payment provider or connection.
- User Secrets ID: `HealthTrack-Development-Identity`. Existing secrets configure SMTP and the development admin. No ConnectionStrings or Payments override was present. No secret was changed or copied.
- Process, user and machine environment inspection found no connection, provider, environment, URL, Kestrel or Assignment override affecting selection.
- Visual Studio's `HealthTrack.csproj.user` selects `https`, which sets Development and URLs 7041/5075. Plain `dotnet run` selects the first profile, `http`, which sets Development and port 5075. These standard profiles use the PayHere provider and DefaultConnection. The `demo` profile explicitly selects Demo in Development and now binds only to the familiar `https://localhost:7041` URL.
- No design-time DbContext factory or OnConfiguring override was found. Program registers the context with DemoConnection only when PaymentMode.IsDemo is true; otherwise it uses DefaultConnection. EF tooling uses the same selection logic, but does not automatically select a launch profile. Never assume EF's environment matches F5.
- Configuration precedence is base JSON, environment-specific JSON, Development User Secrets, environment variables, then command-line configuration. A future higher-priority override can change selection.

## Database evidence and suitability

HealthTrackDB and HealthTrack_LocalDemoOnly remain separate. No data was copied between them, and neither database was reset or recreated.

HealthTrackDB has IsDemo and all 14 repository migrations, ending with `20261021073641_AddDemoPaymentMarker`. This migration was applied with approval after a verified full backup. Existing HealthTrackDB payment rows defaulted to IsDemo=false. The isolated database also has the marker and all 14 repository migrations.

The demo users are exactly client@healthtrack.test and client2@healthtrack.test (Client), and admin@healthtrack.test (Admin), all active and email-confirmed. Packages are Basic Starter (LKR 3500, 30 days), Pro Fitness (LKR 8500, 90 days), Elite Health (LKR 28000, 365 days), all active. Demo smoke runs retain simulated orders and successful test memberships; the database is not reset between runs.

## Changes and isolation

Development settings do not globally select Demo. Only the `demo` launch profile sets `Payments:Provider=Demo`; it uses the DemoConnection already defined in base settings. Standard `http` and `https` profiles retain their original ports and PayHere selection. When Demo is selected, Program binds only to `https://localhost:7041`; do not run the standard profile concurrently because it uses the same URL. Base/production settings were not changed.

Program logs the selected demo server and database without credentials. The smoke test now also checks that each new order appears in the owner's labelled payment history and is absent from the second client's history.

Existing safeguards remain: Demo fails startup outside Development, or if its connection is not the dedicated SQL Express database using Windows authentication. It uses separate Identity cookies and synthetic seed users. Middleware rejects non-loopback peers, public hosts, proxy/ngrok headers and unsafe POST origins before routing. Demo endpoints additionally require the Client policy, ownership and antiforgery. Server-side package prices are authoritative. Completion is transactional and replay-protected. Only success activates membership; failure/cancellation do not. Payments retain IsDemo=true, no gateway ID and SignatureVerified=false. Admin revenue excludes demo records. PayHere IPN excludes demo orders and retains its existing signature, merchant, amount and currency validation; its code was not modified.

Startup automatically checks migrations for the demo database. It was current during verification. Review future migrations before starting an updated build. The standard HealthTrackDB is now also current through AddDemoPaymentMarker.

SMTP secrets remain available to Development. Use the seeded login accounts for payment testing; registration/recovery email workflows were not exercised. Do not point ngrok or any reverse proxy at this listener, and do not strip proxy headers to bypass restrictions. No public tunnel was started.

## Launch safely

Stop any running HealthTrack instance first; standard and demo profiles share the familiar HTTPS address, so run only one at a time. Do not override Payments, ConnectionStrings, Kestrel endpoints or the environment. In PowerShell:

```powershell
dotnet build HealthTrack.csproj -c Release --no-restore
dotnet run --project HealthTrack.csproj --launch-profile demo -c Release --no-build --no-restore
```

Before checkout, confirm these startup messages:

```text
Local Demo Payment database: .\SQLEXPRESS / HealthTrack_LocalDemoOnly. No real money.
Now listening on: https://localhost:7041
Hosting environment: Development
```

Open https://localhost:7041 directly. Sign in as client@healthtrack.test with the synthetic password `LocalDemo!2026`. Select a package and Pay, then simulate success, failure or cancellation. Review `/Payments/History` and `/Payments/Membership`. The admin account uses the same test password. These are test credentials, not personal accounts.

Visual Studio: stop any running instance, select the **demo** launch profile from the launch-profile dropdown, then press F5. The browser opens `https://localhost:7041`; confirm startup logs name `HealthTrack_LocalDemoOnly` before sign-in. Select **https** to run the normal PayHere app at the same familiar HTTPS URL. Plain `dotnet run` selects `http`, runs PayHere on port 5075. Provider choice is server-side per launch profile; there is no client-side switch. Avoid `--no-launch-profile` without explicitly selecting Development. Do not point ngrok or another proxy at the demo listener.

In a second terminal, run:

```powershell
dotnet test ../HealthTrack.Tests/HealthTrack.Tests.csproj -c Release --no-restore
powershell -NoProfile -File Tests/DemoPayments.Smoke.ps1
```

Stop the application with Ctrl+C when finished. Keep the demo database; do not reset/delete either database.

## Results

- Release build: passed, zero warnings/errors.
- xUnit payment safety suite: 20 passed, zero failed/skipped. Covers Production/Staging rejection, wrong database, allowed local requests, remote/public hosts, proxy headers and origins.
- HTTPS/SQL smoke suite: 66 passed. Covers authenticated/role/ownership restrictions, antiforgery, tampered price, failure/cancellation/success, renewal, replay, exact no-real-money labels, receipts, owner-only history, zero real revenue and related application pages.
- Runtime startup confirmed the isolated database and Development; demo mode binds only to localhost HTTPS at 7041.
- Test-project dependency audit emitted NU1902/NU1903 for existing System.Security.Cryptography.Pkcs 9.0.4. Dependency remediation was not part of this configuration change.
- SQL inspection, application execution and smoke tests required execution outside the sandbox because sandbox Windows authentication failed with an SSPI error.
- No real PayHere payment, public tunnel, database reset, deletion, or personal/production data copy occurred. Existing unrelated working-tree changes were preserved.
