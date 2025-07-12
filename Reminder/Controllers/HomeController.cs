using System.Diagnostics;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reminder.Models;
using Reminder.Models.DBEntities;
using Reminder.Services;

namespace Reminder.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IReminderService _reminderService;
    private readonly IAuthService _authService;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IReminderService reminderService, IAuthService authService, UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _configuration = configuration;
        _reminderService = reminderService;
        _authService = authService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var reminders = new List<ReminderViewModel>();
            
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    reminders = (await _reminderService.GetUserRemindersAsync(user.Id)).ToList();
                }
            }

            return View(reminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading home page");
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    [AllowAnonymous]
    [HttpGet("/health")]
    public async Task<IActionResult> Health()
    {
        var healthStatus = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Database = await TestDatabaseConnectionAsync(),
            Environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Unknown"
        };

        if (!healthStatus.Database)
        {
            return StatusCode(503, new { Status = "Unhealthy", healthStatus.Database, healthStatus.Environment });
        }

        return Ok(healthStatus);
    }

    private async Task<bool> TestDatabaseConnectionAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("PostgresqlConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                // Fallback to environment variables
                var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
                var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
                var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "reminder";
                var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
                var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "";
                
                connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};";
            }

            var optionsBuilder = new DbContextOptionsBuilder<SchedulerDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            using (var context = new SchedulerDbContext(optionsBuilder.Options))
            {
                await context.Database.OpenConnectionAsync();
                await context.Database.CloseConnectionAsync();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection test failed.");
            return false;
        }
    }


    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet]
    public IActionResult CreateReminder()
    {
        var model = new Reminder.Models.DBEntities.ReminderViewModel
        {
            Name = string.Empty,
            Message = string.Empty,
            Schedules = new List<Reminder.Models.DBEntities.ScheduleViewModel> { new Reminder.Models.DBEntities.ScheduleViewModel() }
        };
        return View("EditReminder", model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateReminder(ReminderViewModel model)
    {
        // Get current user
        var currentUser = await _authService.GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Set the UserId from the current user
        model.UserId = currentUser.Id;

        // Clear validation errors for Reminder navigation properties in schedules
        if (model.Schedules != null)
        {
            foreach (var schedule in model.Schedules)
            {
                // Clear the Reminder navigation property to avoid validation issues
                schedule.Reminder = null;
                
                // Clear any existing IDs to ensure they're generated by the database
                schedule.ScheduleId = 0;
                schedule.ReminderId = 0;
            }
        }

        // Clear the ReminderId to ensure it's generated by the database
        model.ReminderId = 0;

        if (!ModelState.IsValid)
        {
            // Log validation errors for debugging
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
            
            // Return view with validation errors
            return View("Index", model);
        }

        try
        {
            var success = await _reminderService.CreateReminderAsync(model);
            
            if (success)
            {
                TempData["SuccessMessage"] = "Reminder created successfully!";
                return RedirectToAction("Index");
            }
            else
            {
                _logger.LogError("Failed to create reminder for user {UserId}", model.UserId);
                ModelState.AddModelError("", "Failed to create reminder. Please try again.");
                return View("Index", model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reminder for user {UserId}", model.UserId);
            ModelState.AddModelError("", "An error occurred while creating the reminder. Please try again.");
            return View("Index", model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> ManageReminders()
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var reminders = await _reminderService.GetUserRemindersAsync(currentUser.Id);
            var activeReminders = reminders.Where(r => r.Schedules.Any(s => !s.IsReminderSent)).ToList();

            return View(activeReminders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reminders for user");
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditReminder(int id)
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var reminder = await _reminderService.GetReminderByIdAsync(id);
            if (reminder == null || reminder.UserId != currentUser.Id)
            {
                return NotFound();
            }

            return View(reminder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reminder for editing");
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditReminder(ReminderViewModel model)
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var success = await _reminderService.UpdateReminderAsync(model);
            if (success)
            {
                TempData["SuccessMessage"] = "Reminder updated successfully!";
                return RedirectToAction("ManageReminders");
            }
            else
            {
                ModelState.AddModelError("", "Failed to update reminder. Please try again.");
                return View(model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reminder");
            ModelState.AddModelError("", "An error occurred while updating the reminder. Please try again.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReminder(int id)
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var success = await _reminderService.DeleteReminderAsync(id, currentUser.Id);
            if (success)
            {
                TempData["SuccessMessage"] = "Reminder deleted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete reminder. Please try again.";
            }

            return RedirectToAction("ManageReminders");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reminder");
            TempData["ErrorMessage"] = "An error occurred while deleting the reminder. Please try again.";
            return RedirectToAction("ManageReminders");
        }
    }

}
