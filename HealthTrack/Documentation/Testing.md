# Testing record

Tests target the actual MVC application and SQL Server LocalDB, not mocked controllers. The scripts create uniquely named temporary accounts/records and remove them in finally blocks. Test SQL is used for setup, assertions and simulated passage of time on fixture records only. The source and logs are in Tests/ and Documentation/Test-Results/.

## Reproduce

Start the app, then from HealthTrack/ run:

~~~powershell
powershell -NoProfile -File Tests/FinalVerification.ps1 -BaseUrl http://localhost:5092
~~~

Use the correct URL for your instance. These scripts connect to the default local HealthTrackDB and must not target production. FinalVerification supports a comma-separated -Suites value for targeted reruns and saves per-suite output/results.json. A failed suite is not counted as passed simply because some of its assertions succeeded.

## Verification status

The final pass executed on 2026-10-08 against http://localhost:5092 with the final compiled application and existing HealthTrackDB LocalDB. **13 suites passed, 599 assertions passed, zero failing suites.** The new Coursework suite passed 58 assertions. All fixture scripts completed their cleanup. The host used Production environment over a dedicated local HTTP test URL; this does not verify production HTTPS deployment.

| Suite | Passed assertions | Exit code | Evidence |
|---|---:|---:|---|
| Database | 22 | 0 | [Log](Test-Results/Database.txt) |
| Phase1 | 16 | 0 | [Log](Test-Results/Phase1.txt) |
| TrainerManagement | 58 | 0 | [Log](Test-Results/TrainerManagement.txt) |
| HealthProgress | 48 | 0 | [Log](Test-Results/HealthProgress.txt) |
| Phase3 | 49 | 0 | [Log](Test-Results/Phase3.txt) |
| Phase4 | 63 | 0 | [Log](Test-Results/Phase4.txt) |
| Assignments | 41 | 0 | [Log](Test-Results/Assignments.txt) |
| DashboardPlans | 33 | 0 | [Log](Test-Results/DashboardPlans.txt) |
| DashboardUi | 32 | 0 | [Log](Test-Results/DashboardUi.txt) |
| Notifications | 37 | 0 | [Log](Test-Results/Notifications.txt) |
| Reports | 92 | 0 | [Log](Test-Results/Reports.txt) |
| Security | 50 | 0 | [Log](Test-Results/Security.txt) |
| Coursework | 58 | 0 | [Log](Test-Results/Coursework.txt) |
 Builds were performed after healthcare support, reminders, analytics and the final security adjustments. The final restore/build and pending-model check completed successfully. A build attempt while the test host held the executable open failed with a file-lock error; stopping that host and rebuilding resolved it with zero warnings/errors. This was not a source compilation defect.

One initial DashboardUi assertion assumed exactly one notification before reminders existed. It was updated to compare the persisted unread count and verify isolation. The Reports assertion was narrowed to prohibit private food/appointment details while allowing the newly required aggregate plan names. Both corrected suites were executed, not merely relabelled.

## Coursework test cases

| Test ID | Feature | Scenario | Steps | Expected Result | Actual Result | Status |
|---|---|---|---|---|---|---|
| T01 | Login | Valid login, invalid password and deactivated account | Run Phase1 and Security login cases | Valid account signs in; invalid/inactive account is denied | Executed in Phase1; Security; see matching PASS assertions in saved logs | Passed (automated) |
| T02 | Registration | Client registration and validation | Run Phase1 registration cases | Client role only; validated account persists; duplicate/invalid forms rejected | Executed in Phase1; see matching PASS assertions in saved logs | Passed (automated) |
| T03 | Roles | Existing cookie reflects role revocation | Run Security role/stamp cases | Revoked role and changed security stamp take effect on next request | Executed in Security; see matching PASS assertions in saved logs | Passed (automated) |
| T04 | Client access | Another client ID in measurement/progress/log request | Run HealthProgress and Phase3 ownership cases | Other client data is unavailable | Executed in HealthProgress; Phase3; see matching PASS assertions in saved logs | Passed (automated) |
| T05 | Trainer access | Assigned client versus other client | Run Assignments and Reports cases | Only assigned-client plans/progress/reports can be accessed | Executed in Assignments; Reports; see matching PASS assertions in saved logs | Passed (automated) |
| T06 | Healthcare account | Admin creates specialty profile and login | Run Coursework account creation cases | Identity account, role and specialty persist; login opens appointments | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T07 | Healthcare access | Read or approve another provider appointment | Run Coursework ownership cases | 404 for foreign appointment; forbidden client/trainer/admin screens | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T08 | Workout management | Create/edit plan and exercises; log workout | Run Phase3 plan/exercise/log cases | Authorized changes persist; foreign IDs and invalid inputs rejected | Executed in Phase3; see matching PASS assertions in saved logs | Passed (automated) |
| T09 | Diet management | Create/edit diet and meals; log food | Run Phase3 diet/meal cases | Authorized changes persist; foreign IDs and invalid inputs rejected | Executed in Phase3; see matching PASS assertions in saved logs | Passed (automated) |
| T10 | Health measurements | Valid BMI, zero/negative/out-of-range inputs | Run HealthProgress validation cases | Correct BMI and history; invalid values and duplicate date rejected | Executed in HealthProgress; see matching PASS assertions in saved logs | Passed (automated) |
| T11 | Trainer booking | Book, approve, reschedule, cancel and complete | Run Phase4 lifecycle cases | Existing trainer workflow and status rules remain intact | Executed in Phase4; see matching PASS assertions in saved logs | Passed (automated) |
| T12 | Healthcare booking | Book, approve, reschedule, cancel and complete | Run Coursework consultation lifecycle | Same lifecycle succeeds for selected healthcare provider | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T13 | Provider validation | Both, neither, foreign ID, inactive healthcare provider | Run Coursework invalid-provider cases | Clean error; no invalid appointment is persisted | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T14 | Provider DB invariant | Direct fixture updates with invalid provider combination | Run Coursework check-constraint cases | SQL check constraint rejects both and neither | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T15 | Appointment conflicts | Same provider slot and same client across providers | Run Phase4 and Coursework conflict cases | Pending/Approved slot conflict rejected | Executed in Phase4; Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T16 | Schedule validation | Past booking and stale edit token | Run Phase4 and Coursework schedule cases | Past schedule rejected; stale rowversion cannot overwrite | Executed in Phase4; Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T17 | Appointment reminders | Tomorrow and today; both participants | Run Coursework reminder date simulations | One eligible reminder per participant and phase | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T18 | Reminder terminal status | Completed/cancelled appointments due today | Run Coursework terminal-status reminder case | No new delivery record or reminder created | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T19 | Reminder deduplication | Repeated visits and deleting a reminder | Run Coursework repeat/delete cases | Same delivery key is never recreated | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T20 | Workout reminder | Client has date-active workout plan | Run Coursework active-plan case | Exactly one workout reminder per local day | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T21 | Meal reminder | Client has date-active diet plan | Run Coursework active-plan case | Exactly one meal reminder per local day | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T22 | Existing notifications | Events, read/unread and inbox ownership | Run Notifications suite | Events persist with domain changes; inbox and updates remain private | Executed in Notifications; see matching PASS assertions in saved logs | Passed (automated) |
| T23 | Reports | Retained client reports and admin print totals | Run Reports suite | Existing reports and print totals match scoped SQL data | Executed in Reports; see matching PASS assertions in saved logs | Passed (automated) |
| T24 | Popular plans | Workout/diet names with assigned-client and log counts | Run Coursework analytics cases and Reports regression | Trainer sees own scoped plans; admin sees system plans | Executed in Coursework; Reports; see matching PASS assertions in saved logs | Passed (automated) |
| T25 | Engagement | Monthly logs and most-active clients | Run Coursework fixture log cases | Monthly counts equal inserted/logged activity and active client appears | Executed in Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T26 | Revocation | Remove assignment then refresh reports | Run Assignments, Reports and Coursework revocation cases | Former client data disappears from trainer reports and analytics | Executed in Assignments; Reports; Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T27 | Antiforgery | POST without token | Run Security and Coursework negative POST cases | 400 and no unauthorized mutation | Executed in Security; Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T28 | Account activation | Deactivate provider with existing cookie | Run TrainerManagement and Coursework deactivation cases | Session invalidated; provider removed from booking options | Executed in TrainerManagement; Coursework; see matching PASS assertions in saved logs | Passed (automated) |
| T29 | Dashboard routes | Client/trainer/admin dashboards and plans | Run DashboardUi and DashboardPlans | Role-specific pages render correct scoped data and counts | Executed in DashboardUi; DashboardPlans; see matching PASS assertions in saved logs | Passed (automated) |
| T30 | Database schema | Migrations, tables, FKs and unique indexes | Run Database suite | All expected migrations applied and relationships enabled/trusted | Executed in Database; see matching PASS assertions in saved logs | Passed (automated) |
| T31 | Mixed roles | Healthcare account also has Client membership | Run Coursework mixed-role cases | Healthcare account cannot access measurements/progress | Executed; both routes returned 403 | Passed (automated) |
| T32 | Admin protection | Healthcare profile account also has Admin role | Attempt self-deactivation in healthcare Edit | Own admin account remains active with validation error | Executed; account remained active | Passed (automated) |
| T33 | Reminder concurrency | Four simultaneous dashboard requests | Run Coursework concurrent HttpClient requests | All requests succeed; one reminder per client/type | Executed; four successful responses and once-only counts | Passed (automated) |
| T34 | Plan reminder dates | Inactive and future plans | Visit Notifications with each fixture state | Neither state generates a meal reminder | Executed; reminder counts stayed zero | Passed (automated) |
| T35 | Healthcare request edit | Client edits future Pending healthcare booking | Submit EditRequest with rowversion | Schedule changes; selected provider retained | Executed; persisted new time and same provider | Passed (automated) |

## Additional manual checks

These checks have not been executed and must not be described as passed.

| Test ID | Feature | Scenario | Steps | Expected Result | Actual Result | Status |
|---|---|---|---|---|---|---|
| M01 | HTTPS | Trusted local HTTPS and deployment redirect | Run https profile; browse HTTP and HTTPS; inspect certificate/redirect and deployment HSTS | Trusted HTTPS; correct redirect; no sensitive page served through misconfigured proxy | To be manually verified | Not executed |
| M02 | UI | Healthcare forms at phone and desktop widths | Create/edit provider; open booking and provider management at narrow/wide widths | Controls/messages readable; keyboard navigation usable | To be manually verified | Not executed |
| M03 | Printing | Admin analytics print preview | Open Printable Report; use browser print preview and save PDF | Tables fit pages with readable headings | To be manually verified | Not executed |
| M04 | Screenshots | Coursework evidence | Follow User-Manual and replace screenshot placeholders with captures of your demo accounts | Screenshots match final application and contain no real credentials | To be manually verified | Not executed |
| M05 | Reminder rollover | Real local midnight across two days | Visit with date-active plans on consecutive local dates | One new workout/meal reminder per type on each day | To be manually verified; automated tests check same-day deduplication and simulated appointment dates | Not executed |
| M06 | Installation | Clean machine setup and initial admin secrets | Follow Installation-Guide on a fresh supported Windows environment | Restore, database migration and secure first admin login succeed | To be manually verified; existing local installation was tested | Not executed |
| M07 | Accessibility | Keyboard/screen reader and actual visual layout | Navigate four roles without mouse; review labels/focus/validation messages | Usable navigation and forms with clear feedback | To be manually verified | Not executed |

The development-admin repair test was not run because it intentionally resets the configured real development administrator. Browser screenshot/accessibility tests and the intentionally misconfigured-server ErrorHandling suite were not executed in this pass. SafeExceptionHandler and HTTPS/HSTS registration were inspected in source. No production deployment, penetration testing or database-encryption verification is claimed.
