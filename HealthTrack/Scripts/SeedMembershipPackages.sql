IF NOT EXISTS (SELECT 1 FROM MembershipPackages)
BEGIN
    INSERT INTO MembershipPackages (Name, Description, PriceLKR, DurationDays, BenefitsJson, IsActive, CreatedAt)
    VALUES 
    (N'Basic Starter', 
     N'Essential health and fitness tracking for beginners. Includes standard access and measurement logs.', 
     3500.00, 
     30, 
     N'["Basic health measurements & BMI tracking","Access to view assigned workout & diet plans","Personal workout & meal activity logging","In-app notification reminders"]', 
     1, 
     SYSUTCDATETIME()),

    (N'Pro Fitness', 
     N'Our most popular plan. Direct trainer guidance, tailored workout & diet regimens, and consultation booking.', 
     8500.00, 
     90, 
     N'["All Basic Starter features","Dedicated Trainer assignment","Custom Workout & Diet plan management","Appointment booking with certified trainers","Progress analytics and health reports","Downloadable PDF progress summaries"]', 
     1, 
     SYSUTCDATETIME()),

    (N'Elite Health', 
     N'Comprehensive annual premium health management with full access to trainers and healthcare professionals.', 
     28000.00, 
     365, 
     N'["All Pro Fitness features","Priority appointment scheduling","Consultations with Healthcare Professionals","Advanced clinical health metrics & analytics","Unlimited PDF report exports","Dedicated priority customer support"]', 
     1, 
     SYSUTCDATETIME());
END;

SELECT Id, Name, PriceLKR, DurationDays, IsActive FROM MembershipPackages;
GO
