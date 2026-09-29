# Security and authorization review

Reviewed 2026-09-29. Application logic remains C#, using ASP.NET Identity, Entity Framework Core and SQL Server. No JavaScript business logic, database provider changes or functionality removals were introduced.

## Findings and changes

The existing authorization and ownership controls were already present. No cross-user authorization bypass was identified in the reviewed routes. Both trainer plan status endpoints now explicitly require a bound status and valid ModelState in addition to checking the enum. This is defense in depth: the existing enum check already rejected the current enum's zero default.

Modified files:

- `Controllers/TrainerWorkoutsController.cs`: require status binding and reject invalid ModelState before changing workout plan status.
- `Controllers/TrainerDietsController.cs`: apply the same validation to diet plan status changes.
- `Tests/Security.Smoke.ps1`: verify omitted, empty, malformed and undefined statuses return 400 without changing records; explicitly test negative weight, height and calories and a tampered anti-forgery token; clean up test measurements.
- `SECURITY-REVIEW.md`: record review scope, authorization boundaries, validation, test checklist and results.

## Authorization and ownership inventory

| Controllers / actions | Server-side boundary |
| --- | --- |
| Account | Registration/login/access denied are anonymous; logout requires authentication and POST. Registration always assigns Client, ignoring posted roles/profile ownership. |
| Dashboard | Authenticated entry redirects by role; Client and Trainer policies separate their dashboards; Admin requires Admin role. |
| Admin, AdminTrainers, AdminAssignments, AdminReports | Admin role required on controllers, including mutations and print reports. Trainer IDs must identify trainers; assignment IDs must identify active eligible clients/trainers. |
| Clients | Trainer policy; live assignments filtered by signed-in Identity ID and active Client role. |
| TrainerWorkouts, TrainerDiets | Trainer policy; plans require both trainer ownership and a live client assignment. Exercise/meal IDs are checked through owned parent plans. Create checks assignment; edit cannot move a plan to another client. Workout log history checks assignment and owned plans. |
| Workouts, Diets | Client policy; plans and histories filtered by signed-in Identity ID. Log POST derives ClientId from identity and validates parent plan ownership; exercise must belong to that plan. |
| Measurements | Client policy; history and creation use signed-in Identity ID; target weight updates only that Identity user. BMI is calculated in C#. No record-ID edit/delete endpoint exists. |
| Progress | Clients can view only themselves; trainers require a live assignment; individual Admin access is denied. Summary requires Admin role. |
| Reports | Admin reports require Admin; trainer reports require Trainer policy plus a live assignment. Plan/log reports additionally filter trainer-owned plans; appointment reports filter trainer ID. |
| Appointments | Client access filters ClientId; trainer access filters TrainerId; Admin reads all. Booking and client cancellation require Client policy; edit/approve/complete/trainer cancellation require Trainer policy and appointment ownership. |
| Notifications | Client/Trainer role required; list and MarkRead filter UserId by signed-in Identity ID. |
| Home | Public informational pages and safe error rendering only. |

Client/Trainer feature policies handle mixed roles explicitly: Admin does not inherit those features, and Trainer does not inherit Client features. A fallback authentication policy protects endpoints without an explicit policy. Navigation visibility is supplementary.

Workout logs, meal logs and measurements have no individual record-ID read/edit/delete routes; their history queries are scoped to the signed-in client or authorized trainer. Assignment creation is Admin-only and cannot silently replace another trainer's assignment.

Appointment assignment is independent of plan assignment: clients may book an active trainer; only that appointment's trainer may manage it. This existing workflow does not grant the trainer access to the client's plans or health progress.

## Other verified controls

- Forms use C# DataAnnotations and server-side ModelState checks for names, email, password confirmation, numeric bounds, enum values and required dates. Whitespace-only required names fail validation. Weight/height bounds protect BMI calculation. Calories and exercise/diet values reject negatives. Plan/log date ranges and future appointment times are checked server-side using the configured appointment time zone.
- All MVC unsafe requests have global `AutoValidateAntiforgeryToken` protection. Mutation controllers also use controller-level auto-validation; Account POST actions explicitly use `ValidateAntiForgeryToken`. Razor Form Tag Helpers generate tokens for POST forms. The sole ignore attribute is on the read-only generic error renderer so failed POST requests can render safely.
- Identity creates and hashes passwords; application code does not store plaintext or assign PasswordHash directly. Identity enforces password complexity, unique emails and failed-login lockout. Development Admin seeding reads credentials exclusively from user-secrets, only in Development. No hardcoded password was found in C# or appsettings. Registration/trainer creation tests verify stored values are hashes; this is not a forensic audit of every historical database row.
- Application database operations use EF Core/Identity parameterized queries. No user-input SQL concatenation or application raw SQL command execution was found. Parameterized SQL in PowerShell integration tests is test setup/assertion/cleanup, not application logic.
- A safe exception handler renders a standalone friendly error page with request ID, without exception/configuration details. Private responses use no-store. Server logs retain diagnostic details.
- Login rejects inactive users. Cookie validation rechecks active status and Identity security stamps on every request, refreshing role claims so deactivation and revoked roles affect existing sessions.

## Security test checklist

Use two Clients, two Trainers with separate assignments, an Admin and an anonymous browser. For rejected mutations, verify the database is unchanged. Cookie authorization may redirect to AccessDenied before the final 403 response.

| Test | Action | Expected result |
| --- | --- | --- |
| A | Client opens `/Dashboard/Admin` | 403; no Admin content. |
| B | Client opens `/TrainerWorkouts`, `/TrainerDiets`, `/Clients`, `/Dashboard/Trainer` | 403. |
| C | Client substitutes another client's workout/diet/appointment ID, posts another plan ID, or requests another client in Progress | 404; no foreign data or mutation. Histories remain scoped despite forged IDs. |
| D | Trainer substitutes another trainer's client/plan/exercise/meal/appointment ID in GET and POST requests | 404 or form validation failure; no foreign data or mutation. Revoking an assignment removes plan/progress/report access. |
| E | Anonymous user opens `/Dashboard` or private health/report pages | Login required. |
| F | Book/reschedule in the past, omit time, or post malformed date/time | Validation error; no appointment saved. Reversed plan dates also fail. |
| G | Post WeightKg=-1, HeightCm=0/-1 or invalid target weight | Validation error; no measurement/target change. Posted BMI is ignored. |
| H | Post negative calories in measurement, workout/meal log, exercise, diet plan or meal forms | Validation error; no invalid record saved. |
| I | Register an existing email, including a case variant | Rejected; no duplicate account or role escalation. |
| J | Deactivate a logged-in user; request private pages and attempt login | Existing session rejected and login fails. |
| K | POST without token, with altered token, or another session's token | 400; no state change. Valid token succeeds only with correct role and ownership. |
| L | Admin opens Admin dashboard/user/trainer/assignment/report features; Client/Trainer attempts same routes | Admin authorized; other roles denied, including POST actions. |

Additional regression checks cover role/security-stamp revocation, notification ownership, report ownership, overposting, missing plan/log dates, invalid statuses, concurrency and preservation of valid workflows.

## Verification results

- `dotnet build HealthTrack/HealthTrack.csproj`: succeeded, zero warnings and zero errors, including after resuming the review.
- `Security.Smoke.ps1`: all 50 checks passed against SQL Server LocalDB.
- Completed before interruption: Phase1, TrainerManagement (58 checks), HealthProgress (48 checks), Phase3.
- Completed after resuming: Phase4 (63 checks), Assignments (41), DashboardPlans (22), Notifications (37), Reports (92). All passed.
- ErrorHandling: all three real exception requests returned friendly HTTP 500 pages without internal exception/configuration details, using a separate Development instance with an intentionally invalid time zone.
- `git diff --check`: passed. Integration verification used local SQL Server LocalDB; production hosting/TLS configuration was not exercised.
