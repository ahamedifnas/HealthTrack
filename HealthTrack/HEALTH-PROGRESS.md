# Health Measurements and Progress Analytics

## Implemented behavior

- Clients add one measurement per calendar date and view their own history at `/Measurements`.
- Stored fields: Id, ClientId, MeasurementDate, WeightKg, HeightCm, BMI, optional BodyFatPercentage and HeartRate, CaloriesBurned, and optional Notes.
- Weight: 1-500 kg. Height: 30-300 cm. Body fat: 0-100%. Heart rate: 1-300 bpm. Calories: 0-50,000 kcal. Notes: at most 2,000 characters. Dates must be between 1900-01-01 and today.
- Weight and height are rounded to two decimal places. The server calculates BMI as `WeightKg / (HeightCm / 100)^2`, rounded to two decimals. Submitted BMI and client IDs are never trusted.
- Optional target weight is stored on ApplicationUser. Set it at `/Measurements/TargetWeight`; leave it blank to clear it. The same 1-500 kg range applies when supplied.
- The Client dashboard shows the measurement with the latest measurement date (not insertion order), its BMI, target weight, and calories recorded for today plus the preceding six calendar days. Date boundaries use the existing `Appointments:TimeZoneId` configuration, currently Asia/Colombo. Calories are measurement entries, not a second sum of workout logs.
- `/Progress` shows weight, BMI, and calorie trends, the latest measurement, target weight, and an accessible history table. Tables show recorded dates only; missing days are not imputed as zero.
- Progress is rendered entirely by Razor as a table. C# computes weight and BMI changes between consecutive measurements. No JavaScript chart dependency remains.
- Trainers open **Clients > Progress**. Access requires a current assignment to an active Client. Removing that assignment immediately removes access.
- Admin opens **Health Summary** at `/Progress/Summary`: total measurements, clients with measurements, clients with targets, and the seven-day recorded calorie total. Individual measurement history and progress are denied to Admin.
- No notifications or final reports were added.

## Database

Migration `20260928073603_HealthMeasurements` adds nullable `AspNetUsers.TargetWeight` and the `HealthMeasurements` table. The client foreign key uses restricted deletion; the unique `(ClientId, MeasurementDate)` index prevents concurrent duplicate daily records. Decimal precision is explicitly configured.

The migration has been applied to the local HealthTrackDB. On another installation, from the project directory:

```powershell
dotnet tool restore
dotnet ef database update -- --environment Development
dotnet run --launch-profile https
```

## Manual full-flow test

1. Start the app and sign in as a Client at `https://localhost:7041/Account/Login`. On a new account, the dashboard shows no weight/BMI, zero recorded calories, and no target.
2. Choose **Set target weight**, enter `70`, and save. Confirm the dashboard shows `70 kg`.
3. Choose **+ Add Measurement**. Enter today's date, weight `80`, height `200`, and calories `300`; leave the optional fields blank. Save. Confirm history shows BMI `20.00` and the dashboard shows `80 kg`, `20.00`, and `300 kcal`.
4. Add yesterday's measurement with weight `82`, height `200`, calories `200`, and optional notes/body fat/heart rate. Confirm current weight remains `80 kg` and recent calories become `500 kcal`. Add a record dated seven days ago; it appears in history and trend tables but does not increase the recent total.
5. Open **Progress**. Confirm all three tables, the latest measurement summary, and history. Resize the browser to a narrow/mobile width; tables should remain inside their cards and the history table should scroll horizontally.
6. Try zero/negative weight or height, invalid optional values, a future date, and a second measurement on an existing date. Each must show validation errors without inserting a record.
7. Use a second Client account. Confirm it cannot see the first Client's history. Request `/Progress?clientId=FIRST_CLIENT_ID`; expect 404. Supplying another client ID in the history URL must still show only the signed-in Client's records.
8. Assign the first Client to an existing Trainer using the existing development helper (substitute actual emails):

   ```powershell
   powershell -NoProfile -File Scripts\Set-DevelopmentAssignment.ps1 -TrainerEmail "trainer@example.com" -ClientEmail "client@example.com"
   ```

   Sign in as that Trainer, open **Clients**, and select **Progress**. The assigned Client's tables should be visible; `/Progress?clientId=UNASSIGNED_CLIENT_ID` must return 404. Trainer cannot add measurements or change target weight.
9. Sign in as Admin and open **Health Summary**. Confirm only aggregate counts/totals appear. `/Progress?clientId=FIRST_CLIENT_ID` and `/Measurements` must return access denied.

## Automated checks

From the project directory, with the HTTP profile running on port 5075:

```powershell
powershell -NoProfile -File Tests\HealthProgress.Smoke.ps1 -BaseUrl http://localhost:5075
```

The test uses LocalDB, creates uniquely named temporary Client/Trainer/Admin accounts and an assignment, and removes all its users, measurements, and assignments in a finally block. It verifies 47 checks covering validation, BMI, forged ownership/BMI input, date ordering, the seven-day boundary, target ownership, Razor trend rows and C# changes, assignment revocation, Admin restrictions, and antiforgery.

Verification completed:

- `dotnet build HealthTrack/HealthTrack.csproj --no-restore -o HealthTrack/bin/HealthProgressCheck`: zero warnings and errors (run from repository root; separate output avoids the existing running app's file lock).
- `node --check HealthTrack/wwwroot/js/progress.js`: passed.
- Migration applied successfully to HealthTrackDB.
- All 48 HTTP/database integration checks passed, including server-rendered progress with no JavaScript.
