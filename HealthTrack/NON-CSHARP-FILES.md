# Remaining non-C# files

Inventory of all non-ignored source files, including this report. Paths are relative to the repository root. No JavaScript, TypeScript, Python, PHP, Node package manifest, Firebase or MongoDB application files remain.

All application rules run in C# under ASP.NET Core MVC. Razor only renders prepared data and HTML forms. SQL Server (including LocalDB for development) is the only configured database provider; Identity and application persistence use Entity Framework Core.

PowerShell tests are development tools, not application logic. The assignment script only launches the C# command. Vendor CSS variants and source maps are retained distribution/debugging assets; only bootstrap.min.css is referenced by the layout. These optional assets are not needed to execute application logic.

Generated bin/ and obj/ files (assemblies, runtime metadata, caches, generated styles and static asset manifests), .vs/ IDE state, and .git/ repository metadata are excluded from this source inventory. They are produced by .NET/Visual Studio/Git and do not introduce another application language or database.

| File | Purpose |
| --- | --- |
| HealthTrack/SECURITY-REVIEW.md | Authorization/ownership audit, security fixes, test checklist and changed-file inventory. |
| HealthTrack/Tests/Security.Smoke.ps1 | Regression tests for authorization refresh, invalid inputs, authentication defaults and generic errors. |
| HealthTrack/Tests/ErrorHandling.Smoke.ps1 | Controlled exception-path verification against a separately configured test instance. |
| HealthTrack/Views/AdminReports/Print.cshtml | Standalone Razor rendering of database-backed Admin totals for printing. |
| HealthTrack/wwwroot/css/admin-report-print.css | A4 portrait print presentation without navigation, footer or controls. |
| HealthTrack/REPORTS.md | Reports scope, manual/automated testing instructions and module file list. |
| HealthTrack/Tests/Reports.Smoke.ps1 | HTTP integration tests and temporary SQL Server test fixtures; not application runtime logic. |
| HealthTrack/Views/Reports/Index.cshtml | Razor rendering of the authorized Client list and report links. |
| HealthTrack/Views/Reports/Report.cshtml | Shared Razor rendering of C#-prepared report cards and tables. |
| HealthTrack/wwwroot/css/reports.css | Screen table styling and CSS-only print layout. |
| HealthTrack/NOTIFICATIONS.md | Notification behavior, manual testing scenarios and module file list. |
| HealthTrack/Tests/Notifications.Smoke.ps1 | HTTP integration tests with temporary SQL Server fixtures; not application runtime logic. |
| HealthTrack/Views/Notifications/Index.cshtml | Razor inbox rendering and mark-read HTML forms; application rules remain in C#. |
| .gitignore | Source-control exclusions for generated outputs, IDE files and local settings. |
| HealthTrack.slnx | Visual Studio/.NET solution project list. |
| HealthTrack/ADMIN.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
| HealthTrack/appsettings.Development.json | ASP.NET configuration: logging, SQL Server connection, appointment time zone and allowed hosts as applicable. |
| HealthTrack/appsettings.json | ASP.NET configuration: logging, SQL Server connection, appointment time zone and allowed hosts as applicable. |
| HealthTrack/ARCHITECTURE.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
| HealthTrack/dotnet-tools.json | .NET EF command-line tool version manifest; no Node tooling. |
| HealthTrack/HEALTH-PROGRESS.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
| HealthTrack/HealthTrack.csproj | .NET project/build configuration and EF/Identity NuGet package references. |
| HealthTrack/NON-CSHARP-FILES.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
| HealthTrack/PHASE1.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
| HealthTrack/PHASE3.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
| HealthTrack/PHASE4.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
| HealthTrack/Properties/launchSettings.json | Local .NET launch profiles, ports and environment settings. |
| HealthTrack/Scripts/Set-DevelopmentAssignment.ps1 | Compatibility launcher for the C# development assignment command; contains no role, assignment or database rules. |
| HealthTrack/Tests/Assignments.Smoke.ps1 | Development smoke-test harness: HTTP requests, assertions and SQL Server test fixtures/cleanup; not application runtime code. |
| HealthTrack/Tests/DashboardPlans.Smoke.ps1 | Development smoke-test harness: HTTP requests, assertions and SQL Server test fixtures/cleanup; not application runtime code. |
| HealthTrack/Tests/DevelopmentAdmin.Smoke.ps1 | Development smoke-test harness: HTTP requests, assertions and SQL Server test fixtures/cleanup; not application runtime code. |
| HealthTrack/Tests/HealthProgress.Smoke.ps1 | Development smoke-test harness: HTTP requests, assertions and SQL Server test fixtures/cleanup; not application runtime code. |
| HealthTrack/Tests/Phase1.Smoke.ps1 | Development smoke-test harness: HTTP requests, assertions and SQL Server test fixtures/cleanup; not application runtime code. |
| HealthTrack/Tests/Phase3.Smoke.ps1 | Development smoke-test harness: HTTP requests, assertions and SQL Server test fixtures/cleanup; not application runtime code. |
| HealthTrack/Tests/Phase4.Smoke.ps1 | Development smoke-test harness: HTTP requests, assertions and SQL Server test fixtures/cleanup; not application runtime code. |
| HealthTrack/Tests/TrainerManagement.Smoke.ps1 | Development smoke-test harness: HTTP requests, assertions and SQL Server test fixtures/cleanup; not application runtime code. |
| HealthTrack/TRAINER-ASSIGNMENTS.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
| HealthTrack/Views/_ViewImports.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/_ViewStart.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Account/AccessDenied.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Account/Login.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Account/Register.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Admin/Appointments.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Admin/Clients.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Admin/Users.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/AdminTrainers/Create.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/AdminTrainers/Edit.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/AdminTrainers/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Appointments/Book.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Appointments/Details.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Appointments/Edit.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Appointments/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Clients/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Dashboard/Admin.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Dashboard/Client.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Dashboard/Trainer.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Diets/Details.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Diets/History.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Diets/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Diets/Log.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Home/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Home/Privacy.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Measurements/Create.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Measurements/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Measurements/TargetWeight.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Progress/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Progress/Summary.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/_AdminUsersTable.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/_AppointmentScheduleFields.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/_AppointmentStatus.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/_DietMeals.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/_Layout.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/_Layout.cshtml.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/Views/Shared/_MeasurementHistory.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/_PlanFields.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/_WorkoutExercises.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/Components/Navigation/Default.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/Confirm.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Shared/Error.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/TrainerDiets/Details.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/TrainerDiets/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/TrainerDiets/MealForm.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/TrainerDiets/PlanForm.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/TrainerWorkouts/Details.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/TrainerWorkouts/ExerciseForm.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/TrainerWorkouts/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/TrainerWorkouts/PlanForm.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Workouts/Details.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Workouts/History.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Workouts/Index.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/Views/Workouts/Log.cshtml | Razor rendering template: HTML, form bindings, display conditions and iteration over prepared view data. |
| HealthTrack/wwwroot/css/site.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/favicon.ico | Browser favicon image. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap.min.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap.min.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap.rtl.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap.rtl.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap.rtl.min.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap.rtl.min.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-grid.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-grid.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-grid.min.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-grid.min.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-grid.rtl.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-grid.rtl.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-grid.rtl.min.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-grid.rtl.min.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.min.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.min.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.rtl.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.rtl.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.rtl.min.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-reboot.rtl.min.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.min.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.min.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.rtl.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.rtl.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.rtl.min.css | Presentation styles (site/layout CSS or retained Bootstrap CSS distribution variant). |
| HealthTrack/wwwroot/lib/bootstrap/dist/css/bootstrap-utilities.rtl.min.css.map | Optional Bootstrap CSS source map for browser styling diagnostics; no executable application logic. |
| HealthTrack/wwwroot/lib/bootstrap/LICENSE | Required license notice for redistributed Bootstrap assets. |
| PHASE2-TESTING.md | Developer documentation, setup/test instructions or architecture/inventory report; not runtime code. |
