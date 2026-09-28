# Notifications

Notifications are generated in C# and persisted through Entity Framework Core to SQL Server HealthTrackDB. The event and notification share one SaveChanges transaction, so a rejected booking or concurrency conflict cannot leave a notification for an unsuccessful change. No JavaScript, external notification service or additional database is used.

Migration: `20260928145118_Notifications`. Adds the seven requested fields, a user foreign key and an index for per-user unread queries. CreatedAt is UTC. Notifications are removed when their recipient account is deleted. Existing appointments/plans are not backfilled.

Clients and Trainers open **Notifications** in the navigation bar. The green badge shows their unread count, refreshed on page navigation/reload. The inbox shows newest first. Opening it does not mark messages read. **Mark as read** submits an antiforgery-protected POST. Both the list and update query use the authenticated user ID; a posted UserId cannot change ownership. Other users' IDs return 404. No notification creation endpoint is exposed.

## Manual scenarios

Use separate browser sessions for an active Client and Trainer. Assign the Client to the Trainer through the Admin dashboard before testing plans. Refresh the recipient page after each action.

| Action | Expected notification |
| --- | --- |
| Client books a future appointment | Trainer receives **Appointment requested**. |
| Trainer approves a Pending appointment | Client receives **Appointment approved**. |
| Trainer changes the date/time of a Pending or Approved appointment | Client receives **Appointment rescheduled**, with the new time and configured appointment time zone. Notes-only edits do not create a reschedule notification. |
| Client cancels their Pending appointment | Trainer receives **Appointment cancelled**. |
| Trainer cancels a Pending or Approved appointment | Client receives **Appointment cancelled**. |
| Trainer creates a Workout Plan for their assigned Client | Client receives **Workout plan assigned**. |
| Trainer edits the Workout Plan, adds/edits/deletes an exercise, or changes plan status | Client receives **Workout plan updated**. Reposting the same status creates no notification. |
| Trainer creates a Diet Plan for their assigned Client | Client receives **Diet plan assigned**. |
| Trainer edits the Diet Plan, adds/edits/deletes a meal, or changes plan status | Client receives **Diet plan updated**. Reposting the same status creates no notification. |
| Recipient clicks Mark as read | Status becomes Read and unread badge decreases by one. Repeating the POST leaves it read. |
| Another Client opens Notifications with a forged userId query | Only that Client's own inbox appears. |
| Another Client posts a recipient's notification ID to MarkRead | HTTP 404; notification remains unchanged. A missing antiforgery token returns 400. GET cannot mutate read state. |
| Invalid/conflicting booking, invalid plan edit, stale appointment edit or repeated approval | No notification is generated for the rejected operation. |

## Automated verification

From the repository root: `dotnet build`.

From HealthTrack, start the development application and run:

```powershell
dotnet run --no-launch-profile -- --environment Development --urls http://localhost:5092
# In another terminal, from HealthTrack:
powershell -NoProfile -File Tests\Notifications.Smoke.ps1 -BaseUrl http://localhost:5092
```

The notification suite uses unique temporary accounts and SQL Server fixtures, exercises HTTP workflows and cleans up in finally. It covers 37 assertions, including recipients, rollback, duplicate transitions, plan item changes, read state, count, authentication, ownership and antiforgery. Existing TrainerManagement, HealthProgress, Phase3, Phase4, Assignments and DashboardPlans suites provide regression coverage.

## Files in this change

Created:

- `Models/Notification.cs`
- `Services/NotificationService.cs`
- `Controllers/NotificationsController.cs`
- `Views/Notifications/Index.cshtml`
- `Migrations/20260928145118_Notifications.cs`
- `Migrations/20260928145118_Notifications.Designer.cs`
- `Tests/Notifications.Smoke.ps1`
- `NOTIFICATIONS.md`

Modified:

- `Controllers/AppointmentsController.cs`
- `Controllers/TrainerWorkoutsController.cs`
- `Controllers/TrainerDietsController.cs`
- `Data/ApplicationDbContext.cs`
- `Migrations/ApplicationDbContextModelSnapshot.cs`
- `Program.cs`
- `ViewComponents/NavigationViewComponent.cs`
- `Views/Shared/Components/Navigation/Default.cshtml`
- `ARCHITECTURE.md`
- `NON-CSHARP-FILES.md`
