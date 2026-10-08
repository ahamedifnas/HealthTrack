# Classes, properties and methods

Paths below are relative to HealthTrack/. Domain entities generally have properties and navigation relationships; business operations belong to the controllers/services, not invented entity methods.

## Domain models

| Class | Purpose and important properties | Relationships / behavior |
|---|---|---|
| ApplicationUser | Identity account; FullName, TargetWeight, IsActive, CreatedAt; inherits Id, Email, PasswordHash, SecurityStamp, lockout fields | HealthMeasurements navigation; account principal for plans, logs, assignments, appointments and notifications. Password operations use UserManager, not direct PasswordHash assignment. |
| HealthcareProfessional | UserId, Specialty | UserId is PK/FK to ApplicationUser. User navigation supplies FullName, Email and IsActive. |
| TrainerClient | TrainerId, ClientId, AssignedAt | Composite key; unique ClientId enforces one current trainer per client. Trainer/Client are ApplicationUser navigations. |
| WorkoutPlan | Id, ClientId, TrainerId, PlanName, Description, StartDate, EndDate, Status, CreatedAt | Client/Trainer navigations and Exercises collection. One plan row belongs to one client. |
| WorkoutExercise | Id, WorkoutPlanId, ExerciseName, Sets, Repetitions, DurationMinutes, CaloriesTarget, Instructions | Belongs to WorkoutPlan. Deletion sets optional log reference to null. |
| WorkoutLog | Id, ClientId, WorkoutPlanId, WorkoutExerciseId, WorkoutDate, DurationMinutes, CaloriesBurned, Completed, Notes | References client, plan and optional exercise. |
| DietPlan | Id, ClientId, TrainerId, PlanName, Description, StartDate, EndDate, DailyCalories, Status, CreatedAt | Client/Trainer and Meals navigations. |
| DietMeal | Id, DietPlanId, MealType, FoodName, Quantity, Calories, Protein, Carbohydrates, Fat, Instructions | DietPlan navigation; MealType enum used by forms. |
| MealLog | Id, ClientId, DietPlanId, MealDate, MealType, FoodName, Quantity, Calories, Notes | Client and DietPlan navigations; no DietMeal FK. |
| Appointment | Id, ClientId, nullable TrainerId/HealthcareProfessionalId, AppointmentDate, AppointmentTime, Reason, Status, Notes, CreatedAt, RowVersion | Client and optional provider navigations. Unmapped ProviderId/ProviderName select the provider. AppointmentStatus = Pending 1, Approved 2, Completed 3, Cancelled 4. |
| HealthMeasurement | Id, ClientId, MeasurementDate, WeightKg, HeightCm, BMI, BodyFatPercentage, HeartRate, CaloriesBurned, Notes | Client navigation; unique client/date pair. BMI is calculated in MeasurementsController. |
| Notification | Id, UserId, Title, Message, Type, CreatedAt, IsRead | FK to account; NotificationType is Appointment, Workout or Diet. Used for events and reminders. |
| ReminderDelivery | UserId, ReminderKey, CreatedAt | Composite primary key and user FK; deduplication survives Notification deletion. |

## Controllers

All are under Controllers/. Refer to Security-and-Validation.md for the authorization boundaries.

| Controller | Important actual actions/methods | Dependencies and purpose |
|---|---|---|
| AccountController | Register, Login, Logout, AccessDenied | UserManager, SignInManager, ApplicationDbContext; Identity authentication and client-only registration. |
| DashboardController | Index, Client, Trainer, Admin | UserManager, database, AppointmentClock, PlanAccess, AdminDashboardData. Index dispatches by role; healthcare routes to Appointments. |
| AdminController | Clients, Users, Appointments, CreateClient, Details, Edit, ConfirmDelete, Delete; HasHistory, CanDisable | Database and UserManager. Protects self/last admin and preserves dependent domain history; healthcare profile counts as history. |
| AdminTrainersController | Create, Edit, SetActive | UserManager, RoleManager, database; trainer account management and deactivation. |
| AdminHealthcareProfessionalsController | Index, Create, Edit, AddErrors | Database and UserManager; list/profile management and transactional account/role/profile creation. |
| AdminAssignmentsController | Assign, Remove, Reassign | Database, AdminDashboardData; validates active eligible accounts and applies current relationship changes. |
| ClientsController | Index | PlanAccess; trainer's assigned clients. |
| TrainerWorkoutsController | Index, Details, Create, Edit, SetStatus, AddExercise, EditExercise, ExerciseDetails, DeleteExercise, Delete, Logs | Database, PlanAccess, NotificationService; assigned-client plan/exercise operations. |
| TrainerDietsController | Index, Details, Create, Edit, SetStatus, AddMeal, EditMeal, MealDetails, DeleteMeal, Delete, Logs | Database, PlanAccess, NotificationService; assigned-client diet operations. |
| WorkoutsController | Index, Details, Log, History, LogDetails, EditLog, DeleteLog | Database and AppointmentClock; logged-in client's eligible plans/logs. |
| DietsController | Index, Details, Log, History, LogDetails, EditLog, DeleteLog | Database and AppointmentClock; logged-in client's eligible diets/logs. |
| AppointmentsController | Index, Details, Book, Edit, EditRequest, Cancel, TrainerCancel, Approve, Complete, Delete; VisibleAppointments, ValidateProvider, ValidateFutureSchedule, SaveAppointment | Database, AppointmentClock, NotificationService. TrainerCancel is the retained route name used by either provider type. Provider/client IDs are rechecked before mutation. |
| MeasurementsController | Index, Details, Create, Edit, Delete, TargetWeight | Database, UserManager, AppointmentClock; validates/calculates own measurements. |
| ProgressController | Index, Summary | Database, PlanAccess, AppointmentClock; own/assigned history and admin summary. |
| NotificationsController | Index, Details, MarkRead, MarkUnread, Delete | Database; every lookup includes current UserId. |
| ReportsController | Index, Admin, Progress, Workout, Diet, Appointments, Analytics | ReportService and AnalyticsService; obtains trainer scope from signed-in claims. |
| AdminReportsController | Print | ReportService; standalone printable admin report. |
| HomeController | Index, Privacy, Status, Error | Public pages and safe status/error responses. |

## Services and infrastructure

| Class | Properties / methods | Dependencies and responsibility |
|---|---|---|
| ApplicationDbContext | Notifications, ReminderDeliveries, HealthcareProfessionals, HealthMeasurements, TrainerClients, WorkoutPlans/Exercises/Logs, DietPlans/Meals, MealLogs, Appointments; OnModelCreating | Inherits IdentityDbContext. Configures FKs, precision, unique indexes, provider check constraint, rowversion conventions and delete behavior. |
| IdentitySeeder | SeedAsync, EnsureSucceeded | RoleManager, UserManager, database and Development user-secrets; creates four roles and controlled development admin. |
| PlanAccess | Clients, Workouts, Diets | Database; reusable LINQ scopes for current active assigned clients and the trainer's plans. |
| AppointmentClock | Now, TimeZoneId | IConfiguration; converts UTC to configured wall-clock zone. |
| NotificationService | Appointment, Plan, RemovedPlan; private Add | Database and clock; stages event notifications in the same context as domain changes. |
| ReminderService | GenerateAsync; local AddAsync | IServiceScopeFactory, clock, logger; creates due reminders in isolated serializable transaction. |
| ReminderFilter | OnActionExecutionAsync | ReminderService; authorized GET trigger before selected controllers' actions. |
| AnalyticsService | BuildAsync | Database, PlanAccess, clock; returns ReportViewModel with scoped metrics/popularity/most-active tables. |
| ReportService | ClientsAsync, ClientAsync, AdminAsync | Database, PlanAccess, clock, AnalyticsService; retains detailed reports and extends admin aggregates. |
| AdminDashboardData | LoadAsync, ActiveClients, ActiveTrainers | Database and UserManager; constructs AdminDashboardViewModel. |
| ViewPresentationFilter | OnActionExecuting, OnActionExecuted | Clock; supplies Today and TimeZoneId to views. |
| SafeExceptionHandler | TryHandleAsync | ILogger; logs internal exception and renders a generic ErrorViewModel without database-backed navigation. |
| NavigationViewComponent | InvokeAsync | UserManager and database; role-specific links, name and own unread notification count. |

## ViewModels and form models

| Class/group | Important properties / methods | Use |
|---|---|---|
| RegisterViewModel / LoginViewModel | FullName/Email/Password/ConfirmPassword; Email/Password/RememberMe/ReturnUrl | Restrict account form input. Login ignores external return URLs. |
| CreateTrainerViewModel / EditTrainerViewModel | FullName, Email; password/confirmation for creation; Id, IsActive, TargetWeight for existing edit flows | Existing trainer/client/admin account forms. |
| CreateHealthcareProfessionalForm | Inherits CreateTrainerViewModel; Specialty | Admin-created healthcare account; cannot submit arbitrary role or ID. |
| EditHealthcareProfessionalForm | FullName, Email, Specialty, IsActive | Admin healthcare edit; route ID determines the existing profile. |
| AppointmentScheduleForm | Nullable AppointmentDate/AppointmentTime with Required annotations | Shared schedule binding and clean invalid-date validation. |
| BookAppointmentForm | TrainerId, HealthcareProfessionalId, Reason, RowVersion; Trainers/HealthcareProfessionals dropdown data; Validate | IValidatableObject enforces exactly one provider. Dropdowns use ValidateNever. |
| EditAppointmentForm | Inherits schedule; Notes, required RowVersion | Provider rescheduling with base64 SQL rowversion. |
| AppointmentDetailsViewModel / AppointmentListViewModel | Appointment(s), TimeZoneId, CanBook, CanManage, CanApprove, CanComplete, CanClientEdit, CanAdminDelete | UI permissions reflect server checks; hidden controls are not the authorization boundary. |
| PlanForm / DietPlanForm | ClientId, PlanName, Description, StartDate/EndDate, Status; DailyCalories; Validate | Required text, date ordering and eligible-client selection. |
| ExerciseForm / MealForm | Exercise sets/repetitions/duration/calories; meal type/food/quantity/nutrition | Ranges and string limits; PositiveQuantityAttribute validates quantity text. |
| WorkoutLogForm / MealLogForm | Plan ID, date and activity/nutrition fields; dropdown collections | Ownership is checked by controllers after binding. |
| AddMeasurementViewModel / TargetWeightViewModel | Date, positive WeightKg/HeightCm, optional health values, calories/notes; TargetWeight | Measurement and goal form validation. |
| ProgressViewModel / ProgressTrendRow / HealthSummaryViewModel | Measurements, Trends, target/latest values; counts and recent calorie total | Progress screen and admin aggregate summary. |
| ClientDashboardViewModel / TrainerDashboardViewModel / AdminDashboardViewModel | Role-specific metrics, plans, appointments and account/assignment data | Typed dashboard presentation. |
| ReportViewModel / ReportMetric / ReportTable / ReportClient | Title, Scope, GeneratedAt, Metrics, PrintMetrics, Tables; label/value; columns/rows; client ID/name/email | Existing report UI reused for analytics without exposing EF entities to the template. |
| ConfirmationViewModel / ErrorViewModel | Confirmation title/message/action/IDs; RequestId | Safe POST confirmation and generic error rendering. |

The generated EF migration classes implement Up and Down; ApplicationDbContextModelSnapshot records the final model. Their generated designer metadata is not a second domain model.
