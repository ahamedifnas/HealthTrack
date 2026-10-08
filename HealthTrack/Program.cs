using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<ViewPresentationFilter>();
        options.Filters.Add<ReminderFilter>();
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add(new ResponseCacheAttribute { NoStore = true, Location = ResponseCacheLocation.None });
    })
    .AddViewOptions(options => options.HtmlHelperOptions.ClientValidationEnabled = false);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is required.")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
// Refresh role claims and validate security stamps on every request, including existing cookies.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated == true)
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.GetUserAsync(context.Principal);
            if (user is null || !user.IsActive)
            {
                context.RejectPrincipal();
                await context.HttpContext.RequestServices
                    .GetRequiredService<SignInManager<ApplicationUser>>().SignOutAsync();
            }
        }
    };
});
builder.Services.AddScoped<PlanAccess>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RegistrationOtpService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ReminderService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DownloadReportService>();
builder.Services.AddSingleton<IPdfReportService, PdfReportService>();
PdfSharp.Fonts.GlobalFontSettings.FontResolver = new ReportFontResolver(builder.Configuration);
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<AdminDashboardData>();
builder.Services.AddSingleton<AppointmentClock>();
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(FeaturePolicies.Provider, policy => policy.RequireRole(AppRoles.Trainer, AppRoles.HealthcareProfessional)
        .RequireAssertion(context => !context.User.IsInRole(AppRoles.Admin)));
    options.AddPolicy(FeaturePolicies.Trainer, policy => policy.RequireRole(AppRoles.Trainer)
        .RequireAssertion(context => !context.User.IsInRole(AppRoles.Admin) && !context.User.IsInRole(AppRoles.HealthcareProfessional)));
    options.AddPolicy(FeaturePolicies.Client, policy => policy.RequireRole(AppRoles.Client)
        .RequireAssertion(context => !context.User.IsInRole(AppRoles.Admin) && !context.User.IsInRole(AppRoles.Trainer) && !context.User.IsInRole(AppRoles.HealthcareProfessional)));
});

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Assignment:Run"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DevelopmentAssignment.RunAsync(scope.ServiceProvider, app.Configuration, app.Environment);
    return;
}

// Configure the HTTP request pipeline.
// Never expose exception details to browser users, including in local Development.
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


// Migrations are applied explicitly with dotnet ef database update before startup.
await using (var scope = app.Services.CreateAsyncScope())
{
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Environment);
}

app.Run();
