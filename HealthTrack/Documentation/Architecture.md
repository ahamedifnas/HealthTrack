# Architecture

~~~mermaid
flowchart TD
    Browser[Browser over HTTPS] --> Middleware[Exception handling, HTTPS, routing, Identity cookie authentication, authorization]
    Middleware --> Filters[Global anti-forgery, no-store, ViewPresentationFilter, ReminderFilter]
    Filters --> Controllers[MVC Controllers]
    Controllers --> Views[Razor Views and typed ViewModels]
    Views --> Browser
    Controllers --> Identity[UserManager and SignInManager]
    Controllers --> Services[PlanAccess, ReportService, AnalyticsService, NotificationService, AdminDashboardData]
    Filters --> Reminders[ReminderService]
    Services --> Clock[AppointmentClock]
    Reminders --> Clock
    Controllers --> Db[ApplicationDbContext]
    Identity --> Db
    Services --> Db
    Reminders --> Isolated[Isolated scoped ApplicationDbContext and transaction]
    Isolated --> SQL[(SQL Server / LocalDB)]
    Db --> SQL
    Startup[Program and IdentitySeeder] --> Identity
    Migration[Explicit dotnet ef database update] --> SQL
~~~

Controllers contain workflow/ownership checks and some direct LINQ queries. Services centralize shared access, reports, notifications and time handling. ApplicationDbContext inherits IdentityDbContext<ApplicationUser>; it contains both Identity and domain tables. This is an MVC application with scoped services, not a separate API/frontend architecture or a repository abstraction. The NavigationViewComponent loads role-specific navigation and the current user's unread count. No external scheduler, email gateway or SMS provider is configured.
