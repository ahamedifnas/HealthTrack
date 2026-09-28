# Reports module

All report selection, authorization, queries, calculations and table preparation run in C#. Entity Framework Core reads existing SQL Server tables. No migration, duplicate tables, JavaScript, chart library or PDF package is required. Razor renders prepared cards and tables; browser Print / Save as PDF uses CSS.

## Trainer testing

1. Sign in as Admin and assign an active Client to a Trainer. Add client health measurements and a target weight; use the existing Trainer and Client workflows to add workout/diet plans, exercises, meal items and logs. Create appointments in different statuses.
2. Sign in as that Trainer. Open **Reports** in navigation. Only active assigned Clients appear. Select a report:
   - **Client Progress**: compare current weight and BMI with the latest measurement date, target with the profile, and the first-to-latest weight change with history. Check completed/incomplete workout log counts. Measurement calories and workout calories are shown separately because combining them could double count the same activity.
   - **Workout**: verify plans (including inactive plans), exercises, logs, dates, completion status and calories. Completion date is the recorded WorkoutDate for a completed log; the existing schema has no separate completion timestamp. Incomplete logs show no completion date. Deleted/unspecified exercises retain their logs and show a placeholder.
   - **Diet**: verify plans (including inactive plans), meal items, nutrient values and meal logs.
   - **Appointments**: verify Pending, Approved, Completed and Cancelled counts and tables. Times use the configured appointment time zone.
3. Reports cover all recorded dates. Workout/diet records and appointments are additionally restricted to the signed-in Trainer's own records for that Client. Client measurement history is available to the currently assigned Trainer, matching the existing Progress feature.
4. Try the same report URL from a different Trainer session: expect 404. Change `clientId` to an unassigned or nonexistent user: expect 404. Remove the assignment and refresh: expect 404. Inactive Client reports are also denied. Client sessions receive 403. Anonymous users are sent to Login.
5. A Client with no recorded activity should show **Not recorded** metrics, zero counts and **No records available**, without errors.

## Admin testing

1. Sign in as Admin and choose **Reports**. It opens the system-wide aggregate report.
2. Compare Total Users, Total Clients, Total Trainers, Active Users, Total Appointments, Total Workout Plans and Total Diet Plans with the stored data. Inactive accounts/plans are included in totals; Active Users counts only active accounts. Client/Trainer totals count role membership, consistent with the dashboard; a multi-role account can count in both roles.
3. Compare all four appointment status counts with appointment records. A status with no records shows zero.
4. A Trainer or Client opening `/Reports/Admin` must receive 403. Admins, including those with a Trainer role, cannot open individual Client report actions; Admin reports contain aggregates only.

## Print testing

The Admin report is at `/Reports/Admin` (`/Reports` redirects there for Admins). Its actions are **Printable Report** and **Back to Dashboard**. Click Printable Report to open `/AdminReports/Print`, which requires the Admin role and freshly queries SQL Server through the same C# report service. All eleven totals are displayed in a standalone view with A4 portrait CSS and no navigation, footer or buttons. Use **Ctrl+P**, select **Save as PDF**, and save. The printable page does not invoke a print dialog automatically.

Trainer reports can still be printed directly using **Ctrl+P / Cmd+P** with their existing landscape layout. No JavaScript is used. Automated tests verify print stylesheets and report totals; visual pagination should be checked in the target browser's print preview.

## Automated verification

```powershell
# Repository root
dotnet build
# From HealthTrack, start the app
dotnet run --no-launch-profile -- --environment Development --urls http://localhost:5092
# In another terminal, from HealthTrack
powershell -NoProfile -File Tests\Reports.Smoke.ps1 -BaseUrl http://localhost:5092
```

The suite creates uniquely named temporary accounts and SQL Server fixtures, checks rendered HTTP reports and cleans up its records. Coverage includes role/ownership enforcement, assignment revocation, empty states, summaries, cross-trainer data exclusion, Admin counts, no-store caching, printable route totals, its Admin-only access and print CSS delivery.

## Admin printable report change

Created: `Controllers/AdminReportsController.cs`, `Views/AdminReports/Print.cshtml`, `wwwroot/css/admin-report-print.css`.

Modified: `Models/Reports/ReportViewModels.cs`, `Services/ReportService.cs`, `Views/Reports/Report.cshtml`, `Tests/Reports.Smoke.ps1`, `REPORTS.md`, `NON-CSHARP-FILES.md`.

## Files created

- `Controllers/ReportsController.cs`
- `Models/Reports/ReportViewModels.cs`
- `Services/ReportService.cs`
- `Views/Reports/Index.cshtml`
- `Views/Reports/Report.cshtml`
- `wwwroot/css/reports.css`
- `Tests/Reports.Smoke.ps1`
- `REPORTS.md`

## Files modified

- `Program.cs` — register the report service.
- `ViewComponents/NavigationViewComponent.cs` — Trainer/Admin Reports links.
- `ARCHITECTURE.md` — describe report logic and authorization.
- `NON-CSHARP-FILES.md` — add the new presentation, documentation and test files.
