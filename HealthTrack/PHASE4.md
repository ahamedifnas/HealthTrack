# Phase 4: appointments

## Implemented behavior

- Clients can book a future appointment with any active Trainer account, enter a reason, view their own appointments and cancel their own Pending requests. Booking does not create or change workout/diet client assignments.
- Trainers see only appointments whose TrainerId matches their sign-in. They can approve Pending requests, update/reschedule Pending or Approved appointments, add shared notes, cancel Pending/Approved appointments and complete an Approved appointment once its scheduled time has arrived.
- Admin can view all appointments, including details, but cannot book or invoke any mutation, even if the account also has Client/Trainer roles.
- Completed and Cancelled appointments are terminal and remain available in history. Rescheduling preserves the existing Pending/Approved status; it does not change the client, trainer or original reason.
- All writes use POST with antiforgery validation. Every details/edit/cancel/status action checks ownership on the server. Form models do not accept ownership or status fields. Unauthorized roles receive access denied; another user's record returns 404.
- Reason is required (maximum 1,000 characters). Trainer notes are optional (maximum 2,000 characters) and visible to the Client and Admin.

## Date, time and booking rules

`Appointments:TimeZoneId` in appsettings.json defaults to `Asia/Colombo`. All appointment date/time inputs, displays, future checks and today's dashboard count use that zone, independently of the server's local time. AppointmentDate and AppointmentTime store the selected wall-clock values; CreatedAt is UTC. For this coursework database, keep the time zone consistent once bookings exist.

Booking and rescheduling require a strictly future date/time, with whole-minute precision. SQL filtered unique indexes prevent either participant from having two Pending/Approved appointments at the exact same start time, including concurrent requests. Cancelled slots can be booked again. There is no appointment duration or availability calendar in this phase, so this is exact-start-time conflict checking, not interval overlap detection.

A SQL rowversion also protects concurrent updates; a stale edit must be reloaded before retrying. No appointment records are deleted by the UI.

## Dashboards and navigation

- Client: next future Approved appointment, ordered by date/time; Appointments and Book Appointment links.
- Trainer: today's non-Cancelled appointment count and total Pending count, both scoped to that trainer; Appointments link.
- Admin: total appointments across all statuses; Appointments link to the read-only list.
- Existing placeholders for unrelated features remain unchanged. The navigation collapses below the Bootstrap xl breakpoint to accommodate the additional links.

## Setup

Run from the project directory:

```powershell
dotnet tool restore
dotnet build
dotnet ef database update -- --environment Development
dotnet run --launch-profile https
```

Open `https://localhost:7041`. Use the Trainer and Client accounts from Phase 3 and the existing development Admin account. If you need test accounts, register two accounts and use the Phase 3 development setup script to promote one to Trainer (replace both emails with the registered accounts):

```powershell
powershell -NoProfile -File Scripts\Set-DevelopmentAssignment.ps1 -TrainerEmail "trainer@example.com" -ClientEmail "client@example.com" -PromoteTrainer
```

Log out before promotion and log in again afterward to refresh role claims. The script also creates a plan assignment, but appointments do not require that assignment. Admin credentials remain configured through the Phase 1 user-secrets/environment setup; no passwords were added to source.

## Complete browser test flow

1. Log in as Client. Open **Book Appointment**, choose a Trainer, a future date/time and a reason, then submit. The details page should show **Pending**.
2. Try a past date/time, blank reason, and a conflicting start time; each should show validation errors. View **Appointments** to confirm only your own records appear.
3. In a separate browser/private session, log in as the selected Trainer. Open **Appointments → Details** and **Approve** the request. The Pending dashboard count should decrease.
4. Return to the Client dashboard. The next upcoming Approved appointment should appear. Client cancellation is no longer available after approval.
5. As Trainer, use **Update / reschedule** to choose another future date/time and add notes. Verify the changes from the Client account; approval status is preserved.
6. To test completion without waiting a day, initially book/reschedule a few minutes ahead. Once that scheduled time has arrived, refresh details as Trainer and select **Mark Completed**. The record should remain in history and disappear from the Client's upcoming appointment card. Future appointments cannot be completed early.
7. Create another Pending request and cancel it as Client. Separately test Trainer cancellation on Pending and Approved requests. Completed/Cancelled appointments cannot be reopened or edited.
8. Log in as Admin. Confirm the total dashboard count and the all-appointments list/details. No appointment edit, approval, cancellation or completion controls should be available.
9. With a second Trainer/Client pair, try changing details/edit/cancel IDs to the first pair's appointment IDs. Cross-owner requests must fail; the automated test below also checks forged POST requests and Admin accounts with multiple roles.

## Verification performed

- Baseline and final `dotnet build`: passed, zero warnings/errors.
- Migration `20260927162051_AppointmentSystem` applied to `(localdb)\MSSQLLocalDB`, database `HealthTrackDB`.
- EF reported no pending model changes after migration generation.
- All 63 HTTP/database integration checks passed. These include future-date validation, duplicate slots, ownership, role restrictions, status transitions, stale edits, shared notes and all three dashboards.
- Temporary test accounts and appointments were removed. The completion test simulated time passing only on its own temporary appointment.
- Forms were tested through HTTP; visual browser layout was not automated.

To repeat the integration test:

```powershell
# Terminal 1
dotnet run --launch-profile http
# Terminal 2
powershell -NoProfile -File Tests\Phase4.Smoke.ps1
```

The script targets the default local development database and reads the appointment time zone from appsettings.json. Run it with the app using that same configuration.

## Files modified

```text
Controllers/DashboardController.cs
Data/ApplicationDbContext.cs
Migrations/ApplicationDbContextModelSnapshot.cs
Models/Dashboard/AdminDashboardViewModel.cs
Models/Dashboard/ClientDashboardViewModel.cs
Program.cs
Views/Dashboard/Admin.cshtml
Views/Dashboard/Client.cshtml
Views/Dashboard/Trainer.cshtml
Views/Shared/_Layout.cshtml
Views/_ViewImports.cshtml
appsettings.json
```

## Files created

```text
Controllers/AppointmentsController.cs
Migrations/20260927162051_AppointmentSystem.cs
Migrations/20260927162051_AppointmentSystem.Designer.cs
Models/Appointments/Appointment.cs
Models/Appointments/AppointmentForms.cs
Models/Dashboard/TrainerDashboardViewModel.cs
Services/AppointmentClock.cs
Tests/Phase4.Smoke.ps1
Views/Appointments/Book.cshtml
Views/Appointments/Details.cshtml
Views/Appointments/Edit.cshtml
Views/Appointments/Index.cshtml
Views/Shared/_AppointmentScheduleFields.cshtml
Views/Shared/_AppointmentStatus.cshtml
PHASE4.md
```

Health measurements, BMI, analytics, notifications and reports are not implemented in this phase.
