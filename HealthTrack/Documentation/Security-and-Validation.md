# Security and validation review

## Authentication and account handling

Program.cs registers ASP.NET Identity with EF Core stores, unique emails and password requirements (minimum eight, uppercase, lowercase, digit, non-alphanumeric). Passwords are hashed by Identity through UserManager.CreateAsync/ResetPasswordAsync; no application password encryption/decryption method is used. AccountController.Login enables lockout after five failed attempts for fifteen minutes. Public registration assigns only Client. Administrator and provider account creation uses server-chosen roles.

SecurityStampValidator runs on every request. The cookie validator additionally rejects missing/inactive accounts. Healthcare deactivation updates the security stamp through UserManager. IdentitySeeder reads development administrator credentials directly from user-secrets and rejects elevation of a Client/Trainer/HealthcareProfessional seed email. No real credentials were added to source. Production requires secure deployment configuration and controlled admin provisioning.

## Authorization and ownership

- An authenticated fallback policy covers MVC endpoints unless AllowAnonymous is declared. Admin controllers use the Admin role.
- FeaturePolicies.Client excludes Admin, Trainer and HealthcareProfessional memberships. TrainerFeatures requires Trainer and excludes Admin/HealthcareProfessional. ProviderFeatures allows Trainer or HealthcareProfessional and excludes Admin.
- AppointmentsController.VisibleAppointments scopes lists/details to ClientId, TrainerId or HealthcareProfessionalId according to role. Management actions repeat the provider ID and role condition, and booking uses the authenticated client's ID. A posted ClientId, Status or provider owner cannot overwrite these values.
- Healthcare professionals can only manage their selected appointments and own notifications; they cannot access measurements, progress, workout/diet management or trainer/admin reports. Progress explicitly denies the healthcare role, including mixed Client membership.
- MeasurementsController uses OwnMeasurements; WorkoutsController/DietsController filter by current ClientId. Notification reads and mutations always include UserId.
- PlanAccess requires current active assigned Client membership and excludes privileged admin/trainer/healthcare clients. Trainer plan and report queries also require plan.TrainerId to match the acting trainer. AnalyticsService applies this scope before grouping/counting; it never trusts a trainer ID from the query string.
- AdminController preserves accounts with dependent records, including the new healthcare profile, and protects self/last active administrator.

## Request and output protections

Global AutoValidateAntiforgeryToken validates unsafe MVC requests, including the new healthcare admin POSTs and retained appointment POSTs. Razor form Tag Helpers generate hidden tokens; logout and changes use POST. Confirmation GETs do not perform the confirmed deletion. Reminders are an explicitly request-triggered exception: selected authenticated GETs generate only system-derived due messages.

Razor encodes text output. Forms bind dedicated models rather than entire EF entities. Database work uses parameterized EF LINQ/SaveChanges; no application SQL string concatenation based on input was introduced. Migration SQL contains fixed schema expressions only. Test scripts use parameterized SQL for fixture values and are not application endpoints.

ResponseCache NoStore is applied globally. SafeExceptionHandler logs the internal exception and returns a generic error with request ID; it does not render connection strings, stack traces or passwords. HTTPS redirection is configured, and HSTS is enabled outside Development. Use the HTTPS launch profile/trusted certificate and correct production reverse-proxy configuration. HTTP-only automated tests do not prove certificate trust or HTTPS deployment behavior. No database encryption-at-rest, MFA, email verification workflow or security audit trail is claimed.

## Validation rules in the affected workflows

| Input / state | Server enforcement |
|---|---|
| Provider | BookAppointmentForm.Validate rejects both/neither; active trainer role or active healthcare profile/role is queried again on POST. Admin providers are excluded. SQL XOR check constraint protects the stored row. |
| Appointment date/time | Required nullable date/time form properties; model binding rejects malformed values; ValidateFutureSchedule requires future wall-clock time and whole minutes. |
| Provider/client conflict | Unique filtered indexes on provider+date+time and client+date+time for Pending/Approved. SqlException 2601/2627 becomes a clean form message; no partial event notification commits. No duration-based overlap calculation is claimed. |
| Appointment edits | Provider ownership, Pending/Approved state, future time and eight-byte base64 rowversion. Stale writes return a reload message. Client EditRequest cannot change provider and revalidates that provider's active status. |
| Lifecycle | Approval requires future Pending; completion requires Approved and scheduled time reached; Cancelled/Completed cannot be rescheduled/cancelled again. Clients cancel only Pending. |
| Measurement | Weight 1?500 kg, height 30?300 cm, calories 0?50000, optional body fat 0?100 and heart rate 1?300. Date 1900-01-01 through today, unique own client/date. BMI calculated after normalization. |
| Nutrition/activity | Form ranges reject negative calories/nutrients. MealLog calories allow zero; WorkoutLog duration/calories require positive values. Plans require name, valid date range and eligible client. Meal quantities use PositiveQuantityAttribute. |
| Text | Names/specialty/plan names bounded at 100; email 256; reason 1000; notes/descriptions usually 2000; food 150; quantity 100. Required attributes reject empty/whitespace values. Razor shows field/summary errors. |
| Reminders | Eligible status/date/time, active recipient and date-active plan checks. Serializable transaction and unique delivery key prevent duplicates; notification and ledger are atomic. Ledger retained after inbox deletion. |

## Review evidence and limitations

See Testing.md and Test-Results for executed positive/negative HTTP and database checks. These are coursework regression checks, not a penetration test. Request-triggered delivery requires a visit; no background/email guarantee exists. Provider active-status checks and appointment conflict indexes address different problems: deactivated accounts lose access and cannot be newly selected, while existing appointment history is retained. Concurrent administrative deactivation exactly during another transaction is not a dedicated tested race.
