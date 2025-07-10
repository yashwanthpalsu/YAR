using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reminder.Models;
using Reminder.Models.DBEntities;
using Reminder.Services;
using System.Diagnostics;

namespace Reminder.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthService _authService;
        private readonly ILoggingService _loggingService;
        private readonly SchedulerDbContext _context;
        private readonly IAdminService _adminService;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            IAuthService authService,
            ILoggingService loggingService,
            SchedulerDbContext context,
            IAdminService adminService)
        {
            _userManager = userManager;
            _authService = authService;
            _loggingService = loggingService;
            _context = context;
            _adminService = adminService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Forbid();
                }

                // Get system statistics
                var totalUsers = await _userManager.Users.CountAsync();
                var activeUsers = await _userManager.Users.CountAsync(u => u.IsActive);
                var totalReminders = await _context.Reminders.CountAsync();
                var pendingReminders = await _context.Reminders
                    .Include(r => r.Schedules)
                    .CountAsync(r => r.Schedules.Any(s => !s.IsReminderSent));

                var systemStats = new
                {
                    TotalUsers = totalUsers,
                    ActiveUsers = activeUsers,
                    TotalReminders = totalReminders,
                    PendingReminders = pendingReminders
                };

                ViewBag.SystemStats = systemStats;
                return View();
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error loading admin dashboard");
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SystemStatus()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Forbid();
                }

                // Check database connectivity
                bool databaseConnected = false;
                try
                {
                    await _context.Database.CanConnectAsync();
                    databaseConnected = true;
                }
                catch
                {
                    databaseConnected = false;
                }

                // Check email configuration
                bool emailConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_USERNAME")) &&
                                     !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_PASSWORD"));

                var systemStatus = new
                {
                    DatabaseConnected = databaseConnected,
                    EmailConfigured = emailConfigured,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    AppUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "Not configured"
                };

                return View(systemStatus);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error loading system status");
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ManageUsers()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Forbid();
                }

                var users = await _userManager.Users
                    .Include(u => u.Reminders)
                    .OrderByDescending(u => u.CreatedAt)
                    .ToListAsync();

                // Create a view model with user and role information
                var userViewModels = new List<UserViewModel>();
                foreach (var user in users)
                {
                    var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                    userViewModels.Add(new UserViewModel
                    {
                        User = user,
                        IsAdmin = isAdmin
                    });
                }

                return View(userViewModels);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error loading users for admin");
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Forbid();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("ManageUsers");
                }

                // Prevent admin from deactivating themselves
                if (user.Id == currentUser.Id)
                {
                    TempData["ErrorMessage"] = "You cannot deactivate your own account.";
                    return RedirectToAction("ManageUsers");
                }

                user.IsActive = !user.IsActive;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    var action = user.IsActive ? "activated" : "deactivated";
                    TempData["SuccessMessage"] = $"User {user.FullName} has been {action} successfully.";
                    _loggingService.LogInformation("Admin {AdminId} {Action} user {UserId}", currentUser.Id, action, user.Id);
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update user status.";
                }

                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error toggling user status");
                TempData["ErrorMessage"] = "An error occurred while updating user status.";
                return RedirectToAction("ManageUsers");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAdminStatus(string userId)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Forbid();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("ManageUsers");
                }

                // Prevent admin from removing their own admin status
                if (user.Id == currentUser.Id)
                {
                    TempData["ErrorMessage"] = "You cannot remove your own admin privileges.";
                    return RedirectToAction("ManageUsers");
                }

                var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                
                if (isCurrentlyAdmin)
                {
                    // Remove admin role
                    var success = await _adminService.RemoveAdminRoleAsync(user.Email);
                    if (success)
                    {
                        TempData["SuccessMessage"] = $"Successfully removed admin privileges from {user.FullName}.";
                        _loggingService.LogInformation("Admin {AdminId} removed admin privileges from user {UserId}", currentUser.Id, user.Id);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to remove admin privileges.";
                    }
                }
                else
                {
                    // Add admin role
                    var success = await _adminService.AssignAdminRoleAsync(user.Email);
                    if (success)
                    {
                        TempData["SuccessMessage"] = $"Successfully granted admin privileges to {user.FullName}.";
                        _loggingService.LogInformation("Admin {AdminId} granted admin privileges to user {UserId}", currentUser.Id, user.Id);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to grant admin privileges.";
                    }
                }

                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error toggling admin status");
                TempData["ErrorMessage"] = "An error occurred while updating admin status.";
                return RedirectToAction("ManageUsers");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Forbid();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("ManageUsers");
                }

                // Prevent admin from deleting themselves
                if (user.Id == currentUser.Id)
                {
                    TempData["ErrorMessage"] = "You cannot delete your own account.";
                    return RedirectToAction("ManageUsers");
                }

                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = $"User {user.FullName} has been deleted successfully.";
                    _loggingService.LogInformation("Admin {AdminId} deleted user {UserId}", currentUser.Id, user.Id);
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete user.";
                }

                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error deleting user");
                TempData["ErrorMessage"] = "An error occurred while deleting the user.";
                return RedirectToAction("ManageUsers");
            }
        }
    }
} 