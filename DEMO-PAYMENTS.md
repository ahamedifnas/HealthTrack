# Local demo payments

## Diagnosis

There was no demo provider or demo completion endpoint in this checkout implementation. Both Checkout and Initiate always generated PayHere hashes, and Initiate always returned the auto-submitting PayHereRedirect view. appsettings.json selects PayHere Sandbox at https://sandbox.payhere.lk/pay/checkout and contains placeholder merchant values; appsettings.Development.json did not override payment behavior. PayHereSettings also contains fallback placeholder values. Those values are not approved merchant credentials. Selecting Sandbox alone does not enable simulation.

## Run locally (PowerShell, repository root)

Prerequisites: .NET 10 SDK and local SQL Server Express instance `.\SQLEXPRESS`, with Windows authentication and permission to create the dedicated demo database. Stop any previously started Release build before rebuilding it. The existing Debug application can remain running on its own port.

```powershell
dotnet restore HealthTrack.slnx
dotnet build HealthTrack.slnx -c Release
dotnet run --project HealthTrack --launch-profile demo -c Release --no-build
```

Open **http://127.0.0.1:5076** directly in your local browser. This profile sets Development and Payments:Provider=Demo. Startup creates/migrates only `HealthTrack_LocalDemoOnly`, seeds membership packages and these test accounts, and uses a separate demo login cookie:

| Account | Password | Role |
| --- | --- | --- |
| client@healthtrack.test | LocalDemo!2026 | Client |
| client2@healthtrack.test | LocalDemo!2026 | Second client for ownership tests |
| admin@healthtrack.test | LocalDemo!2026 | Admin/report inspection |

These credentials are intentionally test-only; do not copy these accounts or this database into another environment. No existing database is deleted, reset, or copied. Restarting preserves demo records and does not reset account passwords. Normal development admin credentials from user secrets are not seeded into the demo database.

Sign in as the client, select a membership package, click Pay, then choose **Simulate success**, **Simulate failure**, or **Simulate cancellation**. Checkout, simulation, history and receipts identify **Demo Payment – No Real Money**. Success activates membership using the purchased package duration; a later success extends membership using the existing renewal behavior. Failure and cancellation do not activate it. A terminal order cannot be changed by replaying its completion request. Pending orders expire after 30 minutes; changed/inactive packages require a new checkout.

## Safety boundaries

- Demo mode fails startup outside Development or with a database/server other than the dedicated local SQL Express database.
- The demo profile binds to loopback. The entire demo application rejects non-loopback connections, public Host values, Forwarded/X-Forwarded/Via/ngrok headers. State-changing requests also require an exact local Origin. Requests are checked before routing and authentication; forwarded headers must not be enabled ahead of this check.
- Demo actions require the existing Client authorization policy, authentication, ownership and antiforgery validation. POST completion checks current server-side price, currency, duration and package availability against the server-created order. No posted price is trusted.
- Completion serializes purchases per client and saves subscription activation and payment status in one SQL transaction. GET requests never complete demo payments.
- Demo orders use `DEMO-` IDs and persisted `IsDemo=true`. They have no PayHere payment ID and never set SignatureVerified. PayHere IPN lookup excludes demo orders; its existing signature, merchant, amount and currency verification remains intact.
- Both admin revenue totals exclude IsDemo records. Demo records remain visible and labelled for audit/history.

Do not point ngrok at the demo listener, strip proxy headers, rewrite public requests into local requests, or publish this database. The application rejects ordinary ngrok traffic including host-rewritten requests carrying forwarding headers; the tests exercise these requests locally, not through a live public tunnel. A local administrator who deliberately reconfigures a proxy to impersonate direct localhost traffic is outside this application-level boundary.

## Tests

With the demo application running, from a second PowerShell window:

```powershell
dotnet test HealthTrack.slnx -c Release --no-build --no-restore
powershell -NoProfile -File HealthTrack/Tests/DemoPayments.Smoke.ps1
```

The xUnit tests check provider/environment/database restrictions and loopback/proxy/origin rejection. The smoke tests exercise real HTTP, Identity, antiforgery, server pricing, ownership, simulation outcomes, replays, renewals, receipts, revenue and existing pages against the isolated database. They retain test transactions and memberships; they do not delete data. They are intended for the seeded test accounts only.

## Switch back to PayHere Sandbox later

1. Stop the demo process with Ctrl+C. Keep its database separate.
2. Obtain an approved Sandbox merchant/domain setup and matching Sandbox Merchant ID and Merchant Secret. The placeholder values in the repository will not work. Store the valid Sandbox values in local user secrets, never in source control:

```powershell
dotnet user-secrets set "PayHere:MerchantId" "YOUR_SANDBOX_MERCHANT_ID" --project HealthTrack
dotnet user-secrets set "PayHere:MerchantSecret" "YOUR_SANDBOX_MERCHANT_SECRET" --project HealthTrack
```

3. Select PayHere and apply the additive IsDemo migration to the intended existing Sandbox/development database. Review ConnectionStrings:DefaultConnection (including any user-secret overrides) before running the update. This adds a false-by-default marker to existing transactions; it does not reset them.

```powershell
$env:Payments__Provider = 'PayHere'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project HealthTrack --configuration Release
dotnet run --project HealthTrack --launch-profile https -c Release --no-build
```

The default PayHere configuration already uses Mode=Sandbox, Currency=LKR and the Sandbox checkout URL. Ensure no environment/user-secret override points to a live endpoint. Use an approved Sandbox origin with a reachable `/Payments/Notify` callback when testing the real integration. Return and cancel URLs are derived from the request origin. A browser success return does not activate membership: the existing signed IPN verification is still required. Demo endpoints return 404 when PayHere is selected. Do not use the demo launch profile for Sandbox because it explicitly selects Demo.

## Verification performed

Release build succeeded; `dotnet test` passed 20 tests (0 failed, 0 skipped); the final demo smoke run passed 54 checks. Existing Debug output was locked by an already running application, so verification used Release output without stopping that application. The sandbox could not authenticate to SQL Express or reliably launch the test host; the successful SQL and test runs used approved execution outside the sandbox. Only the new demo database was created/migrated. No live public tunnel or real PayHere transaction was used. NuGet emitted existing transitive System.Security.Cryptography.Pkcs 9.0.4 vulnerability warnings (NU1902/NU1903); dependency remediation was not included in this payment change.
