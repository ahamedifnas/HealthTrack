# C# ASP.NET MVC architecture

HealthTrack runs as one ASP.NET Core MVC application. C# controllers and services implement application behavior; Razor views render prepared data and ordinary HTML forms. Bootstrap CSS provides styling. There is no Node.js, Python, PHP, Firebase, MongoDB, or separate JavaScript backend.

## Application logic

| Concern | C# implementation |
| --- | --- |
| BMI | `MeasurementsController` rounds weight/height and calculates BMI before saving. Posted BMI and ownership values are ignored. |
| Calories | Controllers validate recorded calorie values; dashboard/progress controllers calculate seven-day totals using EF queries. Workout logs and measurement entries remain separate, avoiding double counting. |
| Progress | `ProgressController` prepares chronological trend rows and weight/BMI differences between consecutive recorded dates; `ProgressViewModel` supplies latest and historical measurements. Missing dates are not treated as zero. |
| Dashboard counts | `DashboardController` and `AdminDashboardData` query SQL Server through EF. Dashboard view models prepare metric labels and values. |
| Appointments | `AppointmentsController` enforces participants, date/time rules, allowed status transitions, slot conflicts and concurrency. Appointment view models prepare permitted UI actions. |
| Workouts and diets | Trainer controllers handle plan/exercise/meal CRUD and status changes; client controllers validate and save logs. `PlanViewModel` prepares ordered exercises, grouped meals and status presentation. |
| Trainer assignments | `AdminAssignmentsController` validates active roles and persists the existing `TrainerClient` entity. The unique client index prevents duplicate assignments. `PlanAccess` scopes trainer queries to assigned clients. |
| Authorization | ASP.NET Identity, authorization attributes/policies, and ownership-scoped EF queries protect every operation. Hiding a link is not the security boundary. |
| Validation | C# DataAnnotations, `IValidatableObject`, controller checks and `ModelState`; SQL Server constraints also prevent conflicting writes. POST forms retain antiforgery protection. |
| Navigation | `NavigationViewComponent` prepares role-specific links and the user's display name in C#. Razor does not query Identity or the database. |
| Notifications | `NotificationService` stages appointment and plan notifications in the same EF save as the event. `NotificationsController` enforces per-user ownership on inbox and mark-read requests; navigation queries the signed-in user's unread count. See `NOTIFICATIONS.md`. |
| Reports | `ReportsController` enforces roles and disables response caching. `ReportService` rechecks trainer assignments, scopes EF queries, calculates summaries and prepares display tables in C#. Admin receives system-wide aggregates. Shared Razor rendering and print CSS contain no business rules. See `REPORTS.md`. |

## Forms and presentation

- Admin Dashboard **+ Add Trainer** and **Edit** open MVC pages. Invalid submissions return the form with C# validation errors. Success returns to the dashboard.
- Workout logging uses a GET **Load exercises** form to select a plan, followed by a POST form to save the log. Exercise ownership is validated again on POST.
- Exercise/meal deletion and appointment cancellation use server-rendered confirmation pages. GET requests do not mutate data; POST actions recheck permissions and state.
- Progress uses an accessible Razor table with recorded values and C#-calculated changes. Chart.js and canvas charts are removed.
- Navigation wraps on smaller screens without requiring a JavaScript collapse widget.

## JavaScript dependencies

**None remain in the application's source assets or rendered pages.** Chart.js, custom scripts, jQuery, jQuery Validation, Unobtrusive Validation and Bootstrap JavaScript were removed. Bootstrap CSS and its license remain. Validation and CRUD work without JavaScript.

## Database

SQL Server is the only database. `Program.cs` registers `ApplicationDbContext` with `UseSqlServer`, using `ConnectionStrings:DefaultConnection`. The checked-in configuration targets `(localdb)\MSSQLLocalDB`, database `HealthTrackDB`. Identity also uses EF stores in this same context. Application database access uses Entity Framework Core; no alternate database provider was added.

This refactor does not require a new database migration. Existing data, migrations and assignments are retained. PowerShell smoke tests use SQL Server directly only for test fixtures and verification. The development setup script is a compatibility launcher: `DevelopmentAssignment.cs` implements its validation, role changes, security stamp refresh and assignment persistence through Identity and EF in a transaction. The C# command is restricted to the Development environment and is not an HTTP endpoint.

The command can also run without PowerShell from the application directory:

```text
dotnet run --no-launch-profile -- --environment Development --Assignment:Run true --Assignment:TrainerEmail trainer@example.com --Assignment:ClientEmail client@example.com --Assignment:PromoteTrainer true
```

See [NON-CSHARP-FILES.md](NON-CSHARP-FILES.md) for every remaining non-C# source file and its purpose. Build outputs and IDE caches are generated artifacts, not application source.

## Verification

Build from the repository root: `dotnet build HealthTrack/HealthTrack.csproj`.

The `TrainerManagement`, `HealthProgress`, `Phase3`, `Phase4`, `Assignments`, and `DashboardPlans` scripts under `Tests` exercise the app via HTTP against local SQL Server. Run each with `-BaseUrl` pointing to the running application. Test accounts and records use unique names and are cleaned up afterward.
