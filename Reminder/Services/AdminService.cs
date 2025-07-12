using Microsoft.AspNetCore.Identity;
using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public class AdminService : IAdminService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILoggingService _loggingService;

        public AdminService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILoggingService loggingService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _loggingService = loggingService;
        }

        public async Task<bool> EnsureAdminRoleExistsAsync()
        {
            try
            {
                if (!await _roleManager.RoleExistsAsync("Admin"))
                {
                    var result = await _roleManager.CreateAsync(new IdentityRole("Admin"));
                    if (result.Succeeded)
                    {
                        _loggingService.LogInformation("Admin role created successfully");
                        return true;
                    }
                    else
                    {
                        _loggingService.LogError("Failed to create Admin role: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error ensuring Admin role exists");
                return false;
            }
        }

        public async Task<bool> AssignAdminRoleAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    _loggingService.LogWarning("User not found for admin assignment: {Email}", email);
                    return false;
                }

                // Ensure Admin role exists
                await EnsureAdminRoleExistsAsync();

                if (!await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    var result = await _userManager.AddToRoleAsync(user, "Admin");
                    if (result.Succeeded)
                    {
                        _loggingService.LogInformation("Admin role assigned to user: {Email}", email);
                        return true;
                    }
                    else
                    {
                        _loggingService.LogError("Failed to assign Admin role to user {Email}: {Errors}", 
                            email, string.Join(", ", result.Errors.Select(e => e.Description)));
                        return false;
                    }
                }
                else
                {
                    _loggingService.LogInformation("User {Email} already has Admin role", email);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error assigning Admin role to user: {Email}", email);
                return false;
            }
        }

        public async Task<bool> RemoveAdminRoleAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    _loggingService.LogWarning("User not found for admin removal: {Email}", email);
                    return false;
                }

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    var result = await _userManager.RemoveFromRoleAsync(user, "Admin");
                    if (result.Succeeded)
                    {
                        _loggingService.LogInformation("Admin role removed from user: {Email}", email);
                        return true;
                    }
                    else
                    {
                        _loggingService.LogError("Failed to remove Admin role from user {Email}: {Errors}", 
                            email, string.Join(", ", result.Errors.Select(e => e.Description)));
                        return false;
                    }
                }
                else
                {
                    _loggingService.LogInformation("User {Email} does not have Admin role", email);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error removing Admin role from user: {Email}", email);
                return false;
            }
        }

        public async Task<bool> IsUserAdminAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return false;
                }

                return await _userManager.IsInRoleAsync(user, "Admin");
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error checking if user is admin: {Email}", email);
                return false;
            }
        }

        public async Task<IEnumerable<ApplicationUser>> GetAdminUsersAsync()
        {
            try
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                return adminUsers;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error getting admin users");
                return Enumerable.Empty<ApplicationUser>();
            }
        }
    }
} 