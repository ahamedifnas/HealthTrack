# HealthTrack installation guide

This repository contains HealthTrack.slnx and the ASP.NET Core MVC project HealthTrack/HealthTrack.csproj. Run the following commands from the project directory unless a different directory is stated.

## Requirements

- Windows with Visual Studio and the ASP.NET and web development workload. Use a Visual Studio release that supports the installed .NET 10 SDK and .slnx solution format; this project targets net10.0. If the IDE cannot open it, update the IDE or use the .NET CLI.
- .NET 10 SDK. The verification machine has SDK 10.0.401.
- SQL Server Express LocalDB (MSSQLLocalDB), or SQL Server with a database account permitted to apply migrations.
- EF Core SQL Server, Identity EF stores, Design and dotnet-ef are pinned to 10.0.9. No scheduler package is required.

## Configure and install

1. Open HealthTrack.slnx in Visual Studio. Set HealthTrack as the startup project.
2. In a terminal, change to HealthTrack (the folder containing HealthTrack.csproj).
3. Review appsettings.json. The checked-in development connection is:

~~~json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=HealthTrackDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Appointments": { "TimeZoneId": "Asia/Colombo" }
}
~~~

The doubled backslash is JSON escaping. Windows authentication means this string contains no password. For a different SQL Server, override ConnectionStrings:DefaultConnection in user-secrets for local development or protected deployment configuration. TrustServerCertificate=True is for the local development database, not a claim of production certificate validation. Keep appointment wall-clock times in the configured time zone; CreatedAt timestamps use UTC.

4. Restore and build:

~~~powershell
dotnet restore
dotnet tool restore
dotnet build --no-restore
~~~

5. Back up an existing database before applying schema updates, then run:

~~~powershell
dotnet ef migrations list
dotnet ef database update
dotnet ef migrations has-pending-model-changes
~~~

Use the existing migrations; do not recreate the database or generate a new initial migration. The coursework migrations are 20261007133007_HealthcareProfessionalAppointments and 20261007133224_ReminderDeliveryLedger. The former preserves trainer appointment IDs, dates and data, makes TrainerId nullable, adds the healthcare profile/provider FK, and enforces exactly one provider. The latter adds the reminder delivery ledger. Rolling the provider migration back after healthcare bookings exist requires an explicit data-migration decision; do not drop consultation history.

## Secure administrator setup

Public registration creates only Client accounts. IdentitySeeder creates the Admin, Trainer, Client and HealthcareProfessional roles at startup. In Development only, it reads Admin:Email and Admin:Password directly from ASP.NET user-secrets. These values cannot be supplied through appsettings or command-line seeding parameters.

In Visual Studio use **Manage User Secrets**, then enter your own administrator email and strong password for these two keys. Do not put real credentials in this guide or source control. Alternatively, set each key with dotnet user-secrets set in a private local terminal; avoid committing terminal history. Identity requires at least eight characters with upper/lowercase, a digit and a non-alphanumeric character. The seeder creates or repairs the configured administrator and resets that account's password on Development startup while the two secret keys remain configured. Remove those keys after initial setup if repeated repair is not wanted. Never reuse an existing Client, Trainer or HealthcareProfessional email for administrator seeding.

Production startup seeds roles only; it does not provision an administrator password. Provision the initial production administrator through a controlled Identity administration process. No default password is supplied.

## Run

~~~powershell
dotnet dev-certs https --trust
dotnet run --launch-profile https
~~~

The HTTPS profile uses https://localhost:7041 and HTTP port 5075. Visual Studio can select the https launch profile and run with F5. Complete database migration before starting; the app does not migrate automatically. Sign in as the configured admin, create Trainers and Healthcare Professionals, register Clients, and assign clients to trainers from the admin dashboard.

If LocalDB cannot start, check that SQL Server Express LocalDB is installed for the current Windows user. Sandboxed tools cannot necessarily access the user's LocalDB instance. Do not change authentication or remove authorization to work around this.

## Verification

Run Tests/FinalVerification.ps1 against a running local app, passing -BaseUrl with its actual URL. The test scripts target the default HealthTrackDB LocalDB and create/remove uniquely named fixtures. Do not run them against production. See Testing.md for executed results and manual checks.
