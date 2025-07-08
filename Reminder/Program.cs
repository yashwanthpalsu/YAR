using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Reminder.Models;
using Reminder.Models.DBEntities;
using Reminder.Services;
using Reminder.Middleware;
using Serilog;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with fallback
try
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();
}
catch (Exception ex)
{
    // Fallback to basic console logging if configuration fails
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console()
        .CreateLogger();
    
    Log.Warning(ex, "Failed to load Serilog configuration from appsettings. Using fallback configuration.");
}

builder.Host.UseSerilog();

try
{
    Log.Information("Starting Reminder application");

    // Add services to the container.
    builder.Services.AddControllersWithViews();

    // Configure Entity Framework
    var connectionString = builder.Configuration.GetConnectionString("PostgresqlConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        // Fallback to environment variables for production
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "reminder";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "root";
        
        connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};";
    }
    
    builder.Services.AddDbContext<SchedulerDbContext>(options => options.UseNpgsql(connectionString));

    //Configure Hangfire
    builder.Services.AddHangfireServer();
    builder.Services.AddHangfire(options => options.UsePostgreSqlStorage(connectionString));

    // Configure Identity
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 1;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.User.RequireUniqueEmail = true;

        // SignIn settings
        options.SignIn.RequireConfirmedEmail = false; // We'll handle this manually
        options.SignIn.RequireConfirmedPhoneNumber = false; // We'll handle this manually
    })
    .AddEntityFrameworkStores<SchedulerDbContext>()
    .AddDefaultTokenProviders();

    // Configure Cookie settings
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

    // Register services
    builder.Services.AddSingleton<ILoggingService>(provider => new LoggingService(Log.Logger));
    builder.Services.AddScoped<IReminderService, ReminderService>(provider =>
        new ReminderService(
            provider.GetRequiredService<SchedulerDbContext>(),
            provider.GetRequiredService<ILoggingService>(),
            provider.GetRequiredService<IEmailService>(),
            provider.GetRequiredService<ISmsService>(),
            provider.GetRequiredService<UserManager<ApplicationUser>>()
        )
    );
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<ISmsService, SmsService>();

    var app = builder.Build();

    // Apply database migrations
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        try
        {
            context.Database.Migrate();
            Log.Information("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying database migrations");
            throw;
        }
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    // Add request logging middleware
    app.UseRequestLogging();

    // Add authentication and authorization
    app.UseAuthentication();
    app.UseAuthorization();

    //Add Hangfire
    app.UseHangfireDashboard("/hangfire", new DashboardOptions {
        Authorization = new[] { new HangfireDashboardAuthFilter() },
        DashboardTitle = "Reminder Jobs Dashboard"
    });

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    Log.Information("Reminder application started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Custom Hangfire dashboard authorization: only allow authenticated users
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
