# Coursework change inventory

Paths are relative to HealthTrack/. The workspace already contained uncommitted changes before this task. This inventory lists this task's additions and touched existing files; it does not claim ownership of the other pre-existing edits visible in git status. No commit, replacement of completed features or project restructuring was performed.

## Added application, migration and test files

- Controllers/AdminHealthcareProfessionalsController.cs
- Models/HealthcareProfessional.cs
- Models/ReminderDelivery.cs
- Models/Admin/HealthcareProfessionalForms.cs
- Services/ReminderService.cs
- Services/ReminderFilter.cs
- Services/AnalyticsService.cs
- Views/AdminHealthcareProfessionals/Index.cshtml
- Views/AdminHealthcareProfessionals/Create.cshtml
- Views/AdminHealthcareProfessionals/Edit.cshtml
- Tests/Coursework.Smoke.ps1
- Tests/FinalVerification.ps1
- Migrations/20261007133007_HealthcareProfessionalAppointments.cs
- Migrations/20261007133007_HealthcareProfessionalAppointments.Designer.cs
- Migrations/20261007133224_ReminderDeliveryLedger.cs
- Migrations/20261007133224_ReminderDeliveryLedger.Designer.cs

## Modified existing files

- Program.cs
- Data/ApplicationDbContext.cs
- Data/IdentitySeeder.cs
- Models/AppRoles.cs
- Models/Appointments/Appointment.cs
- Models/Appointments/AppointmentForms.cs
- Controllers/AdminController.cs
- Controllers/AppointmentsController.cs
- Controllers/DashboardController.cs
- Controllers/NotificationsController.cs
- Controllers/ProgressController.cs
- Controllers/ReportsController.cs
- Services/AdminDashboardData.cs
- Services/PlanAccess.cs
- Services/ReportService.cs
- ViewComponents/NavigationViewComponent.cs
- Views/Admin/Appointments.cshtml
- Views/AdminReports/Print.cshtml
- Views/Appointments/Book.cshtml
- Views/Appointments/Details.cshtml
- Views/Appointments/Index.cshtml
- Views/Dashboard/Client.cshtml
- Views/Reports/Index.cshtml
- Views/Shared/_DashboardAppointments.cshtml
- Migrations/ApplicationDbContextModelSnapshot.cs
- Tests/Database.Smoke.ps1
- Tests/DashboardUi.Smoke.ps1
- Tests/Reports.Smoke.ps1

## Documentation

- Documentation/Architecture.md
- Documentation/Class-Diagram.md
- Documentation/Classes-Properties-Methods.md
- Documentation/ER-Diagram.md
- Documentation/Installation-Guide.md
- Documentation/Logical-Solution.md
- Documentation/Reflection-Template.md
- Documentation/Security-and-Validation.md
- Documentation/Testing.md
- Documentation/Use-Case-Diagram.md
- Documentation/User-Manual.md
- Documentation/Viva-Questions.md
- Documentation/Change-Summary.md (this inventory)
- Documentation/Test-Results/ (executed verification evidence)

## Database changes

HealthcareProfessionalAppointments adds HealthcareProfessionals, optional HealthcareProfessionalId and nullable TrainerId, an exactly-one-provider check constraint and a filtered healthcare conflict index. Existing trainer records remain intact. ReminderDeliveryLedger adds ReminderDeliveries with recipient/key deduplication independent of inbox deletion. Both migrations were applied successfully to the existing local HealthTrackDB. EF reported no pending model changes on the final compiled model.

## Runtime behavior and limits

Reminder delivery occurs on relevant authenticated GET visits, uses Asia/Colombo by default, skips terminal/passed appointments, and notifies both active appointment participants. Plan reminders use status plus inclusive date range and are once per day per client/type. No scheduler, email or SMS delivery is added. Analytics reuse ReportViewModel and existing report/print screens. Healthcare profiles use the existing Identity account for name/email/active status and the same AppointmentsController workflow for consultations. Provider status/ownership and mixed-role checks remain server-side.

See Testing.md for verified build/test results and unexecuted manual checks. Screenshot placeholders and the personal reflection prompts intentionally require the student's own evidence/experience.

## Final verification

Restore succeeded; the entire HealthTrack.slnx build succeeded with 0 warnings and 0 errors. EF reported no pending model changes. All 13 suites and 599 assertions passed on the final build. See Testing.md for scope, logs, initial test-expectation adjustments and manual checks still required.
