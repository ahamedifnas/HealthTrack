# Phase 3: workout and diet management

## Implemented

- Trainers can view their assigned active clients, create/edit workout and diet plans, manage exercises and meal items, activate/deactivate plans, and review their clients' workout logs for plans they own.
- Clients can view their own plans and exercises, view meals grouped by type, log completed workouts and meals, and view both histories.
- Navigation links use the existing Bootstrap styling and dashboard cards. Existing dashboard summary placeholders remain previews; no analytics were added.
- All writes use POST and antiforgery validation. Form models whitelist editable fields; user IDs, trainer ownership, creation timestamps and workout completion are set by the server.
- Trainer policies explicitly exclude Admin, including users with both roles. Client features require Client without Trainer/Admin. Cross-owner record requests return 404; disallowed roles receive access denied.

## Development setup

From the HealthTrack project directory:

```powershell
dotnet tool restore
dotnet build
dotnet ef database update -- --environment Development
dotnet run --launch-profile https
```

Open `https://localhost:7041`. Register two separate accounts with passwords of your choosing, one intended as a Trainer and one as a Client. Public registration remains Client-only. Log out of both accounts before running the setup script.

```powershell
powershell -NoProfile -File Scripts\Set-DevelopmentAssignment.ps1 -TrainerEmail "trainer@example.com" -ClientEmail "client@example.com" -PromoteTrainer
```

Use the actual emails you registered. `-PromoteTrainer` converts the intended development Trainer account from Client to Trainer and refreshes its security stamp. It never changes passwords. Log in again afterward to obtain current role claims. For an existing Trainer, omit `-PromoteTrainer`.

The script only targets `(localdb)\MSSQLLocalDB` / `HealthTrackDB` using Windows authentication. It refuses Admin accounts, missing/inactive accounts, self-assignment and reassignment to a different trainer. It is idempotent for an existing matching assignment. Assignments are intentionally maintained through this development script, with no assignment management UI in Phase 3. Each client can have one assigned trainer; a trainer can have many clients.

## Trainer workflow

1. Log in as the assigned Trainer and open **Clients**. Only assigned active clients should appear.
2. Choose **Create workout plan** for the client. Set a name, description, and valid start/end dates including today. New plans are Active.
3. Open the plan and **Add exercise**. Enter sets, repetitions, duration, target calories and instructions. Try editing and deleting an exercise.
4. Open **Diet Plans**, create a plan for the same client and enter its daily calorie target. Add Breakfast, Lunch, Dinner and Snack items, with quantities, calories and macronutrients in grams. Edit/delete meal items as needed.
5. Use **Deactivate** / **Activate** on either plan. Inactive plans remain visible for reference but cannot receive new client logs.
6. After the client logs a workout, return to **Clients → Workout logs** (or the workout plan's **Client workout logs** link).

## Client workflow

1. Log in as the assigned Client, preferably in another browser/private window.
2. Open **My Workouts → View plan** to see exercises. Choose **Log Workout** and select an active plan; an exercise is optional. Changing the workout plan reloads its exercises, so select the plan before filling other fields.
3. Enter the date, duration, calories burned and notes. Saving records a completed workout and opens history.
4. Open **My Diet → View plan** to see items grouped by meal type. Choose **Log Meal**, select the plan, and record what was actually eaten. History preserves the food, quantity and calories entered even if the trainer later changes or deletes a planned item.
5. Both date fields must be within the selected plan's dates and no later than today. Multiple logs per day are allowed. New logs require an Active plan; Active status does not override the date checks.

## Ownership and history rules

- Clients never choose a ClientId when logging; it comes from sign-in. Plan access is filtered by that ID.
- Trainer plan access requires both the plan's TrainerId and a current assignment. Trainers cannot move existing plans between clients or manage another trainer's plans.
- Removing an assignment revokes the former trainer's access. Clients retain access to their own historical plans and logs.
- Multiple plans per client are supported. A client can choose which active plan to log against.
- Workout logs may reference a specific exercise. Deleting it sets the log's exercise reference to null and preserves the log, displayed as `Whole workout / removed exercise`.
- Plans themselves are not deleted through this UI; deactivate them to preserve history. User/plan foreign keys use restricted deletion. Macro values use SQL decimal precision (10,2).

## Validation and testing performed

- Baseline build passed with zero warnings/errors.
- Final `dotnet build --no-restore` passed with zero warnings/errors.
- Applied `20260927160418_WorkoutAndDietManagement` to HealthTrackDB.
- `dotnet ef migrations has-pending-model-changes --no-build` reported no pending model changes.
- All 47 HTTP/database integration checks passed. Tests cover the main flows, assignment isolation, cross-client and cross-trainer ID tampering, an Admin+Trainer account, invalid values, server-owned IDs, antiforgery, inactive plans and history preservation. All generated accounts and records were removed afterward.
- The tests exercise rendered HTTP forms and the database; visual browser layout was not automated.

To repeat, start the HTTP profile in one terminal, then run the test in another:

```powershell
dotnet run --launch-profile http
# In a second terminal:
powershell -NoProfile -File Tests\Phase3.Smoke.ps1
```

The test uses the default local development database and creates/removes uniquely named test records. For manual authorization checks, create another Trainer/Client pair and attempt to open the first pair's plan/edit URLs while signed into the second pair; expect 404. Admin must get access denied on `/TrainerWorkouts`, `/TrainerDiets` and `/Clients`.

## Files modified

```text
Data/ApplicationDbContext.cs
Migrations/ApplicationDbContextModelSnapshot.cs
Program.cs
Views/Dashboard/Client.cshtml
Views/Dashboard/Trainer.cshtml
Views/Shared/_Layout.cshtml
Views/_ViewImports.cshtml
```

## Files created

```text
Controllers/ClientsController.cs
Controllers/DietsController.cs
Controllers/TrainerDietsController.cs
Controllers/TrainerWorkoutsController.cs
Controllers/WorkoutsController.cs
Migrations/20260927160418_WorkoutAndDietManagement.cs
Migrations/20260927160418_WorkoutAndDietManagement.Designer.cs
Models/Training/DietMeal.cs
Models/Training/DietPlan.cs
Models/Training/Forms.cs
Models/Training/MealLog.cs
Models/Training/PlanStatus.cs
Models/Training/TrainerClient.cs
Models/Training/WorkoutExercise.cs
Models/Training/WorkoutLog.cs
Models/Training/WorkoutPlan.cs
Scripts/Set-DevelopmentAssignment.ps1
Services/PlanAccess.cs
Tests/Phase3.Smoke.ps1
Views/Clients/Index.cshtml
Views/Diets/Details.cshtml
Views/Diets/History.cshtml
Views/Diets/Index.cshtml
Views/Diets/Log.cshtml
Views/Shared/_DietMeals.cshtml
Views/Shared/_PlanFields.cshtml
Views/Shared/_WorkoutExercises.cshtml
Views/TrainerDiets/Details.cshtml
Views/TrainerDiets/Index.cshtml
Views/TrainerDiets/MealForm.cshtml
Views/TrainerDiets/PlanForm.cshtml
Views/TrainerWorkouts/Details.cshtml
Views/TrainerWorkouts/ExerciseForm.cshtml
Views/TrainerWorkouts/Index.cshtml
Views/TrainerWorkouts/PlanForm.cshtml
Views/Workouts/Details.cshtml
Views/Workouts/History.cshtml
Views/Workouts/Index.cshtml
Views/Workouts/Log.cshtml
PHASE3.md
```

Appointments, measurements, BMI, analytics, notifications and reports remain outside this phase.
