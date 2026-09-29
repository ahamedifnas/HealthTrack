# Final functional verification

Date: 2026-09-29. Tested the existing ASP.NET MVC application against SQL Server LocalDB (`HealthTrackDB`). No duplicate controllers, models, tables or business features were introduced.

## Bugs fixed

1. The Client calorie card previously showed `0 kcal` even when no measurements existed in its seven-day reporting window. It now shows `Not available yet`; a genuinely recorded zero still displays `0 kcal`.
2. Today's workout/diet previews previously selected the newest active plan regardless of its dates. They now require today's configured application date to fall within the plan's start/end dates. Historical/future plans remain accessible through the normal plan pages. Empty-state wording reflects availability today.
3. Navigation previously used forced horizontal wrapping without a menu toggle and permitted word-breaking in the brand and Logout. The Bootstrap-styled navbar now has a native HTML details/summary hamburger, responsive link grid, unbroken brand/links and a wrapping greeting. The menu is intentionally available at every width to accommodate the full Client link set. No browser JavaScript is required.
4. Missing IDs/records and rejected requests previously could produce empty HTTP error bodies. Status-code middleware now re-executes a read-only friendly Razor page, preserves HTTP status, and reveals no database or exception details. CSRF and ownership checks remain enforced on the original actions.

## Automated results

| Suite | Passed checks | Coverage |
| --- | ---: | --- |
| Phase1 | 16 | Registration, fixed Client role, hashing, logout, duplicates, invalid password, inactive accounts |
| TrainerManagement | 58 | Admin creation/edit/deactivation, role boundaries, real Admin totals |
| HealthProgress | 48 | Measurements, calculated BMI, target, history, progress, invalid input, ownership |
| Phase3 | 49 | Workout/diet plans, items, logs, status changes, validation and ownership |
| Phase4 | 63 | Booking, approval, rescheduling, cancellation, completion, concurrency and dashboard counts |
| Assignments | 41 | SQL persistence, uniqueness, eligibility, recent clients, counts and assignment revocation |
| DashboardPlans | 29 | Real previews, item limits, ownership, empty states, recorded zero, old measurements, future/expired plans |
| Notifications | 37 | Event creation, rollback, unread count, mark read, ownership |
| Reports | 92 | Trainer reports, Admin totals, printable route, ownership, print CSS |
| Security | 50 | Role refresh, session revocation, validation, anti-forgery, safe errors |
| DevelopmentAdmin | 13 | Actual user-secrets Admin login across two startups; other users unchanged |
| Database | 22 | Migration/table inventory, relationships, trusted foreign keys and uniqueness |
| Navigation browser | 119 | Headless Edge with JavaScript disabled: all four roles at 320/375/768/1280/1920px; keyboard open/close, brand/link geometry, Logout, friendly 400/404; PDF generation |
| ErrorHandling | 3 | Real service-resolution exceptions render safe 500 pages on three routes |

All listed suites passed; no unresolved test failures. The ten primary suite logs are under `bin/Functional-*.log` (ignored build artifacts). Browser screenshots and an actual generated Admin PDF are under `bin/BrowserChecks`. The Client mobile screenshot was visually inspected. Print PDF generation and print markup/CSS were verified; physical printing was not tested.

Initial, post-fix and final builds succeeded with zero warnings/errors. All 29 dashboard checks passed again after the final empty-state wording change. `git diff --check` passed. No model changes are pending since the last migration.

## Database and implementation checks

- All six repository migrations are applied, with no unknown migrations.
- Exactly 18 expected tables exist, including Identity and migration history; no duplicate feature tables.
- All 17 application foreign-key relationships were checked in SQL Server; all foreign keys are enabled/trusted. The unique Client assignment index exists. Deleting an exercise preserves workout history through the nullable reference.
- SQL Server remains the only provider. Application queries use C#/EF Core and ASP.NET Identity. SQL in PowerShell tests is isolated fixture/assertion/cleanup tooling.
- Calculations, authorization, input validation, state transitions, notifications and persistence remain C#. Razor, HTML, CSS and Bootstrap provide presentation. `Tests/Navigation.Browser.cjs` is browser test tooling only, never served to users; its browser context disables application JavaScript.
- Public registration and Client/Trainer/Admin login redirects were exercised. Existing suites check role-specific navigation and server-side authorization independently.
- Client health metrics use actual measurements. Zero user/appointment/notification counts are genuine empty-set counts, not fabricated health readings.

## Remaining text and limits

- No `Coming soon`, TODO, fake-value or development-only messaging was found in the current user-facing Razor pages.
- Two intentional input hints remain: `placeholder="e.g. 1 cup (150 g)"` in `Views/Diets/Log.cshtml` and `Views/TrainerDiets/MealForm.cshtml`. CSS `::placeholder` selectors style input hints; these are not fake data.
- `Services/DevelopmentAssignment.cs` retains development-only CLI guard/output messages. The command is not an HTTP endpoint. Developer setup documents, test files, comments and seeding diagnostics also retain development terminology intentionally.
- No known incomplete feature remains in the requested scope based on these checks. Production hosting/TLS, other browser engines, physical printing, load testing and exhaustive accessibility testing were not exercised. Browser geometry tests covered the dashboards and their navigation, not every screen at every width.

## Manual acceptance checklist

Use separate Client, Trainer and Admin sessions, plus a second Client/Trainer for isolation checks.

**Client**

- Register, log out and log in; confirm Client dashboard redirect. Try duplicate email and wrong password.
- With no data, check explanatory health/plan/appointment messages. Add weight/height and confirm BMI, target, history and progress; reject negative values.
- Review today's valid workout/diet, exercises and all meal types. Log a workout and meal; confirm history. Ensure future/expired plans remain in plan pages but do not replace today's preview.
- Book a future appointment, reject a past date/time, view and cancel an allowed pending appointment. Confirm the next approved appointment and notifications after trainer actions.
- Read a notification; confirm its unread count falls. Substitute another client's IDs and confirm access is denied.

**Trainer**

- Log in and confirm Trainer redirect. Verify counts and Recent Clients against active assignments.
- Create/edit plans for an assigned Client; add/edit/delete exercises and meals; activate/deactivate plans. Check Client visibility and notifications.
- Approve, reschedule and cancel owned appointments; complete an approved appointment after its scheduled time. Check today/pending counts.
- Open Progress, Workout, Diet and Appointment reports. Try another trainer's client/plan/appointment IDs; confirm denial.

**Admin**

- Log in and confirm Admin redirect and real user/client/trainer/active-user/appointment totals.
- Create/edit a Trainer. Deactivate while that Trainer has an open session and verify login/access is blocked; reactivate and sign in again.
- Assign an active Client to an active Trainer. Verify persistence and Trainer counts; repeat and attempt another assignment to confirm no duplicates.
- Open Client list, appointments, aggregate Health Summary and reports. Open Printable Report and browser print preview; confirm readable A4 layout and no navigation controls.

**All roles**

- At narrow and desktop widths, open/close the hamburger by keyboard and pointer. Keep the brand visible and Logout on one line. Verify relevant role links only.
- Try missing/invalid IDs, rejected forms and unauthorized routes. Confirm friendly messages, appropriate status codes and no internal details.

## Files changed

- `Controllers/DashboardController.cs`: date-scoped previews and nullable recent calorie total.
- `Models/Dashboard/ClientDashboardViewModel.cs`: explicit missing-calorie display.
- `Views/Dashboard/Client.cshtml`: accurate today-specific empty messages.
- `Program.cs`: friendly status-code re-execution middleware.
- `Controllers/HomeController.cs`: read-only status renderer.
- `Views/Home/Status.cshtml`: standalone friendly error page.
- `Views/Shared/_Layout.cshtml`: semantic main navigation; remove ineffective expansion class.
- `Views/Shared/Components/Navigation/Default.cshtml`: native hamburger and responsive menu structure.
- `Views/Shared/_Layout.cshtml.css`: prevent brand breaking/shrinking.
- `wwwroot/css/site.css`: responsive menu, keyboard focus, link/greeting layout.
- `Tests/DashboardPlans.Smoke.ps1`: empty/zero/old calorie data, date-scoped plans and navigation regressions.
- `Tests/Database.Smoke.ps1`: read-only schema checks.
- `Tests/Navigation.Browser.ps1`: browser launcher and cleanup of temporary users.
- `Tests/Navigation.Browser.cjs`: headless UI/PDF regression tests, outside application runtime.
- `NON-CSHARP-FILES.md`: update presentation/test-tool inventory.
- `SECURITY-REVIEW.md`: document both read-only anti-forgery-exempt error renderers.
- `FUNCTIONAL-REVIEW.md`: findings, evidence, limits and manual checklist.
