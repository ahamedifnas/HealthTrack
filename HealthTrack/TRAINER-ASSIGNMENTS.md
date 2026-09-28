# Trainer-client assignments

## Admin assignment

1. Sign in as Admin and open `/Dashboard/Admin`.
2. Click **Assign Client to Trainer** at the top, or scroll to **Trainer-Client Assignment**.
3. Select **Ahamed Ifnas (ahamedifnas77@gmail.com)** and **sadham (sadham@gmail.com)**, then click **Assign Client**.
4. Confirm the pair in **Current assignments**. Repeating the same assignment is safe; assigning the client to a different trainer is rejected.

The existing `TrainerClient` model and SQL Server `TrainerClients` table are reused. The composite primary key identifies the assignment, and the unique `ClientId` index permits only one trainer per client. Assignment rows represent current assignments; inactive client accounts are excluded from trainer access and counts. No duplicate assignment model or table is needed.

Inspection on 2026-09-28 confirmed both accounts are active, Ahamed Ifnas is already assigned to sadham, and `20260928081242_TrainerClientAssignedAt` is applied in local SQL Server `HealthTrackDB`.

## Trainer verification and plans

Run the latest build (stop and restart the app if it was already running). Sign in as sadham using the existing password and email `sadham@gmail.com`.

1. Open `/Dashboard/Trainer`. With the current single assignment, **Total Clients** is **1**, and **Recent Clients** contains Ahamed Ifnas.
2. Open **Assigned Clients** (`/Clients`) to see Ahamed Ifnas and the plan creation links.
3. Click **Create workout plan** on his row. Enter the plan name, description and dates, save, then use **Add exercise** on the plan details page.
4. Click **Create diet plan** on his row. Enter the plan name, description, dates and daily calories, save, then use **Add meal** on the details page.
5. Return to the Trainer Dashboard. Active plan counts come from saved plans; they can correctly be zero before plans are created. Deactivating a plan removes it from its active count.

The dashboard, client selector, progress, logs and trainer plan actions use assignment-scoped queries. Changing a client or plan ID cannot grant access to another trainer's data. The server also checks posted client IDs and uses the signed-in trainer's ID when creating plans.

## SQL verification

Run in SQL Server Management Studio against `(localdb)\MSSQLLocalDB`:

```sql
USE HealthTrackDB;
SELECT c.FullName AS Client, t.FullName AS Trainer, tc.AssignedAt,
       c.IsActive AS ClientActive, t.IsActive AS TrainerActive
FROM dbo.TrainerClients AS tc
JOIN dbo.AspNetUsers AS c ON c.Id = tc.ClientId
JOIN dbo.AspNetUsers AS t ON t.Id = tc.TrainerId
WHERE c.Email = 'ahamedifnas77@gmail.com';
```

## Automated verification

From the repository root: `dotnet build HealthTrack/HealthTrack.csproj`.

Against a running local app: `powershell -NoProfile -File HealthTrack/Tests/Assignments.Smoke.ps1 -BaseUrl http://localhost:5091` (change the port to match the app). This uses temporary accounts and plans in local HealthTrackDB and removes them afterward. It covers Admin assignment, duplicate prevention, persistence, trainer counts, plan creation, inactive clients, and unauthorized reads and writes.
