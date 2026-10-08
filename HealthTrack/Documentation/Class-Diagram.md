# Important classes

~~~mermaid
classDiagram
    IdentityUser <|-- ApplicationUser
    ApplicationUser : string FullName
    ApplicationUser : bool IsActive
    ApplicationUser : decimal? TargetWeight
    ApplicationUser : DateTime CreatedAt
    HealthcareProfessional : string UserId
    HealthcareProfessional : string Specialty
    ApplicationUser "1" -- "0..1" HealthcareProfessional : User
    TrainerClient : string TrainerId
    TrainerClient : string ClientId
    TrainerClient : DateTime AssignedAt
    ApplicationUser "1" -- "0..*" TrainerClient : trainer/client
    WorkoutPlan : int Id
    WorkoutPlan : string ClientId
    WorkoutPlan : string TrainerId
    WorkoutPlan : string PlanName
    WorkoutPlan : PlanStatus Status
    WorkoutPlan : DateOnly StartDate
    WorkoutPlan : DateOnly EndDate
    WorkoutExercise : string ExerciseName
    WorkoutExercise : int Sets
    WorkoutExercise : int Repetitions
    WorkoutLog : DateOnly WorkoutDate
    WorkoutLog : bool Completed
    WorkoutLog : int CaloriesBurned
    WorkoutPlan "1" -- "0..*" WorkoutExercise
    WorkoutPlan "1" -- "0..*" WorkoutLog
    WorkoutExercise "0..1" -- "0..*" WorkoutLog
    DietPlan : string PlanName
    DietPlan : int DailyCalories
    DietPlan : PlanStatus Status
    DietMeal : string FoodName
    DietMeal : string Quantity
    DietMeal : int Calories
    MealLog : DateOnly MealDate
    MealLog : int Calories
    DietPlan "1" -- "0..*" DietMeal
    DietPlan "1" -- "0..*" MealLog
    Appointment : int Id
    Appointment : string? TrainerId
    Appointment : string? HealthcareProfessionalId
    Appointment : DateOnly AppointmentDate
    Appointment : TimeOnly AppointmentTime
    Appointment : AppointmentStatus Status
    Appointment : byte[] RowVersion
    Appointment : string ProviderName
    HealthcareProfessional "0..1" -- "0..*" Appointment
    ApplicationUser "1" -- "0..*" Appointment : client / optional trainer
    HealthMeasurement : decimal WeightKg
    HealthMeasurement : decimal HeightCm
    HealthMeasurement : decimal BMI
    ApplicationUser "1" -- "0..*" HealthMeasurement
    Notification : string UserId
    Notification : string Title
    Notification : bool IsRead
    ReminderDelivery : string UserId
    ReminderDelivery : string ReminderKey
    ReminderService : GenerateAsync(userId, client)
    ReminderFilter : OnActionExecutionAsync(context, next)
    ReminderFilter ..> ReminderService
    ReminderService ..> Notification
    ReminderService ..> ReminderDelivery
    ReminderService ..> AppointmentClock
    PlanAccess : Clients(trainerId)
    PlanAccess : Workouts(trainerId)
    PlanAccess : Diets(trainerId)
    AnalyticsService : BuildAsync(trainerId)
    ReportService : ClientAsync(trainerId, clientId, kind)
    ReportService : AdminAsync()
    AnalyticsService ..> PlanAccess
    ReportService ..> AnalyticsService
    ApplicationDbContext : OnModelCreating(builder)
    ReminderService ..> ApplicationDbContext
    PlanAccess ..> ApplicationDbContext
    AnalyticsService ..> ApplicationDbContext
~~~

Domain entities are persistence classes; most business methods live in controllers/services. Appointment.ProviderId and ProviderName are computed, unmapped helpers. HealthcareProfessional name/email/active state are obtained through User, rather than duplicated profile fields. See Classes-Properties-Methods.md for action and ViewModel coverage.
